# GameStash

GameStash is a Windows-focused game library manager that unifies locally installed games and owned titles from multiple storefronts. It imports library metadata, enriches games with IGDB data, tracks personal backlog state, exposes an HTTP API, and provides recommendation strips based on the user's playing profile.

## Highlights

- Unified library across local folders, Steam, Epic Games, GOG and Amazon Games.
- Local-folder scanning with IGDB matching and manual match selection.
- IGDB-backed game search, details, franchises, covers, platforms, genres, themes and release information.
- Personal library state: favorites, currently playing, playlist membership, played/finished status and installed status.
- Store-specific achievements for Steam, Epic Games, GOG and Amazon Games when the corresponding integration is configured.
- Dashboard, stash views, game details, playlist management and recommendation endpoints.
- ASP.NET Core web GUI with embedded static assets for release deployments.
- PostgreSQL persistence through Entity Framework Core.
- Serilog console and rolling-file logging.

## Architecture

```text
GameStash.slnx
├── GameStash.Web.GUI          ASP.NET Core API + embedded HTML/CSS/JavaScript GUI
├── GameStash.Core             Domain models, EF Core context and storefront clients
├── GameStash.Tests             NUnit, integration, Reqnroll and Selenium tests
└── GameStash.Tests.Playwright  Playwright API/UI test project
```

### Main layers

- **GameStash.Core** contains the domain model, persistence and integrations. `StashGame` is the common base for local, Steam, Epic, GOG and Amazon game records.
- **GameStash.Web.GUI** hosts the API controllers, application services and web assets. Services are registered through dependency injection in `Program.cs`.
- **GameStash.Tests** contains unit, integration and feature tests for the core and web application.
- **GameStash.Tests.Playwright** contains Playwright-based API/UI tests.

`IgdbSharp` is maintained in a separate repository and is consumed as an external project dependency. See the [IgdbSharp repository](https://github.com/HedpE-/IgdbSharp) for its dedicated documentation.

## Requirements

- Windows (the web host targets `net9.0-windows` and runs as a Windows Service).
- .NET 9 SDK.
- PostgreSQL Server installed on the machine and running on `localhost:5432`.
- IGDB/Twitch credentials for metadata search and matching.
- Optional storefront credentials:
  - Steam Web API key and SteamID64.
  - Epic Games session/refresh token.
  - GOG session/refresh token.
  - Amazon Games session/refresh token.
- The sibling repositories `Common.Lib` and `IgdbSharp` checked out in the relative locations referenced by the project files.

## Database initialization

`GameStash.Core.Data.GameStashDbContext` uses PostgreSQL and maps the following main sets:

- `local_stash`
- `steam_stash`
- `epic_stash`
- `gog_stash`
- `amazon_stash`
- `settings`
- `root_folders`

The current context configuration connects to:

```text
Host=localhost;Database=gamestash;Username=postgres;Password=postgres
```

PostgreSQL Server must be installed and running on the target machine. On startup, GameStash reads `ConnectionStrings:DefaultConnection`, connects to PostgreSQL's `postgres` maintenance database, creates the configured target database when it does not exist, and then applies the EF Core migrations to create or update the mapped tables.

No manual database or table creation is required. The PostgreSQL user must have permission to connect to the server and create databases. If database initialization fails, startup stops and the error is logged; the service does not continue without persistence.

The schema is managed by the current EF Core migration in `GameStash.Core/Migrations`. Future model changes should be introduced by creating additional migrations rather than by manually changing the database.

> The connection string is configured in `GameStash.Web.GUI/appsettings.json`. Change `ConnectionStrings:DefaultConnection` for other PostgreSQL hosts, credentials or database names.

## Configuration and credentials

The web application stores IGDB and storefront credentials in the `settings` table through `SettingsService`. The available settings are:

- `IGDB_Client_ID`
- `IGDB_Secret`
- `SteamId64`
- `SteamApiKey`
- `EpicRefreshToken` / `EpicAccessToken`
- `GogRefreshToken` / `GogAccessToken`
- `AmazonRefreshToken` / `AmazonAccessToken`

Credentials can be updated through the settings API/UI. Do not commit real credentials, tokens or API keys. For development and CI, prefer environment variables, User Secrets or another protected secret store.

The default web configuration is:

- HTTP: `http://localhost:5088`
- HTTPS: `https://localhost:7030`
- `ASPNETCORE_ENVIRONMENT=Development` when using the development launch profiles.

`Program.cs` also registers the host as a Windows Service named `GameStashService`. In non-development builds, the web assets under `wwwroot` are embedded in the executable and served through an embedded file provider.

## Installation from a release

1. Install PostgreSQL Server on the Windows machine and make sure the PostgreSQL service is running.
2. Download and extract the GameStash release to a stable directory, for example `C:\Program Files\GameStash`.
3. Confirm that the release contains `GameStash.exe` and that the configured PostgreSQL server is reachable.
4. Run PowerShell as Administrator and create the Windows Service manually:

   ```powershell
   $InstallPath = 'C:\Program Files\GameStash'
   $ExecutablePath = Join-Path $InstallPath 'GameStash.exe'

   New-Service `
	   -Name 'GameStashService' `
	   -BinaryPathName "`"$ExecutablePath`"" `
	   -DisplayName 'GameStash Service' `
	   -Description 'GameStash web GUI and game library synchronization service.' `
	   -StartupType Automatic
   ```

5. Start and verify the service:

   ```powershell
   Start-Service -Name 'GameStashService'
   Get-Service -Name 'GameStashService'
   ```

6. Open the configured web address, normally `http://localhost:5088` or `https://localhost:7030`.

To stop, remove or update the service:

```powershell
Stop-Service -Name 'GameStashService'
sc.exe delete GameStashService

# After replacing the release files, recreate the service with New-Service above.
```

If the PostgreSQL Windows Service has a known service name, it can be configured as a dependency so Windows starts PostgreSQL first:

```powershell
Get-Service -Name '*postgres*'
sc.exe config GameStashService depend= 'postgresql-x64-<version>'
```

The exact PostgreSQL service name depends on the installed PostgreSQL version.

## Running locally

1. Install and start PostgreSQL. The application creates the `gamestash` database and applies the EF Core migrations automatically on startup.
2. Make the external `Common.Lib` and `IgdbSharp` repositories available at the paths expected by the project references.
3. Configure IGDB credentials and any storefront credentials.
4. Restore and build the solution:

   ```powershell
   dotnet restore GameStash.slnx
   dotnet build GameStash.slnx
   ```

5. Run the web GUI:

   ```powershell
   dotnet run --project GameStash.Web.GUI/GameStash.Web.GUI.csproj
   ```

6. Open `http://localhost:5088` or the HTTPS URL shown by the selected launch profile.

On startup, the host initializes the database/settings first. Root-folder discovery and the five stash services then initialize concurrently in the background. The first synchronization can take time and depends on the configured external APIs.

## Web API

Controllers are grouped under these routes:

| Area | Route | Purpose |
|---|---|---|
| Dashboard | `GET /api/home/dashboard` | Dashboard data and library summary |
| Stash | `/api/stash/{type}` | Read and update `local`, `steam`, `epic`, `gog`, `amazon` or `unified` libraries |
| Playlist | `/api/playlist` | List and modify the cross-store playlist |
| Recommendations | `/api/playlist/recommendations/playlist` | Recommendations based on playlist games |
| Recommendations | `/api/playlist/recommendations/stash` | Recommendations based on the stash profile |
| Games | `/api/games` | IGDB cards, details, search and franchise games |
| Achievements | `/api/games/{store}/{appId}/achievements` | Store-specific achievements |
| Folders | `/api/folders` | Configure local library roots and match discovered folders |
| Settings | `/api/settings` | Read credentials status and update integration credentials |

Useful examples:

```http
GET /api/stash/unified
GET /api/stash/steam?refresh=true
GET /api/playlist
GET /api/playlist/recommendations/playlist?limit=10
GET /api/playlist/recommendations/stash?limit=10
GET /api/games/search?query=The%20Witcher&limitResults=10
GET /api/settings/igdb/status
```

Swagger is enabled in the Development environment at `/swagger`.

## Recommendation engine

The recommendation implementation is in `GameStash.Web.GUI/Services/RecommendationService.cs`. It exposes two public recommendation modes:

- `GetPlaylistSuggestionsAsync`: uses every game currently marked `IsInPlaylist` as a reference seed.
- `GetStashSuggestionsAsync`: uses the user's broader stash profile and focuses on fresh backlog games.

Each recommendation preserves its storefront source in the returned DTO through `ExternalSystem`.

### 1. Unified pool and candidate filtering

`GetUnifiedStashPoolAsync` loads games from all five sources—Local, Steam, Epic, GOG and Amazon—and keeps the source label with each copy.

**Playlist seeds** are all unified-pool games where `IsInPlaylist` is true. Candidates initially exclude:

- playlist seed IDs;
- finished games (`IsFinished`);
- games marked `PlayedToEnd`.

If that filtered pool is empty, the service falls back to all non-playlist games with an IGDB ID.

**Stash-profile seeds** are games that are any of the following:

- `IsFavorite`;
- `IsPlaying`;
- at least 300 tracked playtime minutes (5 hours);
- IGDB rating of at least 70.

If no such seed exists, the cold-start profile uses the highest-rated and newest games in the library, up to the requested limit. Initial stash candidates exclude seed IDs, finished games, games marked `PlayedToEnd`, and games with at least 120 tracked playtime minutes (2 hours). If this pool is empty, the service falls back to all unfinished/uncompleted games with an IGDB ID.

Before scoring, duplicate candidate copies with the same positive IGDB ID are grouped across stores. The installed copy wins (`IsInstalled == true`), preserving the selected copy's storefront source.

> Playtime is currently read from Steam, Epic and GOG stash types. Local and Amazon games return zero from the recommendation service's playtime helper because those types do not provide a tracked playtime value there.

### 2. Taste vector construction

`BuildTasteVector` extracts IGDB metadata from each seed and accumulates a weighted feature vector. The base weights are:

| Feature | Weight | Example reason |
|---|---:|---|
| Franchise | 3.5 | `Franchise: The Witcher` |
| Collection / series | 3.5 | `Series: Arkham` |
| Developer / studio | 3.5 | `Studio: Remedy Entertainment` |
| Theme | 3.0 | `Theme: Horror` |
| Genre | 2.5 | `Genre: RPG` |
| Player perspective | 1.5 | `Perspective: Third person` |
| Game mode | 1.2 | `Mode: Single player` |
| Platform | 0.8 | `Platform` |

Seed confidence is increased before adding its features:

- Favorite: `+0.8`.
- Currently playing: `+0.8`.
- 10+ hours (`600+` minutes): `+0.4`.
- 30+ hours (`1,800+` minutes): `+0.7`.
- 100+ hours (`6,000+` minutes): `+1.0`.

Official IGDB franchise and collection data is preferred. When no franchise is linked, the service detects known title prefixes such as Final Fantasy, Star Wars, Assassin's Creed, Tomb Raider, Resident Evil, Metal Gear, Dark Souls, The Witcher and Far Cry. It also uses the text before `:` as a safe fallback, for example `Hitman: Blood Money` becomes the `Hitman` group.

### 3. Candidate scoring

For each candidate, the engine computes cosine similarity against the taste vector and applies contextual adjustments:

```text
FinalScore = (CosineSimilarity × EraMultiplier × InstallMultiplier) + QualityBoost
```

- **Cosine similarity** measures the overlap between candidate and seed features.
- **Era multiplier** compares the candidate release year with the average seed release year:
  - up to 4 years: `1.10`;
  - 5–8 years: `1.00`;
  - 9–14 years: `0.85`;
  - 15–20 years: `0.65`;
  - more than 20 years: `0.45`.
- **Install multiplier** is `1.15` for installed games and `1.00` otherwise.
- **Quality boost** is `(Rating - 75) × 0.002` for ratings of 75 or higher.

Candidates without feature overlap are discarded. Results are ordered by final score, then rating. `MatchPercentage` exposes the pure cosine affinity, capped at `99.0`, while `MatchScore` contains the boosted score. Up to the two highest-contributing matching feature labels are returned in `MatchReasons`.

### 4. Diversity and backfill

The engine limits each detected primary franchise/series to two recommendations per result strip (`MaxGamesPerFranchise = 2`). The primary group is resolved from IGDB franchise, franchise collection, collection name or the title-prefix fallback.

If scoring produces fewer than the requested number of results, `BackfillRemainingSlots` fills the remaining slots with unused candidate IDs ordered by:

1. installed status;
2. newest release date;
3. highest IGDB rating.

Backfilled cards use fixed fallback values: `MatchScore = 0.70` and `MatchPercentage = 70.0` when installed, otherwise `0.55` and `55.0`. Their reason is `Ready to Play (Installed)` for installed games or the mode-specific fallback reason (`Playlist Queue Backlog` / `Top Rated in Stash`).

## Local library workflow

1. Add one or more root folders through `/api/folders` or the settings UI.
2. The root-folder monitor scans for discovered game directories.
3. Select or search an IGDB match for an unrecognized folder.
4. Confirm the match to enrich the local stash record.
5. Browse the local or unified stash and update personal state on each game.

Steam, Epic, GOG and Amazon services use their respective clients to import owned titles, refresh metadata, detect installed games where supported and retrieve playtime/achievement information where available.

## Testing

The repository contains two test projects:

```powershell
dotnet test GameStash.Tests/GameStash.Tests.csproj
dotnet test GameStash.Tests.Playwright/GameStash.Tests.Playwright.csproj
```

`GameStash.Tests` includes NUnit tests, Reqnroll feature tests, game matching/scanning tests and storefront integration tests. `GameStash.Tests.Playwright` contains Playwright-based API/UI tests and may require a running application plus browser installation/configuration.

Run the complete test suite with:

```powershell
dotnet test GameStash.slnx
```

External integration tests require valid credentials and network access. Tests that contact Steam, Epic, GOG, Amazon, IGDB or Google services should be isolated or configured appropriately in CI.

## Publishing

The web project is configured for Windows x64. Release publishing creates a self-contained, single-file executable and embeds the static web assets:

```powershell
   dotnet publish GameStash.Web.GUI/GameStash.Web.GUI.csproj -c Release -r win-x64 -o .\artifacts\publish
```

The resulting executable is named `GameStash.exe`. Copy the publish directory to the target machine and follow the [installation from a release](#installation-from-a-release) instructions. Ensure the target machine has PostgreSQL installed and that credentials are configured before starting synchronization.

## Logging and troubleshooting

- Development logs are written to the console through Serilog.
- The configured rolling file sink writes to `Logs/gamestash-.txt` and retains seven daily files.
- If a storefront initialization fails, the host logs the error and continues initializing the remaining services.
- If no recommendations are returned, check that games have IGDB matches/features, that at least one valid seed exists, and that candidate games are not all finished, completed or excluded by playtime.
- If metadata search fails, verify IGDB/Twitch credentials through `GET /api/settings/igdb/status`.
- If a storefront is empty, verify its credentials through the matching settings status endpoint and check the application logs.

## External dependencies

- [Common.Lib](https://github.com/HedpE-/Common.Lib)
- [IgdbSharp](https://github.com/HedpE-/IgdbSharp) — maintained as a separate repository.

