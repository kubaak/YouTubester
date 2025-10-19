namespace YouTubester.Integration;

public sealed class AiOptions
{
    public bool Enable { get; set; } = true;
    public string Provider { get; set; } = "ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "gemma3:12b";
    public bool ReplaceTitle { get; set; } = false;
    public bool ApplyDescription { get; set; } = true;
    public bool ReplyToComments { get; set; } = true;
    public int MaxRepliesPerRun { get; set; } = 100;
}