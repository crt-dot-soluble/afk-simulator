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
- **Updating profiles**: Use the Profiles panel to export/import JSON fragments for different layout presets. Because the store persists to disk, you can check the resulting file into source control when you want to share defaults with the team.
- **Extending modules**: When you add a new system, decorate relevant properties with `ModuleInspectableAttribute` and commands with `ModuleCommandAttribute`. If the module implements `IModuleDescriptorSource`, it will automatically appear in the module browser after the host restarts.

## Account Hangar & New Life Profiles
- **Mission Control panel**: The dashboard now includes an *Account Hangar* panel that provisions a pilot identity, lists every “hangar” (account), and surfaces all “New Life” character profiles with starter currencies and loadouts. The client stores the `userId` in `localStorage` so refreshes reuse the same identity.
- **Developer module**: `AccountModule` exposes limits and currency grants in `/devtools`. Adjust `MaxAccountsPerUser`, `MaxProfilesPerAccount`, base/premium grants, or swap the starter sprite without touching code. The module also registers the `avatars/ember-nomad` sprite in the shared `AssetManifest` so tooling and the Blazor UI stay in sync.
- **HTTP surface**: Minimal APIs live under `/accounts`. Use `/accounts/users` to create pilots, `/accounts/users/{userId}/accounts` to manage hangars, and `/accounts/accounts/{accountId}/profiles` to mint new lives. All responses reuse the shared record types in `Engine.Core.Accounts` so the client and developer tooling stay strongly typed.
- **Seeding content**: The `seed-demo-user` command on `AccountModule` creates sample hangars/profiles for quick UI validation. Use `purge-accounts` to wipe in-memory data during manual testing.
