# Backify

Connect your Last.fm music history with Spotify. Like your most-played songs and save your most-listened albums — all at once.

## Stack

- **Backend**: ASP.NET Core 9 Web API (C#)
- **Frontend**: Blazor WebAssembly (C#)
- **Session**: Distributed session (in-memory for dev, Redis for prod)

## Quick Start

### 1. Get API Keys

**Last.fm:**
1. Go to https://www.last.fm/api/account/create
2. Note your **API Key** and **Shared Secret**

**Spotify:**
1. Go to https://developer.spotify.com/dashboard
2. Create an app
3. Add `http://localhost:5000/auth/spotify/callback` to Redirect URIs
4. Note your **Client ID** and **Client Secret**

### 2. Configure the API

Edit `src/Backify.Api/appsettings.Development.json`:
```json
{
  "App": {
    "LastFm": {
      "ApiKey": "YOUR_LASTFM_API_KEY",
      "SharedSecret": "YOUR_LASTFM_SHARED_SECRET"
    },
    "Spotify": {
      "ClientId": "YOUR_SPOTIFY_CLIENT_ID",
      "ClientSecret": "YOUR_SPOTIFY_CLIENT_SECRET"
    }
  }
}
```

### 3. Run

```bash
# Terminal 1 – Backend (runs on http://localhost:5000)
cd src/Backify.Api
dotnet run

# Terminal 2 – Frontend (runs on http://localhost:5173)
cd src/Backify.Web
dotnet run
```

Open http://localhost:5173

## Deployment

### Railway (Backend)
1. Connect your GitHub repo to Railway
2. Set root directory to `src/Backify.Api`
3. Add environment variables:
   - `App__LastFm__ApiKey`
   - `App__LastFm__SharedSecret`
   - `App__LastFm__CallbackUri` = `https://api.yourdomain.com/auth/lastfm/callback`
   - `App__Spotify__ClientId`
   - `App__Spotify__ClientSecret`
   - `App__Spotify__RedirectUri` = `https://api.yourdomain.com/auth/spotify/callback`
   - `App__FrontendOrigin` = `https://yourdomain.com`
   - `ConnectionStrings__Redis` = (from Railway Redis addon)

### Vercel / Netlify (Frontend)
1. Build: `dotnet publish src/Backify.Web -c Release -o publish`
2. Deploy the `publish/wwwroot` folder
3. Set env var `ApiBaseUrl` = `https://api.yourdomain.com`
4. Add SPA redirect rule: all routes → `index.html`

## Architecture

```
Browser (Blazor WASM)
    │
    ├─ GET /session           → Check auth state
    ├─ GET /auth/lastfm       → Redirect to Last.fm auth
    ├─ GET /auth/spotify      → Redirect to Spotify auth (PKCE)
    ├─ GET /tracks/preview    → Fetch Last.fm top tracks + search Spotify
    ├─ GET /tracks/apply/stream → SSE: like tracks in batches of 50
    ├─ GET /albums/preview    → Fetch Last.fm top albums + search Spotify
    └─ GET /albums/apply/stream → SSE: save albums in batches of 20

ASP.NET Core API
    ├─ LastFmService     (MD5-signed API calls)
    ├─ SpotifyService    (PKCE OAuth + token refresh + search + batch ops)
    ├─ TracksOrchestrator (parallel search with SemaphoreSlim concurrency=3)
    └─ AlbumsOrchestrator
```
