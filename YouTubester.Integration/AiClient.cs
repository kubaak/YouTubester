using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace YouTubester.Integration;

public sealed class AiClient(HttpClient http, IOptions<AiOptions> ai) : IAiClient
{
    private readonly AiOptions _ai = ai.Value;

    public async Task<(string? Title, string? Description)> SuggestMetadataAsync(string currentTitle, string currentDescription, IEnumerable<string> tags, CancellationToken ct = default)
    {
        var prompt = $$"""
                       You write concise YouTube metadata. Return JSON only (no code fences).
                       Generate:
                       - title (≤80 chars, no ALL CAPS, punchy)
                       - description (exactly 3 lines with emojis):
                         📍 Event: <event + year if known>
                         🎯 Location: <city/country>
                         💥 Style: <styles/elements>

                       Context:
                       Current title: {{currentTitle}}
                       Current description: {{currentDescription}}
                       Tags: {{string.Join(", ", tags ?? Array.Empty<string>())}}

                       Return: {"title":"...","description":"..."}
                       """;

        var body = new {
            model = _ai.Model,
            prompt,
            stream = false,
            format = "json",
            options = new { temperature = 0.7, num_ctx = 4096 }
        };

        var res = await http.PostAsJsonAsync("/api/generate", body, ct);
        res.EnsureSuccessStatusCode();
        using var env = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var content = env.RootElement.GetProperty("response").GetString() ?? "{}";
        using var doc = JsonDocument.Parse(content);
        var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
        var desc  = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
        return (title, desc);
    }

    public async Task<string?> SuggestReplyAsync(string videoTitle, IEnumerable<string> tags, string commentText, CancellationToken ct = default)
    {
        var prompt = $$"""

                       System: You are the channel owner. Be brief, kind, and helpful. Return JSON only.
                       User:
                       Write a reply ≤ 2 sentences. If hostile, defuse politely.
                       If unsure about a fact, say you're not sure.
                       If this comment has no letters or digits (emoji-only), a short emoji reply is OK.
                       Video title: "{{videoTitle}}"
                       Tags: {{string.Join(", ", tags)}}
                       Comment: "{{commentText}}"
                       Return: {"reply":"..."}
                       """;

        var body = new { model = _ai.Model, prompt, stream = false, format = "json", options = new { temperature = 0.7, num_ctx = 4096 } };
        var res = await http.PostAsJsonAsync("/api/generate", body, ct);
        res.EnsureSuccessStatusCode();
        using var env = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var content = env.RootElement.GetProperty("response").GetString() ?? "{}";
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.TryGetProperty("reply", out var r) ? r.GetString() : null;
    }
}