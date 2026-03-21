using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Backify.Api.Models;

namespace Backify.Api.Controllers;

[ApiController]
public class SessionController : ControllerBase
{
    [HttpGet("session")]
    public IActionResult GetSession()
    {
        var lfmJson = HttpContext.Session.GetString("lastfm");
        var spJson = HttpContext.Session.GetString("spotify");

        LastFmSession? lfm = lfmJson != null ? JsonSerializer.Deserialize<LastFmSession>(lfmJson) : null;
        SpotifySession? sp = spJson != null ? JsonSerializer.Deserialize<SpotifySession>(spJson) : null;

        return Ok(new SessionInfoResponse
        {
            LastFmConnected = lfm != null && !string.IsNullOrEmpty(lfm.SessionKey),
            SpotifyConnected = sp != null && !string.IsNullOrEmpty(sp.AccessToken),
            LastFmUsername = lfm?.Username,
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { ok = true });
    }
}
