using Backify.Api.Models;

namespace Backify.Api.Services;

public class TracksOrchestrator(LastFmService lastFm, SpotifyService spotify)
{
    public async Task<List<PreviewItem>> GetListAsync(LastFmSession lfm, int n, string period)
        => await lastFm.GetTopTracksAsync(lfm.Username, n, period);

    public async Task<PreviewResponse> PreviewAsync(LastFmSession lfm, SpotifySession sp, int n, string period)
    {
        var tracks = await lastFm.GetTopTracksAsync(lfm.Username, n, period);
        return await SearchItemsAsync(sp, tracks);
    }

    public async IAsyncEnumerable<SearchStreamEvent> SearchStreamAsync(SpotifySession sp, List<PreviewItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            yield return new SearchStreamEvent { Type = "searching", Item = item };

            var (id, rl) = await spotify.SearchTrackAsync(sp, item.Name, item.Artist);
            if (rl == -1) { yield return new SearchStreamEvent { Type = "error", ErrorMessage = "Spotify returned 403 Forbidden. Your session may be invalid — please log out and log back in." }; yield break; }
            if (rl > 0) { yield return new SearchStreamEvent { Type = "ratelimit", WaitSeconds = rl }; yield break; }

            item.SpotifyId = id;
            item.Found = id != null;
            yield return new SearchStreamEvent { Type = "result", Item = item };

            if (i < items.Count - 1) await Task.Delay(2000);
        }
        yield return new SearchStreamEvent { Type = "done" };
    }

    public async Task<PreviewResponse> SearchItemsAsync(SpotifySession sp, List<PreviewItem> items)
    {
        int rateLimitSeconds = 0;

        foreach (var item in items)
        {
            var (id, rl) = await spotify.SearchTrackAsync(sp, item.Name, item.Artist);
            if (rl > 0)
            {
                rateLimitSeconds = rl;
                break;
            }
            item.SpotifyId = id;
            item.Found = id != null;
            await Task.Delay(2000);
        }

        return new PreviewResponse { Items = items, RateLimitSeconds = rateLimitSeconds };
    }
}
