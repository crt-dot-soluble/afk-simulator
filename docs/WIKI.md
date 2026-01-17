# AFK Simulator Wiki

## General Information
- **Architecture**: Three core projects (`Engine.Core`, `Engine.Server`, `Engine.Client`) plus targeted test suites. All gameplay systems implement `IModuleContract` and register through the shared `ModuleCatalog` so modules can self-describe and surface controls inside the developer console.
- **Developer Console**: The `/devtools` Blazor route exposes module metadata, inspectable properties, command/query execution, autocomplete, diagnostics telemetry, and profile sandboxing. This is the canonical surface for tuning modules before shipping new gameplay features.
- **Diagnostics Module**: `DiagnosticsModule` (registered in `Engine.Core`) produces sample telemetry, canned developer profiles, and synthetic resource data to keep the console populated even before gameplay content ships.
- **Profile Persistence**: Developer profiles persist to `App_Data/developer-profiles.json` on the server host. This enables layout/state changes to survive application restarts and makes it simple to share curated setups by copying the JSON file between environments.

## Getting Started
1. **Prerequisites**
   - .NET 10 SDK installed.
   - Node.js 20+ if you plan to extend frontend tooling.
   - Modern browser with WebAssembly + WebGL2/WebGPU enabled.
2. **Run the backend**
   ```bash
   cd src/Engine.Server
   dotnet run
   ```
3. **Run the Blazor client**
   ```bash
   cd src/Engine.Client
   dotnet run
   ```
4. **Access the dashboards**
   - Visit `https://localhost:5001` (or the console output URL) for the minimal API + Swagger surface.
   - Visit the client URL printed by `dotnet run` (typically `https://localhost:7061`) to open the Mission Control dashboard and the `/devtools` interface.
5. **Default root/master access**
   - All developer endpoints require the header `X-Developer-Key`.
   - The default root/master key for local development is **`local-dev-key`**.
   - From the Blazor client this value is injected automatically. For manual API testing (e.g., via cURL/Postman) add `-H "X-Developer-Key: local-dev-key"` to each request.
   - Rotate this key via `appsettings*.json` (`Developer:ApiKey`) or environment variables before deploying to shared environments.

## Useful Tips
- **Refreshing diagnostics**: On `/devtools`, use the *Sample noise* button in the “Diagnostics Telemetry” panel to capture the latest values emitted by `DiagnosticsModule`. This is helpful for verifying that property edits and command invocations round-trip correctly.
- **Ribbon navigation + Settings**: The thin ribbon on the left exposes Dashboard, Settings, and Developer Tools. Graphics, audio, and miscellaneous controls now live under `/settings`, so the dashboard can focus purely on telemetry.
- **Updating profiles**: Use the Profiles panel to export/import JSON fragments for different layout presets. Because the store persists to disk, you can check the resulting file into source control when you want to share defaults with the team.
- **Extending modules**: When you add a new system, decorate relevant properties with `ModuleInspectableAttribute` and commands with `ModuleCommandAttribute`. If the module implements `IModuleDescriptorSource`, it will automatically appear in the module browser after the host restarts.
- **Module-owned views**: Implement `IModuleViewProvider` to describe Mission Control panels as data rather than Razor. Each provider returns `ModuleViewDocument` records composed of reusable blocks (stack, grid, metric, list, action bar, form, sprite, equipment, and timeline). The server aggregates every document through `ModuleViewCatalog` and exposes them at `/dashboard/view-documents?userId=<pilot>`, passing a `ModuleViewContext` into providers so layouts can personalize per pilot/session. The Blazor client renders these docs directly; graphics, leaderboard, sessions, statistics, and the Universe Foundry panel now ride this pipeline with form blocks handling universe + character inputs, and the renderer now understands grid/action blocks so the statistics overview can arrange its active/total metrics in a clean two-column layout without bespoke Razor. Mission Control no longer keeps fallback forms for leaderboard submissions or session spawning—the CoreEngine module owns those forms entirely, and the client always supplies the pilot alias/universe context when requesting documents so modules can pre-fill their inputs deterministically.
- **Tick tuning**: Adjust the *Global Tick Rate* property exposed by `CoreEngineModule` (0.5–60 ticks/sec) to speed up or slow down the entire simulation, and use module-specific tick multipliers (e.g., `StatisticsModule`'s skill pacing) when you need finer control.
- **Live statistics telemetry**: The Mission Control statistics panel polls `/statistics` every second, so XP/level/currency stats update automatically without requiring a manual refresh.

## Database & Caching
- **Default SQLite**: Local developer sessions write to `App_Data/engine.dev.db` through Entity Framework Core. The database is auto-created on launch, so you only need to ensure the `App_Data` folder is writable.
- **Supabase/Postgres**: Point `Database:Provider` to `Supabase` (or `PostgreSql`) in `appsettings.Development.json` or user secrets, then set `Database:Supabase:ConnectionString` to the URI from the Supabase dashboard. The hosted Postgres endpoint persists accounts, universes, characters, and module payloads so multiple clients can share the same data.
- **ModuleStateStore**: The new `ModuleStateStore` table captures JSON blobs for each module (statistics, render settings, future systems). Mission Control and `/devtools` reload seamlessly after restarts because their state now comes from the database rather than in-memory caches.
- **Caching provider**: `Caching:Provider` defaults to `Memory`, but supplying a `RedisConnectionString` flips the server to StackExchange.Redis for read-heavy endpoints. This keeps Supabase calls lean while leaving headroom for future cache invalidation strategies.

## Repository Tooling
- **Sequence editor helper**: Keep `sequence-editor.ps1` in the repo root and point `GIT_SEQUENCE_EDITOR` at it (e.g., `Set-Item Env:GIT_SEQUENCE_EDITOR "powershell -NoProfile -File \"$PWD\sequence-editor.ps1\""`). The script re-labels the first four `pick` entries in an interactive rebase as `reword`, which removes pager friction when rewriting the bootstrap commits to match the TODO ledger format. Clear the env var when you go back to standard rebases.

## Universe Foundry & Character Profiles
- **Mission Control panel**: The dashboard now includes a *Universe Foundry* panel whose layout is streamed directly from `AccountModule` via `ModuleViewDocument` blocks. The module-owned view provisions a pilot via email/password registration, lists every universe tied to that pilot, and renders the roster + declarative forms for forging universes or launching characters. The client still caches the `userId` in `localStorage` so refreshes reuse the same identity while form submissions send parameters back through the module view action system.
- **Developer module**: `AccountModule` exposes limits and currency grants in `/devtools`. Adjust `MaxUniversesPerUser`, `MaxCharactersPerUniverse`, base/premium grants, or swap the starter sprite without touching code. The module also registers the `avatars/ember-nomad` sprite in the shared `AssetManifest` so tooling and the Blazor UI stay in sync.
- **HTTP surface**: Minimal APIs live under `/accounts`. Use `/accounts/users` to register pilots (email/password/display name) and `/accounts/authenticate` to verify credentials. Manage universes via `/accounts/users/{userId}/universes` and mint characters through `/accounts/universes/{universeId}/characters`. All responses reuse the shared record types in `Engine.Core.Accounts` so the client and developer tooling stay strongly typed.
- **Seeding content**: The `seed-demo-user` command on `AccountModule` now creates sample universes/characters for quick UI validation. Use `purge-accounts` to wipe in-memory data during manual testing.
