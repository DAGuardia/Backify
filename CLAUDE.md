# Backify — Claude Directives

## Stack
- **Backend**: ASP.NET Core 9 Web API (C#) — `src/Backify.Api/`
- **Frontend**: Blazor WebAssembly (C#) — `src/Backify.Web/`
- Ignore the `backend/` (Node.js) and `frontend/` folders — are NOT the active project.

## Configuration
- API keys go in `src/Backify.Api/appsettings.Development.json`

## Commands to run locally (PowerShell)
```powershell
# Terminal 1 – API (port 5000)
cd src/Backify.Api; dotnet run

# Terminal 2 – Frontend (port 5173)
cd src/Backify.Web; dotnet run
```
