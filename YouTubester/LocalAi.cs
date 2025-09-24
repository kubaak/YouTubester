namespace YouTubester;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

public static class LocalAi
{
    public static async Task<(string? Title, string? Description)> SuggestMetadataAsync(
        string endpoint, string model,
        string currentTitle, string relatedVideoDescription)
    {
        using var http = new HttpClient { BaseAddress = new Uri(endpoint) };

        var prompt = $@"
You are a YouTube metadata assistant for breaking/bboy videos.
Generate a title and a description.

Keep it concise. Use emojis. Don't use hashtags.
Current title: {currentTitle}
Related video description (context only): {relatedVideoDescription}

Return JSON only: {{""title"":""..."",""description"":""...""}}";

        var body = new
        {
            model,
            prompt,
            stream = false,
            options = new { temperature = 0.7, num_ctx = 4096 }
        };

        var resp = await http.PostAsJsonAsync("/api/generate", body);
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadAsStringAsync();

        // Ollama returns { ..., "response": "<the model text>" }
        var jsonObj = ExtractJsonFromOllama(raw);
        // if (jsonObj is null) return null;
        //
        jsonObj.RootElement.TryGetProperty("response", out var r);

        // Ollama returns {"model":"...","created_at":"...","response":"..."}
        // Extract JSON object from 'response'
        var jsonFromModel = ExtractBetween(r.GetString(), "{", "}");
        if (jsonFromModel is null) return (null, null);

        using var doc = JsonDocument.Parse("{" + jsonFromModel + "}");
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
        var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
        return (title, description);
    }
    
    private static JsonDocument? ExtractJsonFromOllama(string raw)
    {
        // Find the biggest {...} block in raw and parse it.
        var start = raw.IndexOf('{'); 
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var slice = raw.Substring(start, end - start + 1);
        try { return JsonDocument.Parse(slice); } catch { return null; }
    }
    
    private static string? ExtractBetween(string s, string open, string close)
    {
        var start = s.IndexOf(open);
        var end = s.LastIndexOf(close);
        if (start < 0 || end <= start) return null;
        return s.Substring(start + 1, end - start - 1); // without braces
    }
    
    public static async Task<string?> SuggestReplyAsync(
        string endpoint, string model, string videoTitle, IEnumerable<string> tags, string commentText, bool isShort)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(endpoint);
        var shortEncourage = isShort ? "If natural, add a tiny CTA “check the related video”. " : "";
        
        var prompt = $$"""

                       System: You are the channel owner. A 30 years old bboy. Be brief, kind, and helpful. Return JSON only.
                       User:
                       Write a reply in ≤ 2 sentences. If it's hostile, then reply with a single emoji. 
                       If unsure about a fact, say you’re not sure.
                       Do not use first-person plural ('we', 'our', 'us'). Write in impersonal or neutral tone.
                       Don't use semicolons. 
                       Don't give props for reacting e.g. don't say anything like 'that is a great observation' or 'that is a solid reaction' or anything like that
                       {{shortEncourage}}
                       Video title: "{{videoTitle}}"
                       Tags: {{string.Join(", ", tags ?? Array.Empty<string>())}}
                       Comment: "{{commentText}}"
                       Return JSON ONLY: {"reply":"..."}
                       """;

        var body = new { model, prompt, stream = false, format = "json", options = new { temperature = 0.7, num_ctx = 4096 } };
        var resp = await http.PostAsJsonAsync("/api/generate", body);
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadAsStringAsync();
        
        using var env = JsonDocument.Parse(raw);
        var content = env.RootElement.GetProperty("response").GetString();

        using var doc = JsonDocument.Parse(content!);
        var reply = doc.RootElement.GetProperty("reply").GetString();
        return reply;
    }
}
