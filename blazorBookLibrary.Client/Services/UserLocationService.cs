using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using BookLibrary.Shared;
using BookLibrary.Shared.Services;
using Microsoft.FSharp.Core;

namespace blazorBookLibrary.Client.Services
{
    public class UserLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusInMeters { get; set; } = 25000.0;
        public string? Label { get; set; }
    }

    public interface IUserLocationService
    {
        Task<UserLocation?> GetLocationAsync();
        Task<Commons.Coordinates?> GetCoordinatesAsync();
        Task SetLocationAsync(double latitude, double longitude, double radiusInMeters, string? label = null);
        Task ClearLocationAsync();
        Task<UserLocation?> ForceRefreshAsync();
        event Action? OnLocationChanged;
    }

    public class UserLocationService : IUserLocationService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ICookieService _cookieService;
        private UserLocation? _cachedLocation;
        private bool _isInitialized;

        public const string LocalStorageKey = "biblionet_user_coordinates";
        public const string CookieName = "user_coordinates";
        public const double DefaultRadiusInMeters = 25000.0;

        public event Action? OnLocationChanged;

        public UserLocationService(IJSRuntime jsRuntime, ICookieService cookieService)
        {
            _jsRuntime = jsRuntime;
            _cookieService = cookieService;
        }

        public async Task<UserLocation?> ForceRefreshAsync()
        {
            _isInitialized = false;
            return await GetLocationAsync();
        }

        public async Task<UserLocation?> GetLocationAsync()
        {
            if (_isInitialized)
            {
                return _cachedLocation;
            }

            bool jsSuccess = false;
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
                jsSuccess = true;
            }
            catch (InvalidOperationException)
            {
                // Prerendering: JS interop not ready yet
            }
            catch (Exception)
            {
                jsSuccess = true;
            }

            if (jsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var location = ParseLocationJson(json);
                    if (location != null)
                    {
                        _cachedLocation = location;
                        _isInitialized = true;
                        return _cachedLocation;
                    }
                }

                // If localStorage is empty, check cookie (fallback for SSR-set cookies or when JS interop first becomes ready)
                try
                {
                    var cookieOpt = await _cookieService.GetCookieAsync(CookieName);
                    if (cookieOpt != null && FSharpOption<string>.get_IsSome(cookieOpt) && !string.IsNullOrWhiteSpace(cookieOpt.Value))
                    {
                        var cookieVal = Uri.UnescapeDataString(cookieOpt.Value);
                        var location = ParseLocationJson(cookieVal);
                        if (location != null)
                        {
                            _cachedLocation = location;
                            _isInitialized = true;
                            // Mirror to localStorage
                            try
                            {
                                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, cookieVal);
                            }
                            catch { }
                            return _cachedLocation;
                        }
                    }
                }
                catch { }

                // LocalStorage was queried and is empty (or cleared)
                _cachedLocation = null;
                _isInitialized = true;
                return null;
            }
            else
            {
                // Prerendering: JS Interop is not ready. Read cookie for initial render without caching permanently.
                try
                {
                    var cookieOpt = await _cookieService.GetCookieAsync(CookieName);
                    if (cookieOpt != null && FSharpOption<string>.get_IsSome(cookieOpt) && !string.IsNullOrWhiteSpace(cookieOpt.Value))
                    {
                        var cookieVal = Uri.UnescapeDataString(cookieOpt.Value);
                        var location = ParseLocationJson(cookieVal);
                        if (location != null)
                        {
                            return location;
                        }
                    }
                }
                catch { }

                return null;
            }
        }

        public async Task<Commons.Coordinates?> GetCoordinatesAsync()
        {
            var loc = await GetLocationAsync();
            if (loc == null) return null;

            return Commons.Coordinates.New(loc.Latitude, loc.Longitude, loc.RadiusInMeters);
        }

        public async Task SetLocationAsync(double latitude, double longitude, double radiusInMeters, string? label = null)
        {
            var loc = new UserLocation
            {
                Latitude = Math.Round(latitude, 6),
                Longitude = Math.Round(longitude, 6),
                RadiusInMeters = radiusInMeters > 0 ? radiusInMeters : DefaultRadiusInMeters,
                Label = label
            };

            _cachedLocation = loc;
            _isInitialized = true;

            var json = JsonSerializer.Serialize(loc);
            var escapedJson = Uri.EscapeDataString(json);

            try
            {
                await _jsRuntime.InvokeVoidAsync("eval",
                    $"try {{ localStorage.setItem('{LocalStorageKey}', {JsonSerializer.Serialize(json)}); }} catch(e){{}} " +
                    $"(function() {{ var sec = window.location.protocol === 'https:' ? '; Secure' : ''; document.cookie = '{CookieName}={escapedJson}; max-age={30 * 86400}; path=/; SameSite=Lax' + sec; }})()");
            }
            catch { }

            try
            {
                // Mirror to cookie with 30-day lifespan
                await _cookieService.SetCookieAsync(CookieName, escapedJson, FSharpOption<int>.Some(30));
            }
            catch { }

            OnLocationChanged?.Invoke();
        }

        public async Task ClearLocationAsync()
        {
            _cachedLocation = null;
            _isInitialized = true;

            try
            {
                await _jsRuntime.InvokeVoidAsync("eval",
                    $"try {{ localStorage.removeItem('{LocalStorageKey}'); }} catch(e){{}} " +
                    $"document.cookie = '{CookieName}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; max-age=0; path=/; SameSite=Lax;'; " +
                    $"document.cookie = '{CookieName}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; max-age=0; path=/; SameSite=Lax; Secure;';");
            }
            catch { }

            try
            {
                await _cookieService.DeleteCookieAsync(CookieName);
            }
            catch { }

            OnLocationChanged?.Invoke();
        }

        private static UserLocation? ParseLocationJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<UserLocation>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }
    }
}
