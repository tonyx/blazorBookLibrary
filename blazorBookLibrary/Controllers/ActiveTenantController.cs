using Microsoft.AspNetCore.Mvc;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using BookLibrary.Services;
using Microsoft.FSharp.Core;
using System.Threading;
using System.Security.Claims;
using blazorBookLibrary.Shared;

using blazorBookLibrary.Infrastructure.Services;

namespace blazorBookLibrary.Controllers;

[Route("ActiveTenant/[action]")]
public class ActiveTenantController : Controller
{
    private readonly IUserService _userService;
    private readonly ITenantCacheWarmupService _warmupService;

    public ActiveTenantController(IUserService userService, ITenantCacheWarmupService warmupService)
    {
        _userService = userService;
        _warmupService = warmupService;
    }

    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> SetActiveTenant(
        [FromForm(Name = "tenantId")] string? formTenantId,
        [FromQuery(Name = "tenantId")] string? queryTenantId,
        [FromForm(Name = "returnUrl")] string? formReturnUrl,
        [FromQuery(Name = "returnUrl")] string? queryReturnUrl)
    {
        var tenantId = !string.IsNullOrWhiteSpace(formTenantId) ? formTenantId : queryTenantId;
        var returnUrl = !string.IsNullOrWhiteSpace(formReturnUrl) ? formReturnUrl : queryReturnUrl;

        return await ProcessTenantSelectionAsync(tenantId, returnUrl);
    }

    [HttpGet("/tenant/{tenantId}")]
    [HttpGet("/t/{tenantId}")]
    [HttpGet("Select")]
    public async Task<IActionResult> Select(string? tenantId, [FromQuery] string? returnUrl = null)
    {
        return await ProcessTenantSelectionAsync(tenantId, returnUrl);
    }

    private async Task<IActionResult> ProcessTenantSelectionAsync(string? tenantId, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            Guid tenantGuid;
            if (string.Equals(tenantId, "default", StringComparison.OrdinalIgnoreCase))
            {
                tenantGuid = Commons.TenantId.Default.Value;
            }
            else if (!Guid.TryParse(tenantId, out tenantGuid))
            {
                tenantGuid = Guid.Empty;
            }

            if (tenantGuid != Guid.Empty)
            {
                var tenantIdentifier = Commons.TenantId.NewTenantId(tenantGuid);
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Path = "/"
                };

                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdStr, out var userIdGuid))
                    {
                        var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
                        var userId = Commons.UserId.NewUserId(userIdGuid);

                        // 1. Persist the tenant selection in the backend event store
                        var setTenantResult = await _userService.SetCurrentTenantAsync(
                            userContext,
                            userId,
                            tenantIdentifier,
                            FSharpOption<CancellationToken>.None);

                        if (setTenantResult.IsOk)
                        {
                            // 2. Set the cookies for immediate recognition by client & server
                            Response.Cookies.Append("selected_tenant_id", tenantGuid.ToString(), cookieOptions);
                            Response.Cookies.Append("selected_tenant", tenantGuid.ToString(), cookieOptions);

                            // 3. Warm up the cache for the new tenant
                            _ = Task.Run(() => _warmupService.WarmupTenantAsync(tenantIdentifier, CancellationToken.None));
                        }
                    }
                }
                else
                {
                    // Anonymous user - set cookies and warm up cache
                    Response.Cookies.Append("selected_tenant_id", tenantGuid.ToString(), cookieOptions);
                    Response.Cookies.Append("selected_tenant", tenantGuid.ToString(), cookieOptions);

                    _ = Task.Run(() => _warmupService.WarmupTenantAsync(tenantIdentifier, CancellationToken.None));
                }
            }
        }

        // Redirect back to returnUrl safely
        var redirectUrl = "/";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            redirectUrl = returnUrl.StartsWith("/") ? returnUrl : $"/{returnUrl}";
        }

        return LocalRedirect(redirectUrl);
    }
}
