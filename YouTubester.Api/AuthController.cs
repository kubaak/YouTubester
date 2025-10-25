using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Api.Extensions;

namespace YouTubester.Api;

[ApiController]
[Route("auth")]
[Tags("Authentication")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Initiates Google OAuth authentication flow
    /// </summary>
    [HttpGet("google/login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles Google OAuth callback and generates JWT token
    /// </summary>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
        {
            return BadRequest(new { error = "Authentication failed", details = authenticateResult.Failure?.Message });
        }

        var principal = authenticateResult.Principal;
        if (principal == null)
        {
            return BadRequest(new { error = "No user information received" });
        }

        // Generate JWT token
        var jwtToken = HttpContext.RequestServices.GenerateJwtToken(principal);

        var userInfo = new
        {
            email = principal.FindFirstValue(ClaimTypes.Email),
            name = principal.FindFirstValue(ClaimTypes.Name),
            picture = principal.FindFirstValue("picture")
        };

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            // For web applications - redirect to the return URL with token
            return Redirect($"{returnUrl}?token={jwtToken}");
        }

        // For API consumers - return JSON with token and user info
        return Ok(new
        {
            token = jwtToken,
            tokenType = "Bearer",
            expiresIn = 86400, // 24 hours in seconds
            user = userInfo
        });
    }

    /// <summary>
    /// Gets current user information (requires authentication)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var user = new
        {
            email = User.FindFirstValue(ClaimTypes.Email),
            name = User.FindFirstValue(ClaimTypes.Name),
            picture = User.FindFirstValue("picture"),
            isAuthenticated = User.Identity?.IsAuthenticated ?? false
        };

        return Ok(user);
    }

    /// <summary>
    /// Validates the current JWT token
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    public IActionResult ValidateToken()
    {
        return Ok(new { valid = true, message = "Token is valid" });
    }
}