using Microsoft.AspNetCore.Mvc;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using BookLibrary.Services;
using Microsoft.FSharp.Core;

namespace blazorBookLibrary.Controllers;

[Route("Notifications/[action]")]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly ITenantService _tenantService;

    public NotificationsController(INotificationService notificationService, ITenantService tenantService)
    {
        _notificationService = notificationService;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> HandleClick(string notificationId, string redirectUrl)
    {
        if (Guid.TryParse(notificationId, out var notifGuid) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notifId = BookLibrary.Domain.NotificationId.NewNotificationId(notifGuid);
            await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(redirectUrl) ? "/" : redirectUrl);
    }

    [HttpGet]
    public async Task<IActionResult> AcceptInvitation(string notificationId, string tenantId, string code)
    {
        if (Guid.TryParse(notificationId, out var notifGuid) && 
            Guid.TryParse(tenantId, out var tenantGuid) && 
            Guid.TryParse(code, out var codeGuid) && 
            User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notifId = BookLibrary.Domain.NotificationId.NewNotificationId(notifGuid);
            var tId = Commons.TenantId.NewTenantId(tenantGuid);
            var invitationCode = Commons.PatronInvitationCode.NewPatronInvitationCode(codeGuid);

            var acceptResult = await _tenantService.ConvertInvitedPatronToPatronAsync(userContext, tId, invitationCode, FSharpOption<CancellationToken>.None);
            if (acceptResult.IsOk)
            {
                await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
                
                // Set active tenant cookie so it takes effect instantly on the next page render
                Response.Cookies.Append(
                    "selected_tenant", 
                    tenantGuid.ToString(), 
                    new CookieOptions 
                    { 
                        Expires = DateTimeOffset.UtcNow.AddDays(30), 
                        HttpOnly = false, 
                        SameSite = SameSiteMode.Lax 
                    });
                
                return LocalRedirect("/tenants");
            }
            else
            {
                TempData["ErrorMessage"] = acceptResult.ErrorValue;
                return LocalRedirect("/tenants");
            }
        }
        return LocalRedirect("/tenants");
    }

    [HttpGet]
    public async Task<IActionResult> ApproveJoin(string notificationId, string tenantId, string? requesterId = null)
    {
        if (Guid.TryParse(notificationId, out var notifGuid) && 
            Guid.TryParse(tenantId, out var tenantGuid) && 
            User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notifId = BookLibrary.Domain.NotificationId.NewNotificationId(notifGuid);
            var tId = Commons.TenantId.NewTenantId(tenantGuid);

            if (!string.IsNullOrEmpty(requesterId) && Guid.TryParse(requesterId, out var requesterGuid))
            {
                var reqId = Commons.UserId.NewUserId(requesterGuid);
                var result = await _tenantService.ApproveJoinRequestAsync(userContext, tId, reqId, FSharpOption<CancellationToken>.None);
            }
            
            await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
            return LocalRedirect($"/tenants/{tenantGuid}/joinRequests?status=approved");
        }
        return LocalRedirect("/tenants");
    }

    [HttpGet]
    public async Task<IActionResult> Dismiss(string notificationId, string returnUrl)
    {
        if (Guid.TryParse(notificationId, out var notifGuid) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notifId = BookLibrary.Domain.NotificationId.NewNotificationId(notifGuid);
            await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
        }
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl.TrimStart('/')}");
    }

    [HttpGet]
    public async Task<IActionResult> RejectJoin(string notificationId, string? tenantId = null, string? requesterId = null, string? returnUrl = null)
    {
        if (Guid.TryParse(notificationId, out var notifGuid) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notifId = BookLibrary.Domain.NotificationId.NewNotificationId(notifGuid);

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tenantGuid) &&
                !string.IsNullOrEmpty(requesterId) && Guid.TryParse(requesterId, out var requesterGuid))
            {
                var tId = Commons.TenantId.NewTenantId(tenantGuid);
                var reqId = Commons.UserId.NewUserId(requesterGuid);
                var result = await _tenantService.RejectJoinRequestAsync(userContext, tId, reqId, FSharpOption<CancellationToken>.None);
                
                await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
                return LocalRedirect($"/tenants/{tenantGuid}/joinRequests?status=rejected");
            }

            await _notificationService.MarkAsReadAsync(userContext, notifId, FSharpOption<CancellationToken>.None);
        }
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl.TrimStart('/')}");
    }

    [HttpGet]
    public async Task<IActionResult> MarkAllAsRead(string returnUrl)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var notificationsResult = await _notificationService.GetUnreadNotificationsForUserAsync(userContext, FSharpOption<CancellationToken>.None);
            if (notificationsResult.IsOk)
            {
                foreach (var notif in notificationsResult.ResultValue)
                {
                    await _notificationService.MarkAsReadAsync(userContext, notif.NotificationId, FSharpOption<CancellationToken>.None);
                }
            }
        }
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl.TrimStart('/')}");
    }

    [HttpGet]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
            var result = await _notificationService.GetUnreadNotificationsForUserAsync(userContext, FSharpOption<CancellationToken>.None);
            if (result.IsOk)
            {
                var list = result.ResultValue.Select(n => new {
                    notificationId = n.NotificationId.Value,
                    title = n.Title,
                    content = n.Content,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt,
                    actionUrl = FSharpOption<string>.get_IsSome(n.ActionUrl) ? n.ActionUrl.Value : ""
                }).ToList();
                return Json(list);
            }
        }
        return Json(new object[0]);
    }
}
