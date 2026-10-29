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
            if (_cachedLocation != null)
            {
                return _cachedLocation;
            }

            if (_isInitialized)
            {
                return null;
            }

            bool jsAvailable = false;
            string? json = null;

            // 1. Try reading from browser storage via biblioStorage or direct localStorage
            try
            {
                try
                {
                    json = await _jsRuntime.InvokeAsync<string?>("biblioStorage.getItem", LocalStorageKey);
                    jsAvailable = true;
                }
                catch (JSException)
                {
                    // Fallback to direct localStorage if biblioStorage helper isn't loaded yet
                    json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
                    jsAvailable = true;
                }
            }
            catch (InvalidOperationException)
            {
                // Prerendering: JS interop not ready yet
                jsAvailable = false;
            }
            catch (Exception)
            {
                // Edge / Incognito restricted storage
                jsAvailable = true;
            }

            if (jsAvailable)
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

                // 2. Check document.cookie via JS helper (works even when localStorage is blocked in InPrivate)
                try
                {
                    var cookieVal = await _jsRuntime.InvokeAsync<string?>("biblioCookies.get", CookieName);
                    if (!string.IsNullOrWhiteSpace(cookieVal))
                    {
                        var location = ParseLocationJson(cookieVal);
                        if (location != null)
                        {
                            _cachedLocation = location;
                            _isInitialized = true;
                            // Mirror to localStorage if possible
                            try
                            {
                                await _jsRuntime.InvokeVoidAsync("biblioStorage.setItem", LocalStorageKey, cookieVal);
                            }
                            catch { }
                            return _cachedLocation;
                        }
                    }
                }
                catch { }

                // 3. Fall back to ICookieService
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
                            return _cachedLocation;
                        }
                    }
                }
                catch { }

                // Browser queried and no location found
                _cachedLocation = null;
                _isInitialized = true;
                return null;
            }
            else
            {
                // Prerendering: JS Interop is not ready. Read cookie from HTTP headers without permanently locking _isInitialized.
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

            // 1. Write to LocalStorage (via helper or direct)
            try
            {
                await _jsRuntime.InvokeVoidAsync("biblioStorage.setItem", LocalStorageKey, json);
            }
            catch
            {
                try
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
                }
                catch { }
            }

            // 2. Write to Cookie via JS (reliable in Blazor Server & WebAssembly)
            try
            {
                await _jsRuntime.InvokeVoidAsync("biblioCookies.set", CookieName, json, 30);
            }
            catch { }

            // 3. Also notify ICookieService
            try
            {
                await _cookieService.SetCookieAsync(CookieName, escapedJson, FSharpOption<int>.Some(30));
            }
            catch { }

            OnLocationChanged?.Invoke();
        }

        public async Task ClearLocationAsync()
        {
            _cachedLocation = null;
            _isInitialized = true;

            // 1. Clear LocalStorage via helper or direct
            try
            {
                await _jsRuntime.InvokeVoidAsync("biblioStorage.removeItem", LocalStorageKey);
            }
            catch
            {
                try
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LocalStorageKey);
                }
                catch { }
            }

            // 2. Clear Cookie via JS
            try
            {
                await _jsRuntime.InvokeVoidAsync("biblioCookies.delete", CookieName);
            }
            catch { }

            // 3. Clear Cookie via ICookieService
            try
            {
                await _cookieService.DeleteCookieAsync(CookieName);
            }
            catch { }

            OnLocationChanged?.Invoke();
        }

        private static UserLocation? ParseLocationJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            // Handle URL-encoded cookies if passed raw
            var candidate = json;
            if (candidate.StartsWith("%7B", StringComparison.OrdinalIgnoreCase))
            {
                candidate = Uri.UnescapeDataString(candidate);
            }

            try
            {
                var loc = JsonSerializer.Deserialize<UserLocation>(candidate, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (loc != null && (loc.Latitude != 0 || loc.Longitude != 0))
                {
                    return loc;
                }
            }
            catch
            {
                // Handle accidental double-encoded JSON string literal fallback
                try
                {
                    var inner = JsonSerializer.Deserialize<string>(candidate);
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        var loc = JsonSerializer.Deserialize<UserLocation>(inner, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (loc != null && (loc.Latitude != 0 || loc.Longitude != 0))
                        {
                            return loc;
                        }
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
