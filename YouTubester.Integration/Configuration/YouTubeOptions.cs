namespace YouTubester.Integration.Configuration;

public sealed class YouTubeOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ReferenceVideoUrl { get; set; } = "";
    public string[] TargetPlaylists { get; set; } = [];
}