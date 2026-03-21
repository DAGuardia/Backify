using Backify.Api.Models;

namespace Backify.Api.Services;

public class AlbumsOrchestrator(LastFmService lastFm, SpotifyService spotify)
{
    public async Task<List<PreviewItem>> PreviewAsync(LastFmSession lfm, SpotifySession sp, int n, string period)
    {
        var albums = await lastFm.GetTopAlbumsAsync(lfm.Username, n, period);

        var semaphore = new SemaphoreSlim(3);
        var tasks = albums.Select(async album =>
        {
            await semaphore.WaitAsync();
            try
            {
                var id = await spotify.SearchAlbumAsync(sp, album.Name, album.Artist);
                album.SpotifyId = id;
                album.Found = id != null;
                return album;
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}
