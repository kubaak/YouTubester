namespace YouTubester.Domain;

public sealed class Channel
{
    public string Name { get; set; } = default!;
    public string ChannelId { get; set; } = default!;
    public string UploadsPlaylistId { get; set; } = default!;
    public string? ETag { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastUploadsCutoff { get; set; }

    public static Channel Create(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt, DateTimeOffset? lastUploadsCutoff = null, string? eTag = null)
    {
        return new Channel(channelId, name, uploadsPlaylistId, updatedAt, lastUploadsCutoff, eTag);
    }

    private Channel(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt, DateTimeOffset? lastUploadsCutoff, string? eTag)
    {
        ChannelId = channelId;
        Name = name;
        UploadsPlaylistId = uploadsPlaylistId;
        ETag = eTag;
        UpdatedAt = updatedAt;
        LastUploadsCutoff = lastUploadsCutoff;
    }

    private Channel()
    {
    }
}
