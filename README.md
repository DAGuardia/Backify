# Backify

**For those with a musical past before Spotify.**

Backify connects your Last.fm listening history with Spotify. It fetches your most-played tracks, albums, and artists, finds them on Spotify, and lets you like songs, save albums, and follow artists in one go — helping Spotify's algorithm finally know you.

---

## ⚠️ Spotify API Limitation

Thanks to Spotify's **brilliant** decision to require 250,000 monthly active users and proof of revenue before granting extended API access — and their equally **inspired** move to exclude individual developers entirely as of May 2025 — this app is stuck in **development mode**.

In development mode, Spotify enforces aggressive rate limits. Running the app from a shared server means all users share the same quota, which gets exhausted quickly. **The recommended way to use Backify is to run it locally with your own API keys**, where you control your own quota.

The hosted version at [backify-app.azurewebsites.net](https://backify-app.azurewebsites.net) exists but is limited to 25 allowlisted accounts and may hit rate limits during heavy use.

---

## How it works

1. Connect your Last.fm and Spotify accounts via OAuth
2. Choose a time period and how many top tracks / albums / artists to fetch
3. Preview what was found on Spotify — deselect anything you don't want
4. Sync — tracks are liked, albums saved, and artists followed in batches, with live progress
5. Each item's search status updates in real time as results stream in
6. If the Spotify rate limit is hit during search, a countdown appears — it resumes automatically or you can cancel and apply what was already found
7. Results are cached locally in your browser — future sessions skip already-matched items and only search what's new
8. The full Last.fm list is also cached, so your previous results appear immediately on page load without needing to fetch again

---

## Tips for running with large libraries

Spotify's development mode rate limits are strict and penalties are progressive — the more you hit the limit, the longer the cooldown. To avoid issues:

- Run **one card at a time** (tracks → albums → artists), not all three simultaneously
- Keep the browser tab active during sync — don't minimize or switch away
- If you get rate limited during search, you can cancel the countdown and apply the items already found — then resume searching the rest in a later session
- The **Start from rank** field on the tracks card lets you resume a partial session from a specific position, skipping tracks already processed
- Results are cached after each session, so you can safely spread the work across multiple days
- A telemetry log is written to `src/Backify.Api/spotify-telemetry.ndjson` — useful for diagnosing rate limit patterns. Check it at `GET /telemetry` or clear it with `DELETE /telemetry`

---

## Stack

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 9 Web API (C#) |
| Frontend | Blazor WebAssembly (C#) |
| Auth | Last.fm OAuth + Spotify PKCE |
| Session | In-memory (dev) / Redis (prod) |
| Deployment | Azure App Service F1 via GitHub Actions |

---

## Running locally

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- A Last.fm API account
- A Spotify Developer app

### 1. Get API keys

**Last.fm**
1. Go to [last.fm/api/account/create](https://www.last.fm/api/account/create)
2. Set callback URL to `http://127.0.0.1:5000/auth/lastfm/callback`
3. Note your **API Key** and **Shared Secret**

**Spotify**
1. Go to [developer.spotify.com/dashboard](https://developer.spotify.com/dashboard)
2. Create an app, select *Web API*
3. Add `http://127.0.0.1:5000/auth/spotify/callback` to Redirect URIs
4. Under *Users and Access*, add the Spotify accounts that will use the app (development mode only allows the app owner and explicitly added users)
5. Note your **Client ID** and **Client Secret**

### 2. Configure

Create `src/Backify.Api/appsettings.Development.json`:

```json
{
  "App": {
    "FrontendOrigin": "http://127.0.0.1:5173",
    "LastFm": {
      "ApiKey": "YOUR_LASTFM_API_KEY",
      "SharedSecret": "YOUR_LASTFM_SHARED_SECRET",
      "CallbackUri": "http://127.0.0.1:5000/auth/lastfm/callback"
    },
    "Spotify": {
      "ClientId": "YOUR_SPOTIFY_CLIENT_ID",
      "ClientSecret": "YOUR_SPOTIFY_CLIENT_SECRET",
      "RedirectUri": "http://127.0.0.1:5000/auth/spotify/callback"
    }
  }
}
```

Create `src/Backify.Web/wwwroot/appsettings.Development.json`:

```json
{
  "ApiBaseUrl": "http://127.0.0.1:5000/"
}
```

### 3. Run

```powershell
# Terminal 1 — API (port 5000)
cd src/Backify.Api; $env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run

# Terminal 2 — Frontend (port 5173)
cd src/Backify.Web; dotnet run --urls "http://127.0.0.1:5173"
```

Open [http://127.0.0.1:5173](http://127.0.0.1:5173)

> Use `127.0.0.1` — not `localhost`. Both services must share the same hostname for session cookies to work correctly.

---

## Architecture

```
Browser (Blazor WASM)
    │
    ├─ GET    /session                  Check auth state
    ├─ GET    /auth/lastfm              Redirect to Last.fm OAuth
    ├─ GET    /auth/lastfm/callback     Exchange token → session
    ├─ GET    /auth/spotify             Redirect to Spotify PKCE
    ├─ GET    /auth/spotify/callback    Exchange code → tokens
    ├─ GET    /tracks/list              Top tracks from Last.fm
    ├─ POST   /tracks/search-stream     SSE: search tracks on Spotify one by one
    ├─ POST   /tracks/apply/stream      SSE: like tracks in batches of 40
    ├─ GET    /albums/list              Top albums from Last.fm
    ├─ POST   /albums/search-stream     SSE: search albums on Spotify one by one
    ├─ POST   /albums/apply/stream      SSE: save albums in batches of 40
    ├─ GET    /artists/list             Top artists from Last.fm
    ├─ POST   /artists/search-stream    SSE: search artists on Spotify one by one
    ├─ POST   /artists/apply/stream     SSE: follow artists in batches of 50
    ├─ GET    /telemetry                View Spotify API request log
    └─ DELETE /telemetry                Clear telemetry log

ASP.NET Core API
    ├─ LastFmService            MD5-signed API calls
    ├─ SpotifyService           PKCE OAuth, token refresh, search, batch saves/follows
    ├─ SpotifyTelemetryService  Appends every Spotify request to spotify-telemetry.ndjson
    ├─ TracksOrchestrator       Sequential search with 2s delay between items, 1s before fallback
    ├─ AlbumsOrchestrator       Sequential search with 2s delay between items, 1s before fallback
    └─ ArtistsOrchestrator      Sequential search with 2s delay between items, 1s before fallback
```

Tokens are stored in server-side session memory and never persisted. Sessions expire after 24 hours of inactivity. Search results and the Last.fm list are cached in browser localStorage — repeated sessions restore the previous state immediately and only search for uncached items.

---

## License

MIT © [Damián Guardia](https://github.com/DAGuardia)
