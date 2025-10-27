namespace YouTubester.Api.Configuration;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string[] AllowedEmailDomains { get; set; } = [];
    public string[] AllowedEmails { get; set; } = [];
    public bool RequireEmailVerification { get; set; } = true;
}