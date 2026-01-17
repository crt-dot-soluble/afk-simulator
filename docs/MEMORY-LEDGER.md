# Memory Ledger

---
EntryId: MEM-20260117-06
Timestamp: 2026-01-18T00:30:00Z
Scope: Persistence;Mission Control;Launcher
Summary: Serialized the module-state persistence pipeline with a write gate so the Mission Control launcher no longer crashes with SQLite "database is locked" errors during the tick loop.
Decisions:
- DECISION: DatabaseModuleStateStore must serialize writes/deletes through a shared semaphore to keep SQLite happy until we swap to Supabase/Postgres.
- DECISION: Launcher verification remains part of the workflow whenever persistence changes touch Mission Control.
Actions:
- ACTION: Wrapped DatabaseModuleStateStore.SaveAsync/DeleteAsync in a semaphore, implemented IAsyncDisposable, reran tests, and confirmed `scripts/run.ps1 -NoBrowser` stays alive without lock faults.
- ACTION: Updated the wiki/database notes to call out the serialized ModuleStateStore writes and logged this entry for future agents.

---
EntryId: MEM-20260118-01
Timestamp: 2026-01-18T00:45:00Z
Scope: Workflow;Version Control;Testing
Summary: Strengthened SPECIFICATION.MD with machine-readable branching, commit, and testing directives so every agent follows the same branch lifecycle and CI parity checks.
Decisions:
- DECISION: Every feature branch must run Release tests, targeted suites, launchers, and CI parity tasks before staging/committing/pushing.
- DECISION: Branch creation, refinement, testing, and pushing now follow a seven-step lifecycle baked into §10.9, with guardrails reiterated in §17.
Actions:
- ACTION: Updated SPECIFICATION.MD §§10 & 17 to codify the branch/test workflow and added CI/CD parity expectations.
- ACTION: Logged the change in the TODO ledger under the operational tooling entry for traceability.

---
EntryId: MEM-20260118-02
Timestamp: 2026-01-18T01:15:00Z
Scope: Workflow;Pull Requests;Repository Hygiene
Summary: Embedded a reusable PR template (title/description/verification/checklist) and mandated `.github/` parity inside SPECIFICATION.MD so tooling consistently surfaces the directives.
Decisions:
- DECISION: Every PR description must follow the code-block template in SPEC §10.11.
- DECISION: The `.github/` folder is treated as mandatory infrastructure and must accompany any workflow/spec updates.
Actions:
- ACTION: Updated SPECIFICATION.MD §§10 & 16 plus TODO ledger to capture the template and `.github` directive for future agents.

---
EntryId: MEM-20260118-03
Timestamp: 2026-01-18T01:25:00Z
Scope: Workflow;Pull Requests
Summary: Added SPEC §10.12 requiring every PR handoff to include the GitHub compare URL so reviewers can open the diff immediately.
Decisions:
- DECISION: Whenever someone is asked to open a PR, they must cite the exact compare link `https://github.com/crt-dot-soluble/afk-simulator/compare/main...feature/<todo-id>-<slug>`.
Actions:
- ACTION: Updated SPECIFICATION.MD, TODO ledger, and this memory log to capture the compare-link directive for future agents.

---
EntryId: MEM-20260117-05
Timestamp: 2026-01-17T23:50:00Z
Scope: Module Views;Mission Control;Leaderboard/Sessions
Summary: Mission Control now sources leaderboard submissions and session spawning exclusively from module-owned documents while passing the pilot alias through context so forms pre-fill without bespoke Razor.
Decisions:
- DECISION: Mission Control panels must render only the module-supplied forms for leaderboard scores and multiplayer sessions; inline Razor fallbacks are no longer allowed once a module publishes a document.
- DECISION: Module view requests always include the current pilot alias (and optional universe) so providers can seed deterministic defaults, keeping score submissions and shard names stable after refreshes.
Actions:
- ACTION: Updated Home.razor to drop fallback leaderboard/session forms, parse module action payloads (alias, score, session name), and reuse CoreEngineModule commands for submissions/spawns.
- ACTION: Passed the pilot alias through `ModuleViewClient` context parameters, refreshed WIKI/spec references, and logged the TODO ledger plus memory entries covering the migration.

---
EntryId: MEM-20260117-04
Timestamp: 2026-01-17T23:00:00Z
Scope: Module Views;Statistics;Mission Control
Summary: Mission Control now renders module-view grid/action blocks, the statistics overview uses the new layout, and regression tests cover the descriptor output.
Decisions:
- DECISION: Grid/action renderers live in the shared ModuleViewPanel so every module can ship complex layouts without custom Razor.
- DECISION: StatisticsModule exposes its overview metrics through a grid block to prove the contract before other modules migrate.
Actions:
- ACTION: Added ModuleViewGridBlock rendering + styling (including action bars/sprite placeholders) so Mission Control honors the full contract.
- ACTION: Updated StatisticsModule to emit a two-column overview grid, added coverage in StatisticsModuleViewTests, and refreshed docs/TODO with the new capabilities.

---
EntryId: MEM-20260117-03
Timestamp: 2026-01-17T21:00:00Z
Scope: Module Views;Accounts;Mission Control
Summary: Added form-capable module view blocks, taught the Accounts module to emit Universe Foundry documents, and updated the Blazor shell so Mission Control renders the panel generically with action payloads.
Decisions:
- DECISION: Module view contracts now include a `form` block so modules can declare text/number/select fields whose values travel with action requests.
- DECISION: The Accounts module owns the Universe Foundry panel (lists, roster, forge/launch forms) to eliminate bespoke Razor and keep inputs deterministic.
Actions:
- ACTION: Extended `ModuleViewDocument`/`ModuleViewPanel` with form primitives + action payload plumbing, then wired Home.razor and ModuleViewClient to pass context (active universe) back to the server.
- ACTION: Implemented `IModuleViewProvider` on `AccountModule`, exposed universe/character forms/metrics, refreshed docs/wiki/spec, and added coverage ensuring the new documents seat properly.


---
EntryId: MEM-20260117-02
Timestamp: 2026-01-17T18:00:00Z
Scope: Module Views;Mission Control;Leaderboard/Sessions
Summary: Introduced contextual module view documents so panels can personalize per pilot, wired Graphics/Leaderboard/Sessions to CoreEngineModule-provided layouts, and taught the Blazor shell how to handle module actions + selections.
Decisions:
- DECISION: `/dashboard/view-documents` now accepts a user-scoped context and forwards it to every `IModuleViewProvider`, enabling modules to emit per-account layouts without bespoke Razor.
- DECISION: CoreEngineModule owns the graphics, leaderboard, and multiplayer session views, while legacy forms remain only for inputs until form-capable blocks exist.
Actions:
- ACTION: Added `ModuleViewContext`, action callbacks, and navigation/refresh commands; extended Home.razor + ModuleViewPanel to handle selections/actions and refresh docs after interactions.
- ACTION: Generated new documents from CoreEngineModule for graphics, leaderboard, and sessions using live services, refreshed docs/spec/wiki, and updated tests + DI registrations.

---
EntryId: MEM-20260117-01
Timestamp: 2026-01-17T15:00:00Z
Scope: Database;Supabase;Statistics Persistence
Summary: Introduced the EF Core persistence layer (SQLite locally, Supabase/Postgres remotely), wired a Redis-ready cache toggle, and migrated accounts/statistics to the new ModuleStateStore-powered database so developer tools always reload real data.
Decisions:
- DECISION: Treat Supabase as the canonical production provider while retaining SQLite under `App_Data` for offline testing.
- DECISION: Persist module state (statistics snapshots, render settings, future systems) via a shared ModuleStateStore so Mission Control and `/devtools` stay deterministic after restarts.
Actions:
- ACTION: Added DbContext/hosted initializer, database options, and a persistent `IAccountService` that hashes credentials and stores pilots/universes/characters in the relational store.
- ACTION: Implemented a database-backed `IModuleStateStore`, updated `StatisticsService` to save/load snapshots, and documented the new Database/Caching knobs across SPEC + WIKI.

---
EntryId: MEM-20260116-04
Timestamp: 2026-01-16T19:30:00Z
Scope: Mission Control UI;Settings;Statistics Panel
Summary: Replaced the top navigation bar with a ribbon-style launcher, moved graphics/audio/other controls into a dedicated Settings page, and finished wiring the statistics-first dashboard helpers.
Decisions:
- DECISION: Mission Control uses a thin vertical ribbon with iconography so additional routes (Settings, DevTools) can surface without stealing hero space.
- DECISION: Graphics/audio/miscellaneous toggles must live on `/settings` to keep the dashboard dedicated to telemetry and live state.
Actions:
- ACTION: Added the `/settings` page with graphics forms, audio sliders, and experimental UI toggles plus updated docs/spec to reference the new workflow.
- ACTION: Reworked `Home.razor` helpers to consume statistics snapshots only and replaced the Graphics dashboard card with a CTA pointing to the Settings route.

---
EntryId: MEM-20260116-03
Timestamp: 2026-01-16T18:00:00Z
Scope: Tick Scheduler;Mission Control;Rendering
Summary: Added developer-facing controls for the global tick rate and module-specific multipliers, refreshed the Mission Control skill telemetry loop, and fixed the hero viewport so it animates continuously.
Decisions:
- DECISION: TickScheduler must expose both a global tick-rate control and per-consumer `TickRateProfile` multipliers so systems can run faster or slower without breaking determinism.
- DECISION: Mission Control panels rely on background polling to keep stats in sync, and the render host favors a deterministic 2D loop until the GPU pipeline lands.
Actions:
- ACTION: Updated CoreEngine/Skill modules plus scheduler/tests to support adjustable ticks and relative consumer speeds.
- ACTION: Added a periodic client-side skill state refresh and repaired the canvas render loop/animation bridge.

---
EntryId: MEM-20260116-02
Timestamp: 2026-01-16T16:30:00Z
Scope: Workflow;Documentation Discipline
Summary: Locked in a standardized session ritual and post-commit documentation duties so every AI-assisted cycle starts from a known state and leaves a durable trace.
Decisions:
- DECISION: Every session begins with spec + ledger review, git status, full tests, and repo formatting to ensure deterministic baselines.
- DECISION: After each push, the wiki and memory ledger must be updated, with the spec amended whenever strategy or workflow shifts.
Actions:
- ACTION: Added §17 (Session Workflow) to the specification capturing the required pre/post change steps.
- ACTION: Logged this memory entry to enforce the new documentation cadence.

---
# Memory Ledger

---
EntryId: MEM-20260116-01
Timestamp: 2026-01-16T15:00:00Z
Scope: Skills Module;Dashboard Layout;Build Pipeline
Summary: Mission Control now consumes module-provided descriptors, shows the idle skill loop by default, and enforces the "tests before run" workflow while CA warnings were cleared.
Decisions:
- DECISION: Dashboard hero row and secondary cards must be generated from registered module descriptors for future plugin compatibility.
- DECISION: Idleing remains the mandatory default activity and drives hero sprite animation until another active skill is chosen.
Actions:
- ACTION: Refactored Home.razor/app.css to use descriptor-driven layout with compact cards, skill stats, and hero animation hooks.
- ACTION: Fixed dashboard descriptor argument casing, idle animation selection, and CA1812 warnings via targeted suppressions.

- ACTION: Added Memory Ledger specification and this ledger file to capture AI conversations chronologically.
