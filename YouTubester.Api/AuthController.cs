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
[Route("api/auth")]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Me()
    {
        var name = User.Identity?.Name;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var googlePicture = User.FindFirst("picture")?.Value;

        var channelId = User.FindFirst("yt_channel_id")?.Value;
        var channelTitle = User.FindFirst("yt_channel_title")?.Value;
        var channelPicture = User.FindFirst("yt_channel_picture")?.Value;

        var picture = string.IsNullOrWhiteSpace(channelPicture)
            ? googlePicture
            : channelPicture;

        return Ok(new
        {
            name,
            email,
            sub = subject,
            channelId,
            channelTitle,
            picture
        });
    }
}
