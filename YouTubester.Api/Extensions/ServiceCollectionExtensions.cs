using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.OpenApi.Models;
using YouTubester.Persistence.Users;

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
                o.CallbackPath = "/auth/callback/google";
                o.CorrelationCookie.SameSite = SameSiteMode.None;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Scope.Add("profile");
                o.ClaimActions.MapJsonKey("picture", "picture");

                o.Events = new OAuthEvents
                {
                    OnTicketReceived = async context =>
                    {
                        var principal = context.Principal;
                        if (principal is null)
                        {
                            return;
                        }

                        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            return;
                        }

                        var email = principal.FindFirstValue(ClaimTypes.Email);
                        var name = principal.Identity?.Name;
                        var picture = principal.FindFirst("picture")?.Value;

                        var accessToken = context.Properties?.GetTokenValue("access_token");
                        var refreshToken = context.Properties?.GetTokenValue("refresh_token");
                        var expiresAtRaw = context.Properties?.GetTokenValue("expires_at");
                        DateTimeOffset? expiresAt = null;
                        if (!string.IsNullOrWhiteSpace(expiresAtRaw) &&
                            DateTimeOffset.TryParse(expiresAtRaw, out var parsedExpiresAt))
                        {
                            expiresAt = parsedExpiresAt;
                        }

                        var requestServices = context.HttpContext.RequestServices;
                        var userRepository = requestServices.GetRequiredService<IUserRepository>();
                        var userTokenStore = requestServices.GetRequiredService<IUserTokenStore>();
                        var now = DateTimeOffset.UtcNow;
                        var cancellationToken = context.HttpContext.RequestAborted;

                        await userRepository.UpsertUserAsync(userId, email, name, picture, now, cancellationToken);
                        await userTokenStore.UpsertGoogleTokenAsync(
                            userId,
                            accessToken,
                            refreshToken,
                            expiresAt,
                            cancellationToken);
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
                    "[🔐 Login with Google](/auth/login/google?returnUrl=/swagger/index.html)"
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