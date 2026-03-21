using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Backify.Api.Models;
using Backify.Api.Services;

namespace Backify.Api.Controllers;

[ApiController]
[Route("albums")]
public class AlbumsController(AlbumsOrchestrator orchestrator, SpotifyService spotifyService) : ControllerBase
{
    private static readonly HashSet<string> ValidPeriods = ["overall", "7day", "1month", "3month", "6month", "12month"];

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int n = 50, [FromQuery] string period = "overall")
    {
        if (!GetSessions(out var lfm, out var sp))
            return Unauthorized(new { error = "Not authenticated" });

        if (n < 1 || n > 500 || !ValidPeriods.Contains(period))
            return BadRequest(new { error = "Invalid parameters" });

        var results = await orchestrator.PreviewAsync(lfm!, sp!, n, period);
        return Ok(results);
    }

    [HttpGet("apply/stream")]
    public async Task ApplyStream([FromQuery] string ids)
    {
        if (!GetSessions(out _, out var sp))
        {
            Response.StatusCode = 401;
            return;
        }

        var albumIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (albumIds.Count == 0)
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var batches = albumIds.Chunk(40).ToList();
        int done = 0;
        int total = albumIds.Count;

        try
        {
            foreach (var batch in batches)
            {
                await spotifyService.SaveAlbumsAsync(sp!, batch);
                done += batch.Length;

                var evt = JsonSerializer.Serialize(new ProgressEvent { Done = done, Total = total, Complete = done == total });
                await Response.WriteAsync($"data: {evt}\n\n");
                await Response.Body.FlushAsync();

                if (done < total) await Task.Delay(350);
            }
        }
        catch (SpotifyRateLimitException rl)
        {
            var evt = JsonSerializer.Serialize(new ProgressEvent { Done = done, Total = total, RateLimit = true, WaitSeconds = rl.RetryAfterSeconds });
            await Response.WriteAsync($"data: {evt}\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            var evt = JsonSerializer.Serialize(new ProgressEvent { Done = done, Total = total, Error = ex.Message });
            await Response.WriteAsync($"data: {evt}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    private bool GetSessions(out LastFmSession? lfm, out SpotifySession? sp)
    {
        var lfmJson = HttpContext.Session.GetString("lastfm");
        var spJson = HttpContext.Session.GetString("spotify");
        lfm = lfmJson != null ? JsonSerializer.Deserialize<LastFmSession>(lfmJson) : null;
        sp = spJson != null ? JsonSerializer.Deserialize<SpotifySession>(spJson) : null;
        return lfm != null && sp != null;
    }
}
