# Backify

**For those with a musical past before Spotify.**

Backify connects your Last.fm listening history with Spotify. It fetches your most-played tracks and albums, finds them on Spotify, and lets you like songs and save albums to your library in one go — helping Spotify's algorithm finally know you.

Live at: [backify-app.azurewebsites.net](https://backify-app.azurewebsites.net)

---

## How it works

1. Connect your Last.fm and Spotify accounts via OAuth
2. Choose a time period and how many top tracks/albums to fetch
3. Preview what was found on Spotify — deselect anything you don't want
4. Sync — tracks are liked and albums saved in batches, with live progress per item
5. If anything fails mid-sync, retry from where it left off

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
4. Note your **Client ID** and **Client Secret**

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

## Deploying to Azure

The repo includes a GitHub Actions workflow that deploys on every push to `main`.

### 1. Create Azure resources

```bash
az login
az group create --name backify-rg --location westeurope
az appservice plan create --name backify-plan --resource-group backify-rg --sku F1 --is-linux
az webapp create --name YOUR-APP-NAME --resource-group backify-rg --plan backify-plan --runtime "DOTNETCORE:9.0"
```

### 2. Set environment variables

```bash
az webapp config appsettings set --name YOUR-APP-NAME --resource-group backify-rg --settings \
  "App__FrontendOrigin=https://YOUR-APP-NAME.azurewebsites.net" \
  "App__LastFm__ApiKey=..." \
  "App__LastFm__SharedSecret=..." \
  "App__LastFm__CallbackUri=https://YOUR-APP-NAME.azurewebsites.net/auth/lastfm/callback" \
  "App__Spotify__ClientId=..." \
  "App__Spotify__ClientSecret=..." \
  "App__Spotify__RedirectUri=https://YOUR-APP-NAME.azurewebsites.net/auth/spotify/callback"
```

### 3. Add publish profile to GitHub

- Azure Portal → App Service → *Get publish profile* → copy the XML
- GitHub repo → Settings → Secrets and variables → Actions → New secret
- Name: `AZURE_WEBAPP_PUBLISH_PROFILE`, value: the XML

### 4. Update redirect URIs

Update your Spotify and Last.fm apps to use the Azure URLs from step 2.

### 5. Push to main

GitHub Actions will build and deploy automatically.

---

## Architecture

```
Browser (Blazor WASM)
    │
    ├─ GET  /session                 Check auth state
    ├─ GET  /auth/lastfm             Redirect to Last.fm OAuth
    ├─ GET  /auth/lastfm/callback    Exchange token → session
    ├─ GET  /auth/spotify            Redirect to Spotify PKCE
    ├─ GET  /auth/spotify/callback   Exchange code → tokens
    ├─ GET  /tracks/preview          Top tracks from Last.fm + Spotify search
    ├─ GET  /tracks/apply/stream     SSE: like tracks in batches of 40
    ├─ GET  /albums/preview          Top albums from Last.fm + Spotify search
    └─ GET  /albums/apply/stream     SSE: save albums in batches of 40

ASP.NET Core API
    ├─ LastFmService       MD5-signed API calls
    ├─ SpotifyService      PKCE OAuth, token refresh, search, batch saves
    ├─ TracksOrchestrator  Parallel Spotify search (concurrency = 3)
    └─ AlbumsOrchestrator  Parallel Spotify search (concurrency = 3)
```

Tokens are stored in server-side session memory and never persisted. Sessions expire after 24 hours of inactivity.

---

## License

MIT © [Damián Guardia](https://github.com/DAGuardia)
