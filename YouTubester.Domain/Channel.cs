namespace YouTubester.Domain;

public sealed class Channel
{
    public string Name { get; set; } = default!;
    public string ChannelId { get; set; } = default!;
    public string UploadsPlaylistId { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastUploadsCutoff { get; set; }

    public static Channel Create(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt, DateTimeOffset? lastUploadsCutoff = null)
    {
        return new Channel(channelId, name, uploadsPlaylistId, updatedAt, lastUploadsCutoff);
    }

    private Channel(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt, DateTimeOffset? lastUploadsCutoff)
    {
        ChannelId = channelId;
        Name = name;
        UploadsPlaylistId = uploadsPlaylistId;
        UpdatedAt = updatedAt;
        LastUploadsCutoff = lastUploadsCutoff;
    }
}
