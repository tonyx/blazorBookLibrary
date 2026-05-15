using System.Security.Claims;
using Microsoft.JSInterop;
using BookLibrary.Domain;
using BookLibrary.Shared;
using blazorBookLibrary.Shared;

namespace blazorBookLibrary.Client.Services;

public class TenantStateService
{
    private readonly IJSRuntime _jsRuntime;
    private Tenant? _currentTenant;
    private const string CookieName = "selected_tenant";
    
    public TenantStateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Tenant? CurrentTenant
    {
        get => _currentTenant;
        set
        {
            if (_currentTenant?.TenantId.Value != value?.TenantId.Value)
            {
                _currentTenant = value;
                NotifyStateChanged();
            }
        }
    }

    public event Action? OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();

    public Commons.UserContext GetUserContext(ClaimsPrincipal principal)
    {
        var context = ConverterUtils.fromClaimsPrincipal(principal);
        if (context.IsAuthenticated && _currentTenant != null)
        {
            return context.WithNewTenant(_currentTenant.TenantId);
        }
        return context;
    }

    public async Task SetTenantAsync(Tenant tenant)
    {
        CurrentTenant = tenant;
        try 
        {
            await _jsRuntime.InvokeVoidAsync("blazorCookies.set", CookieName, tenant.TenantId.Value.ToString(), 30);
        }
        catch { /* Prerendering or JS not available */ }
    }

    public async Task<Guid?> GetPersistedTenantIdAsync()
    {
        try 
        {
            var value = await _jsRuntime.InvokeAsync<string>("blazorCookies.get", CookieName);
            if (Guid.TryParse(value, out var guid)) return guid;
        }
        catch { /* Prerendering */ }
        return null;
    }
}
