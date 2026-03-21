namespace Backify.Api.Models;

public class ApplyRequest
{
    public List<string> Ids { get; set; } = new();
}

public class ProgressEvent
{
    public int Done { get; set; }
    public int Total { get; set; }
    public bool Complete { get; set; }
    public string? Error { get; set; }
    public bool RateLimit { get; set; }
    public int WaitSeconds { get; set; }
}
