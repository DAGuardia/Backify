using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Backify.Api.Models;
using Backify.Api.Services;

namespace Backify.Api.Controllers;

[ApiController]
[Route("artists")]
public class ArtistsController(ArtistsOrchestrator orchestrator, SpotifyService spotifyService) : ControllerBase
{
    private static readonly HashSet<string> ValidPeriods = ["overall", "7day", "1month", "3month", "6month", "12month"];

    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] int n = 50, [FromQuery] string period = "overall")
    {
        if (!GetSessions(out var lfm, out _))
            return Unauthorized(new { error = "Not authenticated" });
        if (n < 1 || n > 500 || !ValidPeriods.Contains(period))
            return BadRequest(new { error = "Invalid parameters" });
        return Ok(await orchestrator.GetListAsync(lfm!, n, period));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int n = 50, [FromQuery] string period = "overall")
    {
        if (!GetSessions(out var lfm, out var sp))
            return Unauthorized(new { error = "Not authenticated" });

        if (n < 1 || n > 500 || !ValidPeriods.Contains(period))
            return BadRequest(new { error = "Invalid parameters" });

        var response = await orchestrator.PreviewAsync(lfm!, sp!, n, period);
        return Ok(response);
    }

    [HttpPost("search-items")]
    public async Task<IActionResult> SearchItems([FromBody] List<PreviewItem> items)
    {
        if (!GetSessions(out _, out var sp))
            return Unauthorized(new { error = "Not authenticated" });

        if (items == null || items.Count == 0)
            return BadRequest(new { error = "No items" });

        var response = await orchestrator.SearchItemsAsync(sp!, items);
        return Ok(response);
    }

    [HttpPost("search-stream")]
    public async Task SearchStream([FromBody] List<PreviewItem> items)
    {
        if (!GetSessions(out _, out var sp)) { Response.StatusCode = 401; return; }
        if (items == null || items.Count == 0) { Response.StatusCode = 400; return; }
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        await foreach (var evt in orchestrator.SearchStreamAsync(sp!, items))
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(evt)}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpPost("apply/stream")]
    public async Task ApplyStream([FromBody] List<string> ids)
    {
        if (!GetSessions(out _, out var sp))
        {
            Response.StatusCode = 401;
            return;
        }

        var artistIds = ids ?? [];
        if (artistIds.Count == 0)
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var batches = artistIds.Chunk(50).ToList();
        int done = 0;
        int total = artistIds.Count;

        try
        {
            foreach (var batch in batches)
            {
                await spotifyService.FollowArtistsAsync(sp!, batch);
                done += batch.Length;

                var evt = JsonSerializer.Serialize(new ProgressEvent { Done = done, Total = total, Complete = done == total });
                await Response.WriteAsync($"data: {evt}\n\n");
                await Response.Body.FlushAsync();

                if (done < total) await Task.Delay(1000);
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
