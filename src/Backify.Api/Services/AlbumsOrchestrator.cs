using Backify.Api.Models;

namespace Backify.Api.Services;

public class AlbumsOrchestrator(LastFmService lastFm, SpotifyService spotify)
{
    public async Task<PreviewResponse> PreviewAsync(LastFmSession lfm, SpotifySession sp, int n, string period)
    {
        var albums = await lastFm.GetTopAlbumsAsync(lfm.Username, n, period);
        return await SearchItemsAsync(sp, albums);
    }

    public async Task<PreviewResponse> SearchItemsAsync(SpotifySession sp, List<PreviewItem> items)
    {
        var cts = new CancellationTokenSource();
        int rateLimitSeconds = 0;
        var semaphore = new SemaphoreSlim(3);

        var tasks = items.Select(async item =>
        {
            bool acquired = false;
            try
            {
                await semaphore.WaitAsync(cts.Token);
                acquired = true;
            }
            catch (OperationCanceledException)
            {
                return item;
            }
            try
            {
                var (id, rl) = await spotify.SearchAlbumAsync(sp, item.Name, item.Artist);
                if (rl > 0)
                {
                    Interlocked.CompareExchange(ref rateLimitSeconds, rl, 0);
                    cts.Cancel();
                }
                item.SpotifyId = rl == 0 ? id : null;
                item.Found = rl == 0 && id != null;
                return item;
            }
            finally
            {
                if (acquired) semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return new PreviewResponse { Items = results.ToList(), RateLimitSeconds = rateLimitSeconds };
    }
}
