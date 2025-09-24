namespace YouTubester.Integration.Configuration;

public sealed class YouTubeAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ApplicationName { get; set; } = "YouTubester";
    public string UserName { get; set; } = "user";
    public string TokenStoreFolder { get; set; } = "YouTubeAuth";
}