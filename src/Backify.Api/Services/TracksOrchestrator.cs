using Backify.Api.Models;

namespace Backify.Api.Services;

public class TracksOrchestrator(LastFmService lastFm, SpotifyService spotify)
{
    public async Task<List<PreviewItem>> PreviewAsync(LastFmSession lfm, SpotifySession sp, int n, string period)
    {
        var tracks = await lastFm.GetTopTracksAsync(lfm.Username, n, period);

        var semaphore = new SemaphoreSlim(3); // max 3 concurrent Spotify searches
        var tasks = tracks.Select(async track =>
        {
            await semaphore.WaitAsync();
            try
            {
                var id = await spotify.SearchTrackAsync(sp, track.Name, track.Artist);
                track.SpotifyId = id;
                track.Found = id != null;
                return track;
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}
