using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BookLibrary.Domain;
using BookLibrary.Shared;
using blazorBookLibrary.Shared;
using BookLibrary.Shared.Services;
using Microsoft.Extensions.Configuration;

namespace blazorBookLibrary.Client.Services
{
    public class TenantStateService
    {
        private readonly ICookieService _cookieService;
        private readonly IConfiguration _configuration;
        private Tenant? _currentTenant;
        private const string CookieName = "selected_tenant_id";
        private const string LegacyCookieName = "selected_tenant";
        
        public TenantStateService(ICookieService cookieService, IConfiguration configuration)
        {
            _cookieService = cookieService;
            _configuration = configuration;
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
                var expiration = Microsoft.FSharp.Core.FSharpOption<int>.Some(30);
                await _cookieService.SetCookieAsync(CookieName, tenant.TenantId.Value.ToString(), expiration);
                await _cookieService.SetCookieAsync(LegacyCookieName, tenant.TenantId.Value.ToString(), expiration);
            }
            catch { /* Prerendering or JS not available */ }
        }

        public async Task ClearTenantAsync()
        {
            CurrentTenant = null;
            try 
            {
                await _cookieService.DeleteCookieAsync(CookieName);
                await _cookieService.DeleteCookieAsync(LegacyCookieName);
            }
            catch { /* Prerendering or JS not available */ }
        }

        public async Task<Guid?> GetPersistedTenantIdAsync()
        {
            try 
            {
                var valueOpt = await _cookieService.GetCookieAsync(CookieName);
                if (valueOpt == null || !Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(valueOpt))
                {
                    valueOpt = await _cookieService.GetCookieAsync(LegacyCookieName);
                }

                if (valueOpt != null && Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(valueOpt))
                {
                    if (Guid.TryParse(valueOpt.Value, out var guid)) return guid;
                }
            }
            catch { /* Prerendering */ }
            return null;
        }

        public bool IsAdministrator(ClaimsPrincipal? principal)
        {
            if (principal == null || principal.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (principal.IsInRole("Admin"))
            {
                return true;
            }

            var adminUsername = _configuration["AdminUsername"];
            if (!string.IsNullOrWhiteSpace(adminUsername))
            {
                var name = principal.Identity?.Name;
                var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
                if (string.Equals(name, adminUsername, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(email, adminUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasManagementAccess(ClaimsPrincipal? principal, Tenant? tenant = null)
        {
            if (principal == null || principal.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (IsAdministrator(principal) || principal.IsInRole("Manager"))
            {
                return true;
            }

            var targetTenant = tenant ?? CurrentTenant;
            if (targetTenant == null)
            {
                return false;
            }

            var userIdClaim = principal.FindFirst(c => c.Type == ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var guid))
            {
                var userId = Commons.UserId.NewUserId(guid);
                return guid.Equals(targetTenant.OwnerId.Value) 
                    || targetTenant.IsOwner(userId) 
                    || targetTenant.IsManager(userId);
            }

            var userContext = ConverterUtils.fromClaimsPrincipal(principal);
            if (userContext.UserId != null && Microsoft.FSharp.Core.FSharpOption<Commons.UserId>.get_IsSome(userContext.UserId))
            {
                var userId = userContext.UserId.Value;
                return targetTenant.IsOwner(userId) || targetTenant.IsManager(userId);
            }

            return false;
        }

        public async Task<bool> CheckManagementAccessAsync(
            ClaimsPrincipal? principal,
            ITenantService tenantService,
            IUserTenantResolverService resolverService,
            Tenant? tenant = null)
        {
            if (principal == null || principal.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (IsAdministrator(principal) || principal.IsInRole("Manager"))
            {
                return true;
            }

            var targetTenant = tenant ?? CurrentTenant;
            if (targetTenant == null || (!HasManagementAccess(principal, targetTenant) && targetTenant.TenantId.Equals(Commons.TenantId.Default)))
            {
                try
                {
                    var userContext = GetUserContext(principal);
                    var myTenantsRes = await tenantService.GetMyTenantsAsync(userContext, Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);
                    if (myTenantsRes.IsOk && myTenantsRes.ResultValue.Any())
                    {
                        var managedTenant = myTenantsRes.ResultValue.FirstOrDefault(t => HasManagementAccess(principal, t));
                        if (managedTenant != null)
                        {
                            targetTenant = managedTenant;
                            CurrentTenant = targetTenant;
                        }
                    }

                    if (targetTenant == null || (!HasManagementAccess(principal, targetTenant) && targetTenant.TenantId.Equals(Commons.TenantId.Default)))
                    {
                        var tenantResult = await resolverService.GetTenantForUserAsync(userContext, Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);
                        if (tenantResult.IsOk)
                        {
                            var tenantInfoResult = await tenantService.GetTenantAsync(userContext, tenantResult.ResultValue, System.Threading.CancellationToken.None);
                            if (tenantInfoResult.IsOk)
                            {
                                targetTenant = tenantInfoResult.ResultValue;
                                if (CurrentTenant == null || CurrentTenant.TenantId.Equals(Commons.TenantId.Default))
                                {
                                    CurrentTenant = targetTenant;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Silent fallback
                }
            }

            return HasManagementAccess(principal, targetTenant);
        }
    }
}
