using System.Text.Json;

namespace Backify.Api.Services;

public class SpotifyTelemetryService
{
    private readonly string _filePath;
    private readonly Lock _lock = new();
    private int _requestCount = 0;

    public SpotifyTelemetryService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "spotify-telemetry.ndjson");
    }

    public void Record(SpotifyTelemetryEntry entry)
    {
        entry.RequestNumber = Interlocked.Increment(ref _requestCount);
        var line = JsonSerializer.Serialize(entry) + "\n";
        lock (_lock)
        {
            File.AppendAllText(_filePath, line);
        }
    }

    public int RequestCount => _requestCount;
}

public class SpotifyTelemetryEntry
{
    public int RequestNumber { get; set; }
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    public string Operation { get; set; } = "";   // search_track | search_album | search_artist | like_tracks | save_albums | follow_artists
    public string? Query { get; set; }
    public int StatusCode { get; set; }
    public bool RateLimited { get; set; }
    public int RetryAfterSeconds { get; set; }
    public bool Success { get; set; }
}
