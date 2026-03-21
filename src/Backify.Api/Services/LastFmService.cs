using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backify.Api.Configuration;
using Backify.Api.Models;

namespace Backify.Api.Services;

public class LastFmService(HttpClient http, AppConfig config)
{
    private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";

    private string Sign(Dictionary<string, string> parameters)
    {
        var sorted = parameters.OrderBy(p => p.Key)
            .Select(p => $"{p.Key}{p.Value}")
            .Aggregate(string.Concat);
        var toHash = sorted + config.LastFm.SharedSecret;
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(toHash))).ToLower();
    }

    public async Task<(string SessionKey, string Username)> GetSessionAsync(string token)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = "auth.getSession",
            ["api_key"] = config.LastFm.ApiKey,
            ["token"] = token,
        };
        parameters["api_sig"] = Sign(parameters);
        parameters["format"] = "json";

        var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var response = await http.GetAsync($"{BaseUrl}?{query}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(json);

        if (doc.RootElement.TryGetProperty("error", out _))
        {
            var msg = doc.RootElement.GetProperty("message").GetString() ?? "Last.fm error";
            throw new Exception(msg);
        }

        var session = doc.RootElement.GetProperty("session");
        return (session.GetProperty("key").GetString()!, session.GetProperty("name").GetString()!);
    }

    public async Task<List<PreviewItem>> GetTopTracksAsync(string username, int n, string period)
    {
        var tracks = new List<PreviewItem>();
        int page = 1;

        while (tracks.Count < n)
        {
            int remaining = n - tracks.Count;
            int currentPageSize = Math.Min(remaining, 1000);

            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopTracks",
                ["user"] = username,
                ["api_key"] = config.LastFm.ApiKey,
                ["limit"] = currentPageSize.ToString(),
                ["period"] = period,
                ["page"] = page.ToString(),
                ["format"] = "json",
            };

            var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            var response = await http.GetAsync($"{BaseUrl}?{query}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(json);

            if (doc.RootElement.TryGetProperty("error", out _))
                throw new Exception(doc.RootElement.GetProperty("message").GetString() ?? "Last.fm error");

            var items = doc.RootElement.GetProperty("toptracks").GetProperty("track").EnumerateArray().ToList();
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                if (tracks.Count >= n) break;
                tracks.Add(new PreviewItem
                {
                    Rank = tracks.Count + 1,
                    Name = item.GetProperty("name").GetString() ?? "",
                    Artist = item.GetProperty("artist").GetProperty("name").GetString() ?? "",
                    PlayCount = int.TryParse(item.GetProperty("playcount").GetString(), out var pc) ? pc : 0,
                });
            }

            if (items.Count < currentPageSize) break;
            page++;
        }

        return tracks;
    }

    public async Task<List<PreviewItem>> GetTopAlbumsAsync(string username, int n, string period)
    {
        var albums = new List<PreviewItem>();
        int page = 1;

        while (albums.Count < n)
        {
            int remaining = n - albums.Count;
            int currentPageSize = Math.Min(remaining, 1000);

            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopAlbums",
                ["user"] = username,
                ["api_key"] = config.LastFm.ApiKey,
                ["limit"] = currentPageSize.ToString(),
                ["period"] = period,
                ["page"] = page.ToString(),
                ["format"] = "json",
            };

            var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            var response = await http.GetAsync($"{BaseUrl}?{query}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(json);

            if (doc.RootElement.TryGetProperty("error", out _))
                throw new Exception(doc.RootElement.GetProperty("message").GetString() ?? "Last.fm error");

            var items = doc.RootElement.GetProperty("topalbums").GetProperty("album").EnumerateArray().ToList();
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                if (albums.Count >= n) break;
                albums.Add(new PreviewItem
                {
                    Rank = albums.Count + 1,
                    Name = item.GetProperty("name").GetString() ?? "",
                    Artist = item.GetProperty("artist").GetProperty("name").GetString() ?? "",
                    PlayCount = int.TryParse(item.GetProperty("playcount").GetString(), out var pc) ? pc : 0,
                });
            }

            if (items.Count < currentPageSize) break;
            page++;
        }

        return albums;
    }

    public async Task<List<PreviewItem>> GetTopArtistsAsync(string username, int n, string period)
    {
        var artists = new List<PreviewItem>();
        int page = 1;

        while (artists.Count < n)
        {
            int remaining = n - artists.Count;
            int currentPageSize = Math.Min(remaining, 1000);

            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopArtists",
                ["user"] = username,
                ["api_key"] = config.LastFm.ApiKey,
                ["limit"] = currentPageSize.ToString(),
                ["period"] = period,
                ["page"] = page.ToString(),
                ["format"] = "json",
            };

            var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            var response = await http.GetAsync($"{BaseUrl}?{query}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(json);

            if (doc.RootElement.TryGetProperty("error", out _))
                throw new Exception(doc.RootElement.GetProperty("message").GetString() ?? "Last.fm error");

            var items = doc.RootElement.GetProperty("topartists").GetProperty("artist").EnumerateArray().ToList();
            if (items.Count == 0) break;

            foreach (var item in items)
            {
                if (artists.Count >= n) break;
                artists.Add(new PreviewItem
                {
                    Rank = artists.Count + 1,
                    Name = item.GetProperty("name").GetString() ?? "",
                    Artist = "",
                    PlayCount = int.TryParse(item.GetProperty("playcount").GetString(), out var pc) ? pc : 0,
                });
            }

            if (items.Count < currentPageSize) break;
            page++;
        }

        return artists;
    }
}
