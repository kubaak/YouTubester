using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.OpenApi.Models;

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
                o.SaveTokens = false;
                o.CallbackPath = "/auth/callback/google";
                o.CorrelationCookie.SameSite = SameSiteMode.None;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Scope.Add("profile");
                o.ClaimActions.MapJsonKey("picture", "picture");
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