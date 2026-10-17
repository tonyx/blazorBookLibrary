using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace blazorBookLibrary.Controllers;

[Route("CookieConsent/[action]")]
public class CookieConsentController : Controller
{
    [HttpPost]
    [HttpGet]
    public IActionResult Dismiss()
    {
        var cookieOptions = new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            IsEssential = true,
            Secure = Request.IsHttps
        };
        Response.Cookies.Append("cookie_banner_dismissed", "true", cookieOptions);
        return Ok(new { success = true });
    }
}
