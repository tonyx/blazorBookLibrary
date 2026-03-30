using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace blazorBookLibrary.Controllers;

[Route("Culture/[action]")]
public class CultureController : Controller
{
    public IActionResult SetCulture(string culture, string returnUrl)
    {
        if (culture != null)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : $"/{returnUrl}");
    }
}
