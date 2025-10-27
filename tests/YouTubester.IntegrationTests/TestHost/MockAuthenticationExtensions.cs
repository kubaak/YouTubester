using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YouTubester.IntegrationTests.TestHost;

public static class MockAuthenticationExtensions
{
    public const string TestScheme = "Test";
    public const string TestEmail = "test@example.com";
    public const string TestName = "Test User";
    public const string TestPicture = "https://example.com/test-picture.jpg";

    public static IServiceCollection AddMockAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(TestScheme)
            .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(TestScheme, options => { });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(TestScheme)
                .Build();
        });

        return services;
    }
}

public sealed class MockAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public MockAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, MockAuthenticationExtensions.TestName),
            new Claim(ClaimTypes.Email, MockAuthenticationExtensions.TestEmail),
            new Claim("picture", MockAuthenticationExtensions.TestPicture),
            new Claim("email_verified", "true")
        };

        var identity = new ClaimsIdentity(claims, MockAuthenticationExtensions.TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, MockAuthenticationExtensions.TestScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}