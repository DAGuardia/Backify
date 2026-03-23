using Microsoft.AspNetCore.Mvc;
using Backify.Api.Services;

namespace Backify.Api.Controllers;

[ApiController]
[Route("telemetry")]
public class TelemetryController(SpotifyTelemetryService telemetry, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var path = Path.Combine(env.ContentRootPath, "spotify-telemetry.ndjson");
        if (!System.IO.File.Exists(path))
            return Ok(new { totalRequests = 0, entries = Array.Empty<object>() });

        var lines = System.IO.File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return Ok(new
        {
            totalRequests = telemetry.RequestCount,
            entriesOnDisk = lines.Count,
            entries = lines
        });
    }

    [HttpDelete]
    public IActionResult Clear()
    {
        var path = Path.Combine(env.ContentRootPath, "spotify-telemetry.ndjson");
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
        return Ok(new { cleared = true });
    }
}
