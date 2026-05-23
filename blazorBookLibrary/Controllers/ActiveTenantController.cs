using Microsoft.AspNetCore.Mvc;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using BookLibrary.Services;
using Microsoft.FSharp.Core;
using System.Threading;
using System.Security.Claims;
using blazorBookLibrary.Shared;

namespace blazorBookLibrary.Controllers;

[Route("ActiveTenant/[action]")]
public class ActiveTenantController : Controller
{
    private readonly IUserService _userService;

    public ActiveTenantController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> SetActiveTenant([FromForm] string tenantId, [FromForm] string returnUrl)
    {
        if (Guid.TryParse(tenantId, out var tenantGuid) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userIdGuid))
            {
                var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
                var userId = Commons.UserId.NewUserId(userIdGuid);
                var tenantIdentifier = Commons.TenantId.NewTenantId(tenantGuid);

                // 1. Persist the tenant selection in the backend event store
                var setTenantResult = await _userService.SetCurrentTenantAsync(
                    userContext, 
                    userId, 
                    tenantIdentifier, 
                    FSharpOption<CancellationToken>.None);

                if (setTenantResult.IsOk)
                {
                    // 2. Set the cookie for immediate recognition by client & server
                    Response.Cookies.Append(
                        "selected_tenant", 
                        tenantGuid.ToString(), 
                        new CookieOptions 
                        { 
                            Expires = DateTimeOffset.UtcNow.AddDays(30), 
                            HttpOnly = false, 
                            SameSite = SameSiteMode.Lax 
                        });
                }
            }
        }

        // Redirect back to the referrer or home page
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl}");
    }
}
