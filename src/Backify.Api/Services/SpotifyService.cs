using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backify.Api.Configuration;
using Backify.Api.Models;

namespace Backify.Api.Services;

public class SpotifyService(HttpClient http, AppConfig config, IHttpContextAccessor httpContextAccessor)
{
    private const string AccountsUrl = "https://accounts.spotify.com";
    private const string ApiUrl = "https://api.spotify.com/v1";

    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string GenerateState()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower();
    }

    public string GetAuthorizationUrl(string codeChallenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = config.Spotify.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = config.Spotify.RedirectUri,
            ["scope"] = "user-library-modify user-read-private user-follow-modify playlist-modify-public",
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge,
            ["state"] = state,
        };
        var qs = string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{AccountsUrl}/authorize?{qs}";
    }

    public async Task<SpotifySession> ExchangeCodeAsync(string code, string codeVerifier)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = config.Spotify.RedirectUri,
            ["client_id"] = config.Spotify.ClientId,
            ["code_verifier"] = codeVerifier,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{AccountsUrl}/api/token") { Content = form };

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(json);

        return new SpotifySession
        {
            AccessToken = doc.RootElement.GetProperty("access_token").GetString()!,
            RefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + doc.RootElement.GetProperty("expires_in").GetInt32() * 1000L,
        };
    }

    private async Task<string> GetValidTokenAsync(SpotifySession session)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < session.ExpiresAt - 60_000)
            return session.AccessToken;

        // Refresh
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = session.RefreshToken,
            ["client_id"] = config.Spotify.ClientId,
        });
        var request = new HttpRequestMessage(HttpMethod.Post, $"{AccountsUrl}/api/token") { Content = form };

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(json);

        session.AccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
            session.RefreshToken = rt.GetString()!;
        session.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + doc.RootElement.GetProperty("expires_in").GetInt32() * 1000L;

        // Persist updated session
        var ctx = httpContextAccessor.HttpContext!;
        ctx.Session.SetString("spotify", JsonSerializer.Serialize(session));
        await ctx.Session.CommitAsync();

        return session.AccessToken;
    }

    public async Task<(string? Id, int RateLimitSeconds)> SearchTrackAsync(SpotifySession session, string track, string artist)
    {
        var token = await GetValidTokenAsync(session);

        var q1 = $"track:\"{track}\" artist:\"{artist}\"";
        var (id1, rl1) = await DoSearchAsync(token, q1, "track");
        if (id1 != null) return (id1, 0);
        if (rl1 > 0) return (null, rl1);

        var q2 = $"{track} {artist}";
        return await DoSearchAsync(token, q2, "track");
    }

    public async Task<(string? Id, int RateLimitSeconds)> SearchAlbumAsync(SpotifySession session, string album, string artist)
    {
        var token = await GetValidTokenAsync(session);

        var q1 = $"album:\"{album}\" artist:\"{artist}\"";
        var (id1, rl1) = await DoSearchAsync(token, q1, "album");
        if (id1 != null) return (id1, 0);
        if (rl1 > 0) return (null, rl1);

        var q2 = $"{album} {artist}";
        return await DoSearchAsync(token, q2, "album");
    }

    private async Task<(string? Id, int RateLimitSeconds)> DoSearchAsync(string token, string query, string type)
    {
        var qs = $"?q={Uri.EscapeDataString(query)}&type={type}&limit=1";
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}/search{qs}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var retryAfter = (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30);
            return (null, retryAfter);
        }

        if (!response.IsSuccessStatusCode) return (null, 0);

        var json = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(json);

        var pluralType = type switch { "track" => "tracks", "artist" => "artists", _ => "albums" };
        var items = doc.RootElement.GetProperty(pluralType).GetProperty("items").EnumerateArray().ToList();
        if (items.Count > 0)
            return (items[0].GetProperty("id").GetString(), 0);

        return (null, 0);
    }

    public async Task<(string? Id, int RateLimitSeconds)> SearchArtistAsync(SpotifySession session, string artist)
    {
        var token = await GetValidTokenAsync(session);

        var q1 = $"artist:\"{artist}\"";
        var (id1, rl1) = await DoSearchAsync(token, q1, "artist");
        if (id1 != null) return (id1, 0);
        if (rl1 > 0) return (null, rl1);

        return await DoSearchAsync(token, artist, "artist");
    }

    public async Task LikeTracksAsync(SpotifySession session, IEnumerable<string> trackIds)
    {
        var token = await GetValidTokenAsync(session);
        var uris = string.Join(",", trackIds.Select(id => Uri.EscapeDataString($"spotify:track:{id}")));
        var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiUrl}/me/library?uris={uris}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new SpotifyRateLimitException((int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveAlbumsAsync(SpotifySession session, IEnumerable<string> albumIds)
    {
        var token = await GetValidTokenAsync(session);
        var uris = string.Join(",", albumIds.Select(id => Uri.EscapeDataString($"spotify:album:{id}")));
        var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiUrl}/me/library?uris={uris}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new SpotifyRateLimitException((int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30));
        response.EnsureSuccessStatusCode();
    }

    public async Task FollowArtistsAsync(SpotifySession session, IEnumerable<string> artistIds)
    {
        var token = await GetValidTokenAsync(session);
        var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiUrl}/me/following?type=artist");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = System.Net.Http.Json.JsonContent.Create(new { ids = artistIds.ToArray() });
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new SpotifyRateLimitException((int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30));
        response.EnsureSuccessStatusCode();
    }
}

public class SpotifyRateLimitException(int retryAfterSeconds) : Exception("Rate limited by Spotify")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
