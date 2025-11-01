namespace YouTubester.Domain;

public sealed class Channel
{
    public string ChannelId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string UploadsPlaylistId { get; private set; } = null!;
    public string? ETag { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastUploadsCutoff { get; private set; }

    public static Channel Create(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt,
        DateTimeOffset? lastUploadsCutoff = null, string? eTag = null)
    {
        return new Channel(channelId, name, uploadsPlaylistId, updatedAt, lastUploadsCutoff, eTag);
    }

    /// <summary>Advance the uploads cutoff if the candidate is newer. Returns true if updated.</summary>
    public bool AdvanceUploadsCutoff(DateTimeOffset candidateCutoffUtc, DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        if (LastUploadsCutoff.HasValue && candidateCutoffUtc <= LastUploadsCutoff.Value)
        {
            return false;
        }

        LastUploadsCutoff = candidateCutoffUtc;
        UpdatedAt = nowUtc;
        return true;
    }

    public bool ApplyRemoteSnapshot(string name, string uploadsPlaylistId, string? eTag, DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);
        var dirty = false;

        if (!StringComparer.Ordinal.Equals(Name, name))
        {
            Name = RequireNonEmpty(name, nameof(name));
            dirty = true;
        }

        if (!StringComparer.Ordinal.Equals(UploadsPlaylistId, uploadsPlaylistId))
        {
            UploadsPlaylistId = RequireId(uploadsPlaylistId);
            dirty = true;
        }

        if (!StringComparer.Ordinal.Equals(ETag, eTag))
        {
            ETag = eTag;
            dirty = true;
        }

        if (dirty)
        {
            UpdatedAt = nowUtc;
        }

        return dirty;
    }

    private static string RequireId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Id must be a non-empty string.", nameof(value));
        }

        return value;
    }

    private static string RequireNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{paramName}' must be a non-empty string.", paramName);
        }

        return value;
    }

    private static void RequireUtc(DateTimeOffset ts)
    {
        if (ts.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be in UTC.", nameof(ts));
        }
    }

    private Channel(string channelId, string name, string uploadsPlaylistId, DateTimeOffset updatedAt,
        DateTimeOffset? lastUploadsCutoff, string? eTag)
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