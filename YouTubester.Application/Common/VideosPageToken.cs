using System.Globalization;
using System.Text;

namespace YouTubester.Application.Common;

/// <summary>
/// Utility for encoding and decoding cursor tokens for video pagination.
/// </summary>
public static class VideosPageToken
{
    /// <summary>
    /// Encodes a cursor into a URL-safe Base64 token.
    /// </summary>
    /// <param name="publishedAtUtc">Published date in UTC.</param>
    /// <param name="videoId">Video ID for tie-breaking.</param>
    /// <returns>URL-safe Base64 encoded token.</returns>
    public static string Serialize(DateTimeOffset publishedAtUtc, string videoId)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        
        var payload = $"{publishedAtUtc:O}|{videoId}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Attempts to decode a cursor token.
    /// </summary>
    /// <param name="token">URL-safe Base64 encoded token.</param>
    /// <param name="publishedAtUtc">Decoded published date in UTC.</param>
    /// <param name="videoId">Decoded video ID.</param>
    /// <returns>True if decoding was successful; otherwise false.</returns>
    public static bool TryParse(string? token, out DateTimeOffset publishedAtUtc, out string videoId)
    {
        publishedAtUtc = default;
        videoId = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            // Restore Base64 padding and convert from URL-safe
            var base64 = token.Replace('-', '+').Replace('_', '/');
            var paddingLength = 4 - (base64.Length % 4);
            if (paddingLength != 4)
            {
                base64 += new string('=', paddingLength);
            }

            var bytes = Convert.FromBase64String(base64);
            var payload = Encoding.UTF8.GetString(bytes);

            var parts = payload.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out publishedAtUtc))
            {
                return false;
            }

            videoId = parts[1];
            return !string.IsNullOrEmpty(videoId);
        }
        catch
        {
            return false;
        }
    }
}