# AFK Simulator – Copilot Instructions

> **Prime Directive**: Before touching any code, **re-read docs/SPECIFICATION.MD and obey its constraints.** If SPECIFICATION.MD conflicts with any other guidance (including this file), SPECIFICATION.MD wins.

## Big Picture
- Start each task by re-reading SPECIFICATION.MD and docs/WIKI.md; they carry machine-readable priorities, roadmap, and developer console requirements referenced by CI.
- Solution split: Engine.Core (deterministic modules + DI), Engine.Server (ASP.NET Core minimal API + SignalR), Engine.Client (Blazor WebAssembly + GPU canvas). Tests mirror Core under tests/Engine.Core.Tests.
- Modules implement IModuleContract and register via ServiceCollectionExtensions.AddIncrementalEngine; ModuleBootstrapperHostedService initializes them and feeds ModuleCatalog/DashboardViewCatalog for Mission Control + /devtools surfaces.
- CoreEngineModule seeds the deterministic tick/resource graph; new systems should expose ModuleInspectable/ModuleCommand members so the developer console can mutate/query them without redeploys.
- Every module ships metadata through MODULE.yaml and ModuleDescriptor so automation can reason about exports, telemetry, and capabilities.

## Local Workflows
- Use scripts/run.ps1 or run.sh for end-user flows (Mission Control only) and scripts/run_developer.* for engineering sessions (Mission Control + /devtools). Pass -NoBrowser or NO_BROWSER=1 when you need headless runs.
- Scripts kill orphaned hosts on ports 7206/5206 (server) and 7061/5269 (client); let them handle process cleanup instead of manual dotnet run loops.
- Tests run via dotnet test -c Release from repo root; CI expects deterministic output, so keep builds analyzer-clean before committing.
- When touching launch profiles or ports, keep PowerShell and Bash variants in lockstep; the docs promise parity across shells.

## Coding Conventions
- Directory.Build.props enforces net10.0, C# preview, nullable enable, deterministic builds, and treats warnings as errors with latest analyzers; align new projects/items with those settings.
- Stick to file-scoped namespaces, minimal comments (only for complex math), and deterministic data structures (sorted dictionaries, stable ordering) to keep AI diffing reliable.
- Commits must follow conventional commit format and embed the relevant docs/TODO-LEDGER.md blocks in the body; update ledger ChangeLog entries whenever you touch an ID.

## Server & API Patterns
- Minimal APIs live in Engine.Server/Program.cs: groups /developer, /accounts, /skills, /assets, /telemetry, /graphics, /leaderboard, /sessions plus /hubs/simulation for SignalR.
- All /developer endpoints enforce the X-Developer-Key header via DeveloperAuthEndpointFilter; local default key is local-dev-key, configurable via appsettings (Developer:ApiKey).
- Account flows surface deterministic models from Engine.Core.Accounts; expect AccountError codes such as UserNotFound, AccountLimit, ProfileLimit for control-flow.
- DeveloperProfileStore persists to App_Data/developer-profiles.json; keep format stable so Mission Control layouts remain shareable.

## Client Patterns
- Engine.Client/Program.cs provisions a scoped HttpClient with Engine:ApiBaseUrl fallback to HostEnvironment.BaseAddress; every service (AccountClient, SessionClient, etc.) is a thin wrapper around JSON APIs.
- AccountClient encapsulates API calls and throws AccountClientException with server-supplied codes; reuse that pattern for new HTTP clients to keep error reporting consistent.
- RenderLoopService bridges Blazor to ./scripts/render-host.js; always serialize RenderSettings and SpriteAnimationDescriptor using its helper shapes to keep the JS host stable.

## Documentation & Observability
- Update docs/SPECIFICATION.MD, docs/WIKI.md, and docs/MEMORY-LEDGER.md whenever you add capabilities, change workflows, or finish tasks—auditors rely on these machine-readable surfaces.
- Keep telemetry identifiers (engine.tick.duration, engine.resources.snapshot, etc.) in sync between MODULE.yaml and ModuleDescriptor so dashboards stay accurate.
- Leaderboards, skills, and resource graph settings appear inside Mission Control views declared by each IDashboardViewProvider implementation; keep layout metadata descriptive so UI auto-layout remains useful.
