using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BookLibrary.Domain;
using BookLibrary.Shared;
using blazorBookLibrary.Shared;
using BookLibrary.Shared.Services;

namespace blazorBookLibrary.Client.Services
{
    public class TenantStateService
    {
        private readonly ICookieService _cookieService;
        private Tenant? _currentTenant;
        private const string CookieName = "selected_tenant_id";
        
        public TenantStateService(ICookieService cookieService)
        {
            _cookieService = cookieService;
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
                await _cookieService.SetCookieAsync(CookieName, tenant.TenantId.Value.ToString(), Microsoft.FSharp.Core.FSharpOption<int>.Some(30));
            }
            catch { /* Prerendering or JS not available */ }
        }

        public async Task ClearTenantAsync()
        {
            CurrentTenant = null;
            try 
            {
                await _cookieService.DeleteCookieAsync(CookieName);
            }
            catch { /* Prerendering or JS not available */ }
        }

        public async Task<Guid?> GetPersistedTenantIdAsync()
        {
            try 
            {
                var valueOpt = await _cookieService.GetCookieAsync(CookieName);
                if (valueOpt != null && Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(valueOpt))
                {
                    if (Guid.TryParse(valueOpt.Value, out var guid)) return guid;
                }
            }
            catch { /* Prerendering */ }
            return null;
        }

        public bool HasManagementAccess(ClaimsPrincipal? principal, Tenant? tenant = null)
        {
            if (principal == null || principal.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (principal.IsInRole("Admin"))
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

            if (principal.IsInRole("Admin"))
            {
                return true;
            }

            var targetTenant = tenant ?? CurrentTenant;
            if (targetTenant == null)
            {
                try
                {
                    var userContext = GetUserContext(principal);
                    var tenantResult = await resolverService.GetTenantForUserAsync(userContext, Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);
                    if (tenantResult.IsOk)
                    {
                        var tenantInfoResult = await tenantService.GetTenantAsync(userContext, tenantResult.ResultValue, System.Threading.CancellationToken.None);
                        if (tenantInfoResult.IsOk)
                        {
                            targetTenant = tenantInfoResult.ResultValue;
                            if (CurrentTenant == null)
                            {
                                CurrentTenant = targetTenant;
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
