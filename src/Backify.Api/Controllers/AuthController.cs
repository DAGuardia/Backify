using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Backify.Api.Configuration;
using Backify.Api.Models;
using Backify.Api.Services;

namespace Backify.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(LastFmService lastFm, SpotifyService spotify, AppConfig config) : ControllerBase
{
    // ── Last.fm ──────────────────────────────────────────────────────────────

    [HttpGet("lastfm")]
    public IActionResult LastFmLogin()
    {
        var cb = Uri.EscapeDataString(config.LastFm.CallbackUri);
        return Redirect($"https://www.last.fm/api/auth/?api_key={config.LastFm.ApiKey}&cb={cb}");
    }

    [HttpGet("lastfm/callback")]
    public async Task<IActionResult> LastFmCallback([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
            return Redirect($"{config.FrontendOrigin}/login?error=lastfm_failed");

        try
        {
            var (sessionKey, username) = await lastFm.GetSessionAsync(token);
            var lfmSession = new LastFmSession { SessionKey = sessionKey, Username = username };
            HttpContext.Session.SetString("lastfm", JsonSerializer.Serialize(lfmSession));
            await HttpContext.Session.CommitAsync();
            return Redirect($"{config.FrontendOrigin}/dashboard");
        }
        catch
        {
            return Redirect($"{config.FrontendOrigin}/login?error=lastfm_failed");
        }
    }

    // ── Spotify ───────────────────────────────────────────────────────────────

    [HttpGet("spotify")]
    public IActionResult SpotifyLogin()
    {
        var verifier = SpotifyService.GenerateCodeVerifier();
        var challenge = SpotifyService.GenerateCodeChallenge(verifier);
        var state = SpotifyService.GenerateState();

        var temp = new SpotifyOAuthTemp { CodeVerifier = verifier, State = state };
        HttpContext.Session.SetString("spotify_oauth", JsonSerializer.Serialize(temp));

        var url = spotify.GetAuthorizationUrl(challenge, state);
        return Redirect(url);
    }

    [HttpGet("spotify/callback")]
    public async Task<IActionResult> SpotifyCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
            return Redirect($"{config.FrontendOrigin}/login?error=spotify_denied");

        var tempJson = HttpContext.Session.GetString("spotify_oauth");
        if (tempJson == null)
            return Redirect($"{config.FrontendOrigin}/login?error=spotify_failed");

        var temp = JsonSerializer.Deserialize<SpotifyOAuthTemp>(tempJson)!;
        if (temp.State != state)
            return BadRequest("Invalid state");

        try
        {
            var spSession = await spotify.ExchangeCodeAsync(code!, temp.CodeVerifier);
            HttpContext.Session.Remove("spotify_oauth");
            HttpContext.Session.SetString("spotify", JsonSerializer.Serialize(spSession));
            await HttpContext.Session.CommitAsync();
            return Redirect($"{config.FrontendOrigin}/dashboard");
        }
        catch
        {
            return Redirect($"{config.FrontendOrigin}/login?error=spotify_failed");
        }
    }
}
