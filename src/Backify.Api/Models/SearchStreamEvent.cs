namespace Backify.Api.Models;

public class SearchStreamEvent
{
    public string Type { get; init; } = "";   // "searching" | "result" | "ratelimit" | "error" | "done"
    public PreviewItem? Item { get; init; }
    public int WaitSeconds { get; init; }
    public string? ErrorMessage { get; init; }
}
