# Incremental Engine Specification

> **Machine-readable priority**
>
> - Uses numbered sections, stable identifiers, and tables for deterministic parsing.
> - Consistent key names and value formatting to simplify algorithmic extraction.
> - Human explanations appended after structured data blocks.

## 1. Purpose
1.1 `Goal`: Deliver a modular incremental/idle engine inspired by *Legends of IdleOn*, *Melvor Idle*, and "number go up" economies (e.g., *Adventure Capitalist*), including collection/gacha loops.
1.2 `Success Criteria`: Feature-complete modules merged via GitHub feature branches; deterministic simulation, AI-supportable workflow artifacts, and Progressive Web App (PWA) delivery.
1.3 `Multiplayer Mandate`: Real-time multiplayer sessions with authoritative sync services plus global scoreboards/leaderboards that remain deterministic and auditable.
1.4 `Visual Constraints`: Primary target is GPU-accelerated 2D rendering (8x8–512x512 assets) with low-resolution pixel-art sensibilities, scalable UI, and adjustable render cadence for quality/performance balance.

## 2. Scope
2.1 `In-Scope`: Core engine, progression systems, plugin API, multi-platform client shell (browser + installable PWA), CI-ready .NET build pipeline, AI-optimized documentation surfaces, authoritative multiplayer services, real-time global scoring, GPU-accelerated 2D renderer, and extensible asset pipeline for items/textures/effects.
2.2 `Out-of-Scope`: Shipping art packs, live-ops tooling beyond telemetry hooks, monetization SDK integration (stub hooks required), high-fidelity 3D rendering pipelines.

## 3. Operating Principles
| ID | Principle | Enforcement |
|----|-----------|-------------|
| OP1 | Modularity-first | Every subsystem implements `IModuleContract` with versioning + health checks. |
| OP2 | Deterministic first, async second | Simulation ticks run on deterministic scheduler; async tasks only feed inputs. |
| OP3 | Expandability | Plugin manifest advertises new resources/actions without engine rebuild. |
| OP4 | Observability | Structured logs + metrics (OpenTelemetry) with sampling guardrails. |
| OP5 | Atomic features | Feature branches must include tests + TODO log updates before merge. |
| OP6 | Standards compliance | Folder names, analyzers, formatting, analyzers, and build flags enforce latest C#/.NET standards (C# 13/.NET 10) across code, assets, and tooling. |
| OP7 | GPU-first 2D pipeline | Rendering and animation stacks default to GPU acceleration (WebGPU/WebGL2/Metal/Vulkan) with deterministic fallbacks for headless tests. |

## 4. Technology Stack
4.1 `Runtime`: .NET 10 / C# 13 (future-proof for native AOT + modern features) with solution-wide analyzers enforcing language version, nullable context, tab sizes (4 spaces), and naming conventions.
4.2 `Hosting`: ASP.NET Core Minimal API for services; Blazor WebAssembly + WebWorker-backed simulation for client shell; optional MAUI host for desktop/mobile packaging; SignalR/WebSockets for multiplayer transport.
4.3 `Rendering`: GPU acceleration via WebGPU (preferred) with WebGL2/Vulkan/D3D12 fallbacks; server builds leverage SkiaSharp/GPU compute for shared rendering or validation passes.
4.4 `Storage`: Entity Framework Core drives persistence with SQLite (App_Data) for offline/dev runs and Supabase-hosted PostgreSQL for shared deployments, leaving room for future Cosmos adapters. Module data flows through a database-backed `ModuleStateStore`, JSON5 save export remains for portability, and asset metadata stays in JSON5 + binary atlases for 8x8–512x512 textures.
4.5 `Build`: GitHub Actions matrix (Windows, Linux) with deterministic `dotnet publish` for PWA/AOT, including GPU shader compilation, static analysis, formatting, and test execution per feature branch.
4.6 Justification: .NET 10 ensures long-term LTS, performant WASM, strong tooling alignment with GitHub ecosystem, and first-class GPU + multiplayer support.

## 5. Architecture Overview
| Layer | Responsibility | Key Components |
|-------|----------------|----------------|
| Presentation | UI shell, PWA wrapper, input adapters | Blazor UI, GPU canvas renderer, Graphics settings panel, Notification broker |
| Domain Simulation | Game logic, progression, event systems | `TickScheduler`, `ResourceGraph`, `ActionPipeline`, `QuestService`, `MultiplayerSessionService` |
| Data | Persistence, serialization, schema migrations | `SaveSlotStore`, `SchemaRegistry`, `CloudSyncAdapter`, `LeaderboardStore` |
| Platform | Hosting, telemetry, feature toggles | `HostBuilder`, `ModuleLoader`, `TelemetrySink`, `ConfigService`, `AssetPipeline` |

5.1 `Inter-module Contracts`: Defined via `Contracts/*.cs` with protobuf/JSON schema for cross-process serialization.
5.2 `Extensibility`: Module discovery via reflection + manifest JSON. Hot-load supported in editor mode, PWA uses lazy-loaded WASM bundles.
5.3 `Engine Loop`: Fixed timestep (default 100ms) with sub-step resolution for prestige bursts; `ITickConsumer` interface for plug-ins. TickScheduler exposes a developer-tunable tick rate (0.5–60 TPS) and per-module `TickRateProfile` multipliers so systems can speed up or slow down relative to the global clock while preserving deterministic math.
5.4 `Rendering Stack`: GPU jobs scheduled via render graph; supports tile maps, sprite sheets, particle strips, shader-based post-processing, and configurable render cadence (30–240 fps) exposed via graphics settings menu.
5.5 `Asset Pipeline`: Items, textures, audio, and animation definitions live in `/Assets` with manifest-driven hashing, import scripts, and versioned bundles to ensure hot-load, deterministic caching, and plugin contribution.
5.6 `Multiplayer Backbone`: Authoritative simulation nodes expose deterministic deltas over SignalR; `LeaderboardService` streams real-time scores with conflict resolution via CRDT/last-write-wins policies.
5.7 `Module Views`: Every gameplay module that surfaces UI implements `IModuleViewProvider` and emits `ModuleViewDocument` trees (stack/grid/metric/list/action/form/sprite/equipment/timeline blocks). Mission Control consumes these documents directly, so modules remain responsible for their own layouts without bespoke Razor per panel. Providers receive a `ModuleViewContext` (user id + arbitrary query parameters) and the `/dashboard/view-documents` endpoint accepts the same context so panels can personalize output per pilot. Form-capable blocks now cover declarative inputs (text/number/select) so flows like forging universes or minting characters ride the same contract as telemetry.

## 6. Gameplay Systems (Initial)
| System | Description | Justification |
|--------|-------------|---------------|
| Resource Economy | Graph of generators, converters, sinks; supports IdleOn-style multiskill interplay. | Graph approach keeps math composable and AI-traceable. |
| Accounts & Universes | Multi-account identity foundry with email/password auth, developer-tunable universe limits, starter currency grants, and sprite manifests for character profiles. | Enables persistent pilot identity, prestige loops, and UI-driven content drops without compromising determinism. |
| Skill Training | Parallel skill trees with XP curves, mastery bonuses, offline progress calculations. | Matches genre expectations, modular formula definitions. |
| Adventure Nodes | Melvor-like dungeon/zone scripts defined via data files, executed through state machines. | Enables fast content drops without new code. |
| Gacha/Collection | Weighted tables, pity counters, set bonuses; integrates with TODO log for tracking drop tables. | Supports engagement loops; machine-readable definitions. |
| Prestige/Reset | Multi-layer prestige with scaling formulas stored as expression trees. | Keeps long-term goals; deterministic for AI analysis. |
| Multiplayer & Leaderboards | Co-op/competitive nodes with deterministic matchmaking, instanced zones, and real-time global scoreboards. | Reinforces community goals and supports shared progression without compromising determinism. |
| UI/Animation Toolkit | Declarative UI widgets, in-game interfaces, tween timelines, and sprite animation rigs optimized for 2D idle games. | Ensures low-res assets feel polished while remaining easy to extend. |

## 7. Data & State Management
7.1 `State Graph`: Immutable snapshots per tick, diffed for persistence to ensure AI tools can reconstruct history.
7.2 `Serialization`: `System.Text.Json` with source generators for speed; fallback to MessagePack for cloud sync.
7.3 `Save Slots`: Each slot has metadata (seed, playtime, checksum) enabling validation before load.
7.4 `Versioning`: `SaveSchemaVersion` + migrators; engine refuses to run if migration absent (safety for deterministic behavior).
7.5 `Asset Definitions`: Items, textures, animations, and drop tables stored as schema-validated JSON5 with binary blobs referenced via content-addressed hashes; tooling guarantees easy addition without touching core code.
7.6 `Leaderboard Data`: Real-time leaderboards persisted via append-only log with anti-cheat signatures; snapshots replicated to clients for read-only views.
7.7 `Module State Store`: Every module can persist deterministic JSON payloads through the shared `ModuleStateStore` table so developer tooling, Mission Control, and hot-load inspectors can restore state (`StatisticsService`, render settings, etc.) after a restart.
7.8 `Statistics Persistence`: Skill progress, active skill selection, and currency totals are mirrored to the database at tick cadence so Supabase/PostgreSQL nodes and developer tooling read consistent snapshots without relying on in-memory caches.

## 8. Client Delivery & Platform Targets
8.1 `PWA`: Service Worker caches WASM, manifests for install prompts, background sync hooks.
8.2 `Local Package`: MAUI/Blazor Hybrid host; installers built via `dotnet publish -p:PublishAot=true` when feasible.
8.3 `Web`: Static hosting on GitHub Pages/Azure Static Web Apps; CDN for content manifests.
8.4 `Responsive Design`: Layout grid + theming tokens (CSS custom properties) to retain modern look.
8.5 `Graphics Settings`: In-game menu surfaces render rate (30/60/120/240 fps), resolution scale, sprite smoothing, particle density, and GPU tier selection with safe defaults for low-end hardware. These controls, along with audio and miscellaneous toggles, now live on the `/settings` route that is reachable from the vertical ribbon navigation in the Blazor shell.

## 9. Performance & Scalability
9.1 `Tick Budget`: <4ms per tick on mid-tier hardware; enforced via benchmark tests (BenchmarkDotNet).
9.2 `Workload Partitioning`: WebWorker isolates simulation from UI; server mode can scale via horizontal pods running headless simulation shards.
9.3 `Caching`: Deterministic memoization for formula-heavy calculations plus pluggable distributed caches (in-memory by default, StackExchange.Redis when a connection string is supplied) for read-heavy API responses and Supabase-backed workloads.
9.4 `Testing`: Load tests simulate >1e6 entities; CI gates merges if thresholds regress.
9.5 `Render KPIs`: GPU frame budget <=6ms on reference hardware at 60fps, adjustable render cadence ensures deterministic sampling of in-game time despite graphical fidelity toggles.
9.6 `Multiplayer KPIs`: Sub-150ms round-trip for multiplayer actions with jitter buffers; leaderboards replicate within 1s of authoritative update.

## 10. Workflow & TODO Ledger
10.1 `Version Control`: GitHub feature branches (`feature/<id>-<slug>`). PR template requires checklist: tests, TODO updates, spec impact. Branch names always start from the TODO entry ID so automation can trace lineage without heuristics.
10.2 `TODO Ledger` (machine-first):
```
ID:<uuid>
Title:<actionable>
Status:<Queued|InProgress|Blocked|Review|Done>
Owner:<GitHub handle>
Links:<spec sections|issues>
ChangeLog:
  - <commit SHA>: <summary>
Notes:<short context>
```
- Stored in `/docs/TODO-LEDGER.md`, parsed by AI agents.
- Every commit must append to `ChangeLog` entries describing touched IDs for post-hoc reasoning.
10.3 `AI Sync Hooks`: CI step publishes ledger + spec digest to artifacts enabling agents to bootstrap context.
10.4 `Testing Mandate`: Every feature branch must include a dedicated automated test suite (unit + integration + GPU/render snapshots as applicable) before merge; PRs without tests are blocked by policy checks. Contributors must rerun the full Release configuration (`dotnet test -c Release`) plus any `runTests`-based focus suites and ensure the CI/CD matrix (Windows/Linux) passes locally when touching build, persistence, or rendering logic.
10.5 `Branch Health`: Feature branches may only merge when lint/analyzer, formatting, deterministic builds, and multiplayer soak tests pass; partial implementations stay isolated until all acceptance tests succeed.
10.6 `Commit Format`: Conventional commits are mandatory. Short messages follow `type(scope): summary` (e.g., `feat(devtools): add diagnostics noise`). The commit body must inline the exact TODO Ledger block(s) associated with the change so automated agents can diff status, and the footer must reference those entries (example: `Refs: docs/TODO-LEDGER.md#ID:00000000-0000-0000-0000-000000000003`).
10.7 `Branch Discipline`: Every effort begins on a fresh `feature/<todo-id>-<slug>` branch. Push after every logical commit to keep the remote mirror current; local-only work is prohibited because TODO ledger, CI, and reviewers depend on real-time branch availability.
10.8 `Ledger Coupling`: When a commit touches multiple ledger IDs, include the full block for each ID in the body and annotate the footer with multiple `Refs:` lines to maintain bidirectional traceability.
10.9 `Feature Branch Lifecycle`:
```
1. Create branch `feature/<todo-id>-<slug>` immediately after pulling `main`.
2. Implement the change while continuously reviewing/refining code; keep work in small, reviewable slices.
3. Run analyzers/formatters followed by `dotnet test -c Release`, targeted `runTests` suites, and any CI/CD smoke tasks (e.g., `scripts/run.ps1 -NoBrowser`, `scripts/run_developer.ps1 -NoBrowser`) to mirror the pipeline locally.
4. If the stack launches, exercise the feature manually (Mission Control + /devtools) to confirm runtime behavior before staging.
5. Stage only the files tied to the current logical slice, craft a conventional commit message, and embed the authoritative TODO block + `Refs:` footer.
6. Push the branch immediately after each commit (`git push -u origin feature/<todo-id>-<slug>`) so CI/CD runs and stakeholders can monitor progress in real time.
7. Create/update the PR once the branch contains at least one passing, pushed commit; continue iterating with the same loop until reviewers approve.
```
10.10 `CI/CD Parity`: Treat the GitHub Actions matrix as part of the developer workflow. Any feature touching infrastructure, persistence, rendering, networking, or tooling must execute the equivalent local tasks (Release tests + launcher verification) before committing to reduce CI churn.

## 11. AI-Augmented Development
11.1 `Metadata`: Each module includes `MODULE.yaml` with exports, dependencies, telemetry keys for AI ingestion.
11.2 `Memory Tricks`: Deterministic seed logs, reversible simulations, hashed snapshots to allow AI diffing of game states.
11.3 `Code Comments`: Reserved for complex math/algorithms only, to reduce noise for LLM parsing.
11.4 `GPU & Multiplayer Metadata`: MODULE metadata lists shaders, GPU feature levels, and multiplayer endpoints so AI agents can reason about performance and networking impacts.

## 12. Initial Engine Roadmap
| Phase | Deliverables | Exit Criteria |
|-------|--------------|---------------|
| P1 | Spec finalization, TODO ledger scaffold, baseline project skeleton (`dotnet new`), standards enforcement config | CI green, spec + ledger reviewed, analyzers blocking non-compliant code. |
| P2 | Core tick scheduler, module loader, resource graph prototype, save store stub, GPU renderer bootstrap | Demo runs in browser + desktop host with adjustable render rate + baseline multiplayer echo server. |
| P3 | Skill + adventure systems, gacha tables, UI shell, telemetry plumbing, leaderboards + asset tooling | Feature parity with MVP loops and global score syncing. |
| P4 | Optimization, AI automation hooks, live balancing dashboards, multiplayer hardening | Meets performance targets, ready for content scale with full GPU + multiplayer observability. |

## 13. Compliance Checklist
- [x] Modular, plug-and-play architecture.
- [x] .NET/C# core, GitHub workflow.
- [x] PWA + local package delivery with GPU acceleration and adjustable graphics settings.
- [x] IdleOn/Melvor/number-go-up mechanics planned with 2D low-res focus.
- [x] TODO ledger defined for AI context.
- [x] Justifications provided for every critical choice.
- [x] Multiplayer + leaderboard requirements captured.
- [x] Standards/testing mandates documented.

## 14. Standards & Compliance
14.1 `Coding Standards`: Enforce latest C#/.NET analyzers, consistent tab size (4 spaces), file-scoped namespaces, nullable reference types, editorconfig parity across repos, and stylecop/fxcop parity; CI fails on violations.
14.2 `Folder/Asset Naming`: Lowercase-hyphen or PascalCase folder conventions defined in repo root; assets use snake_case with version suffixes to ensure deterministic hashing.
14.3 `Testing Coverage`: Per-feature coverage minimums (unit >90%, integration >80% of critical paths) plus golden image tests for render output and soak tests for multiplayer sessions.
14.4 `Security & Compliance`: Multiplayer endpoints undergo threat modeling, TLS enforcement, cheat detection hooks, and data privacy reviews before release.
14.5 `Documentation`: Each module ships `MODULE.yaml`, API docs, and asset contribution guides ensuring new items/textures/UI elements can be added safely by content teams without touching engine code.

## 15. Developer Tooling
15.1 `Console`: A first-party developer console must exist (currently the Blazor `/devtools` surface) exposing module metadata, inspectable properties, command execution, autocomplete, and profile sandboxing so engineers can extend systems rapidly.
15.2 `Authentication`: All `/developer` HTTP endpoints require the `X-Developer-Key` header. The default root/master key for local setups is `local-dev-key`; production deployments must override via configuration or secrets storage.
15.3 `Profiles`: Developer profiles (UI layouts, notes, sandbox state) persist to disk under `App_Data/developer-profiles.json` by default so tooling changes survive restarts. Stores must be backed up or rotated per environment policies.
15.4 `Diagnostics Module`: At least one diagnostics-focused module (e.g., `DiagnosticsModule`) remains active to generate sample telemetry, seeded profiles, and synthetic data for verification of the developer console.

## 16. Operational Tooling
16.1 `Launch Scripts`: The `scripts/run_developer.(ps1|sh)` helpers boot Engine.Server and Engine.Client together, open both Mission Control and `/devtools`, and clean up host processes on exit. They infer the repo root automatically and honor `NO_BROWSER=1` or the `-NoBrowser` switch.
16.2 `End-User Flows`: The `scripts/run.(ps1|sh)` pair mirrors the production experience (Mission Control only) and shares the same cleanup semantics so QA and stakeholders can launch validated builds with one command.
16.3 `Parity`: All four scripts must remain feature-parity across shells. Any future lifecycle changes (timeouts, env vars, telemetry hooks) land in both Bash and PowerShell variants simultaneously.
16.4 `Database Profiles`: `appsettings*.json` expose `Database.*` (provider, connection string, Supabase overrides, data directory) plus `Caching.*` (Memory vs Redis). Scripts ensure the `App_Data` folder exists for SQLite, while Supabase deployments supply the hosted Postgres URL/API key via secrets.

## 17. Session Workflow
17.1 `Start-of-Session Ritual` (execute in this order before making changes):
```
1. Review docs/SPECIFICATION.MD to refresh constraints and priorities.
2. Review docs/MEMORY-LEDGER.md (newest-first) to load prior AI/operator context.
3. Run `git status -sb` to capture the current working tree and staging state.
4. Run the full automated test suite (`runTests` tool or `dotnet test`) to baseline code health.
5. Clean code: apply analyzers/formatters (`dotnet format` or equivalent) so the repo starts from a lint-clean slate.
```
17.2 `Post-Commit Duties` (required immediately after a change is committed and pushed):
```
1. Update docs/WIKI.md with any user-facing or operational knowledge gained.
2. Append a new entry to docs/MEMORY-LEDGER.md (newest-first) summarizing the conversation or change set.
3. If the work alters core principles, direction, design, or workflow expectations, amend docs/SPECIFICATION.MD accordingly.
4. Ensure the TODO Ledger entry cited in the commit reflects the new state before considering the task complete.
```
17.3 `In-Session Guardrails`:
```
1. Re-read docs/SPECIFICATION.MD whenever relaunching scripts/run*.ps1 or returning from a break to keep directives fresh.
2. For every coding slice: read/plan → implement → review/refine → run tests (`dotnet test -c Release` + targeted suites) → run the app (`scripts/run.ps1` or `scripts/run_developer.ps1`) → only then stage/commit/push.
3. Before handing off work (PR updates, pauses longer than an hour), rerun Release tests and capture launcher status so the next agent inherits a deterministic state.
4. Treat CI parity tasks (matrix builds/tests) as mandatory rehearsal; if a local capacity limit prevents running them, halt and document the blocker in the ledger before proceeding.
```
