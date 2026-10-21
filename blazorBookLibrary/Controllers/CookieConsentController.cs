using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace blazorBookLibrary.Controllers;

[Route("CookieConsent/[action]")]
public class CookieConsentController : Controller
{
    [IgnoreAntiforgeryToken]
    [HttpPost]
    [HttpGet]
    public IActionResult Dismiss()
    {
        var isHttps = Request.IsHttps || 
                      string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

        var cookieOptions = new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            IsEssential = true,
            Secure = isHttps
        };
        Response.Cookies.Append("cookie_banner_dismissed", "true", cookieOptions);
        return Ok(new { success = true });
    }

    [IgnoreAntiforgeryToken]
    [HttpPost]
    [HttpGet]
    public IActionResult DismissAiBanner()
    {
        var isHttps = Request.IsHttps || 
                      string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

        var cookieOptions = new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            IsEssential = true,
            Secure = isHttps
        };
        Response.Cookies.Append("ai_librarian_banner_dismissed", "true", cookieOptions);
        return Ok(new { success = true });
    }
}
