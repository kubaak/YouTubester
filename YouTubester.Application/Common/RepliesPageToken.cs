using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace YouTubester.Application.Common;

public static class RepliesPageToken
{
    public static string Serialize(DateTimeOffset pulledAtUtc, string commentId, string? binding)
    {
        ArgumentException.ThrowIfNullOrEmpty(commentId);

        // payload: ISO8601 | commentId | optional binding
        var core = $"{pulledAtUtc:O}|{commentId}";
        var payload = string.IsNullOrEmpty(binding) ? core : $"{core}|{binding}";
        var bytes = Encoding.UTF8.GetBytes(payload);

        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static bool TryParse(string? token, out DateTimeOffset pulledAtUtc, out string commentId,
        out string? binding)
    {
        pulledAtUtc = default;
        commentId = string.Empty;
        binding = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            // Decode Base64URL
            var bytes = WebEncoders.Base64UrlDecode(token);
            var payload = Encoding.UTF8.GetString(bytes);

            // limit to 3 parts so binding may contain '|'
            var parts = payload.Split('|', 3);
            if (parts.Length < 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(parts[0], "O",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out pulledAtUtc))
            {
                return false;
            }

            commentId = parts[1];
            if (string.IsNullOrEmpty(commentId))
            {
                return false;
            }

            if (parts.Length == 3)
            {
                binding = parts[2];
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}