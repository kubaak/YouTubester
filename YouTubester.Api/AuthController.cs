using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YouTubester.Api;

/// <summary>
/// Authentication Controller
/// </summary>
[ApiController]
[Route("auth")]
[Tags("Authentication")]
[Authorize]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("login/google")]
    [AllowAnonymous]
    public IActionResult LoginWithGoogle([FromQuery] string? returnUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        var props = new AuthenticationProperties { RedirectUri = returnUrl };

        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            name = User.Identity?.Name,
            email = User.FindFirst(ClaimTypes.Email)?.Value,
            sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            picture = User.FindFirst("picture")?.Value
        });
    }
}