using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using BookLibrary.Services;
using Microsoft.FSharp.Core;
using System.Threading;
using System.Security.Claims;

namespace blazorBookLibrary.Controllers;

[Route("Culture/[action]")]
public class CultureController : Controller
{
    private readonly IUserService _userService;

    public CultureController(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> SetCulture(string culture, string returnUrl)
    {
        if (culture != null)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdStr, out var userIdGuid))
                {
                    var userContext = UserContextMapper.mapFromClaimsPrincipal(User);
                    var userId = Commons.UserId.NewUserId(userIdGuid);
                    var langPref = Commons.ShortLang.New(culture);

                    await _userService.SetLangPrefAsync(userContext, userId, langPref, FSharpOption<CancellationToken>.None);
                }
            }
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl}");
    }
}
