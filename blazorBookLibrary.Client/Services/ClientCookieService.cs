using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using BookLibrary.Shared.Services;
using Microsoft.FSharp.Core;

namespace blazorBookLibrary.Client.Services
{
    public class ClientCookieService : ICookieService
    {
        private readonly IJSRuntime _jsRuntime;

        public ClientCookieService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<FSharpOption<string>> GetCookieAsync(string key)
        {
            try
            {
                var cookieString = await _jsRuntime.InvokeAsync<string>("eval", "document.cookie");
                if (string.IsNullOrEmpty(cookieString)) 
                {
                    return FSharpOption<string>.None;
                }

                var cookies = cookieString.Split(';');
                foreach (var cookie in cookies)
                {
                    var parts = cookie.Split('=');
                    if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return FSharpOption<string>.Some(parts[1].Trim());
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Gracefully catch prerendering errors when JS Interop is not yet available
            }
            return FSharpOption<string>.None;
        }

        public async Task<Unit> SetCookieAsync(string key, string value, FSharpOption<int> days)
        {
            try
            {
                var expires = "";
                if (days != null && FSharpOption<int>.get_IsSome(days))
                {
                    var date = DateTime.UtcNow.AddDays(days.Value);
                    expires = $"; expires={date:R}";
                }
                var cookie = $"{key}={value}{expires}; path=/; SameSite=Lax; Secure";
                await _jsRuntime.InvokeVoidAsync("eval", $"document.cookie = '{cookie}'");
            }
            catch (InvalidOperationException)
            {
                // Gracefully catch prerendering errors when JS Interop is not yet available
            }
            return null;
        }

        public async Task<Unit> DeleteCookieAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval", $"document.cookie = '{key}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;'");
            }
            catch (InvalidOperationException)
            {
                // Gracefully catch prerendering errors when JS Interop is not yet available
            }
            return null;
        }
    }
}
