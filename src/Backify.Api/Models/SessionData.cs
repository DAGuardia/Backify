namespace Backify.Api.Models;

public class LastFmSession
{
    public string SessionKey { get; set; } = "";
    public string Username { get; set; } = "";
}

public class SpotifySession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public long ExpiresAt { get; set; } // Unix ms
}

public class SpotifyOAuthTemp
{
    public string CodeVerifier { get; set; } = "";
    public string State { get; set; } = "";
}

public class SessionInfoResponse
{
    public bool LastFmConnected { get; set; }
    public bool SpotifyConnected { get; set; }
    public string? LastFmUsername { get; set; }
}
