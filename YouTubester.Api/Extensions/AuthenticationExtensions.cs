using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using YouTubester.Api.Configuration;

namespace YouTubester.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddGoogleAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment? environment = null)
    {
        // Skip Google OAuth configuration in test environment
        var isTestEnvironment = environment?.EnvironmentName == "Test" || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Test";

        if (isTestEnvironment)
        {
            // Add basic authentication and authorization for tests
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options => { }); // Mock configuration

            services.AddAuthorization();
            return services;
        }

        var googleAuthOptions = configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section: {GoogleAuthOptions.SectionName}");

        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));

        // JWT configuration
        var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? configuration["JWT_SECRET_KEY"]
            ?? "your-256-bit-secret-key-for-development-only-do-not-use-in-production";

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["JWT_ISSUER"] ?? "YouTubester.Api",
                    ValidateAudience = true,
                    ValidAudience = configuration["JWT_AUDIENCE"] ?? "YouTubester.Client",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.NoResult();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        var result = System.Text.Json.JsonSerializer.Serialize(new { error = "Invalid token" });
                        return context.Response.WriteAsync(result);
                    }
                };
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleAuthOptions.ClientId;
                options.ClientSecret = googleAuthOptions.ClientSecret;
                options.CallbackPath = "/auth/google/callback";

                // Request email and profile scopes
                options.Scope.Add("email");
                options.Scope.Add("profile");

                options.Events.OnCreatingTicket = context =>
                {
                    var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                    var emailVerified = context.Principal?.FindFirstValue("email_verified");

                    if (string.IsNullOrEmpty(email))
                    {
                        context.Fail("Email claim not found");
                        return Task.CompletedTask;
                    }

                    if (googleAuthOptions.RequireEmailVerification && emailVerified != "true")
                    {
                        context.Fail("Email not verified");
                        return Task.CompletedTask;
                    }

                    // Check allowed emails or domains
                    if (googleAuthOptions.AllowedEmails.Length > 0 && !googleAuthOptions.AllowedEmails.Contains(email))
                    {
                        context.Fail("Email not in allowed list");
                        return Task.CompletedTask;
                    }

                    if (googleAuthOptions.AllowedEmailDomains.Length > 0)
                    {
                        var emailDomain = email.Split('@').LastOrDefault();
                        if (string.IsNullOrEmpty(emailDomain) || !googleAuthOptions.AllowedEmailDomains.Contains(emailDomain))
                        {
                            context.Fail("Email domain not allowed");
                            return Task.CompletedTask;
                        }
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static string GenerateJwtToken(this IServiceProvider serviceProvider, ClaimsPrincipal user)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? configuration["JWT_SECRET_KEY"]
            ?? "your-256-bit-secret-key-for-development-only-do-not-use-in-production";

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(user.Claims),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = configuration["JWT_ISSUER"] ?? "YouTubester.Api",
            Audience = configuration["JWT_AUDIENCE"] ?? "YouTubester.Client",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}