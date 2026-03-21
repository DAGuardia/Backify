namespace Backify.Api.Configuration;

public class AppConfig
{
    public string FrontendOrigin { get; set; } = "http://localhost:5173";
    public string SessionSecret { get; set; } = "dev-secret-change-in-production!!";
    public LastFmConfig LastFm { get; set; } = new();
    public SpotifyConfig Spotify { get; set; } = new();
}

public class LastFmConfig
{
    public string ApiKey { get; set; } = "";
    public string SharedSecret { get; set; } = "";
    public string CallbackUri { get; set; } = "http://localhost:5000/auth/lastfm/callback";
}

public class SpotifyConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "http://localhost:5000/auth/spotify/callback";
}
