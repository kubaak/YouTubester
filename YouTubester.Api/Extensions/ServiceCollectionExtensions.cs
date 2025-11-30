using System.Reflection;
using System.Security.Claims;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using YouTubester.Integration;

namespace YouTubester.Api.Extensions;

/// <summary>
/// 
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Auth setup: Cookie (session) + Google (login)
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddCookieWithGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromHours(12);
                o.LoginPath = "/auth/login/google";
                o.LogoutPath = "/auth/logout";

                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        var isApiRequest =
                            ctx.Request.Path.StartsWithSegments("/auth") ||
                            ctx.Request.Path.StartsWithSegments("/api") ||
                            ctx.Request.Path.StartsWithSegments("/swagger");

                        if (isApiRequest)
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        var returnUrl = ctx.Request.Path + ctx.Request.QueryString;
                        var redirectUri = $"/auth/login/google?returnUrl={Uri.EscapeDataString(returnUrl)}";
                        ctx.Response.Redirect(redirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            })
            .AddGoogle(o =>
            {
                o.ClientId = configuration["GoogleAuth:ClientId"]!;
                o.ClientSecret = configuration["GoogleAuth:ClientSecret"]!;
                o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.SaveTokens = true;
                o.AccessType = "offline";
                o.CallbackPath = "/auth/callback/google";
                o.CorrelationCookie.SameSite = SameSiteMode.None;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.Scope.Add("email");
                o.Scope.Add(YouTubeService.Scope.YoutubeReadonly);
                o.ClaimActions.MapJsonKey("picture", "picture");

                o.Events = new OAuthEvents
                {
                    OnTicketReceived = async context =>
                    {
                        var accessToken = context.Properties?.GetTokenValue("access_token");
                        if (string.IsNullOrWhiteSpace(accessToken))
                        {
                            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                            var logger = loggerFactory.CreateLogger("YouTubester.Api.Authentication");
                            logger.LogWarning("Access token was not available during Google login; skipping channel enrichment.");
                            return;
                        }

                        var youTubeIntegration = context.HttpContext.RequestServices.GetRequiredService<IYouTubeIntegration>();
                        var userChannel = await youTubeIntegration.GetCurrentChannelAsync(accessToken, context.HttpContext.RequestAborted);

                        if (userChannel is null)
                        {
                            return;
                        }

                        var claimsIdentity = (ClaimsIdentity)context.Principal!.Identity!;
                        claimsIdentity.AddClaim(new Claim("yt_channel_id", userChannel.Id));
                        claimsIdentity.AddClaim(new Claim("yt_channel_title", userChannel.Title ?? string.Empty));
                        claimsIdentity.AddClaim(new Claim("yt_channel_picture", userChannel.Picture ?? string.Empty));
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Adds swagger services
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SupportNonNullableReferenceTypes();

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "YouTubester API",
                Version = "v1",
                Description =
                    "To access protected endpoints, first log in:\n\n" +
                    "[🔐 Login with Google](/api/auth/login/google?returnUrl=/swagger/index.html)"
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });
        return services;
    }
}