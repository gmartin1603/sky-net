# Sky Net Roadmap

This roadmap is intentionally lightweight and CLI-first.

## Phase 1 (Completed): Minimum viable simulation loop + live CLI

Delivered:
- .NET solution + projects: Core library, CLI runner, and tests
- Deterministic fixed-step ticking (60Hz) plus optional real-time pacing
- Runtime-adjustable parameter store
- Minimal demo “hydraulic-ish” system proving parameter propagation
- CLI REPL with a live-updating status line and command snapshot behavior
- Smoke tests covering determinism and parameter hot-change propagation

## Phase 2: Simulation framework foundations (training-grade architecture)

Goal: evolve the proof-of-concept into a reusable simulation framework that supports composable components, predictable state updates, and better observability — still CLI-first.

Status: Completed (January 2026)

Delivered:
1) Component graph + typed ports + dependency ordering
   - Added unit-strong signal keys (e.g., pressure/flow/position/velocity) and typed `SignalBus` APIs
   - Implemented a dependency-graph system builder that topologically sorts components based on declared reads/writes
   - Enforced safety rules: single writer per signal, unit mismatch detection, and cycle detection

2) Parameter model upgrades
   - Added `ParameterDefinition` metadata (default/min/max/description/unit type)
   - Centralized validation/clamping on set (e.g., ValveOpening clamped to [0,1])
   - Improved change events to expose requested vs applied values and whether clamping occurred

3) Observability & discovery (CLI-first)
   - CLI discovery commands for `params`/`param <name>` and `signals`/`signal <name>`
   - Simple `watch (param|signal) <name>` overlay rendered on the live status line

4) Simulation control & safety
   - CLI step mode: `step [n]`
   - Pause/resume support in the runner
   - Basic real-time drift counters (late ticks + max behind seconds)

5) First training-grade domain slice (hydraulic)
   - Added a training-oriented multi-component `HydraulicTrainingSystem` built via the dependency graph
     (supply → valve → sensor → actuator) with typed units and discoverable signals/params

6) Testing strategy expansion
   - Added focused unit tests for parameter clamping/metadata, dependency ordering, and runner stepping
   - Added a “golden snapshot” test for deterministic hydraulic runs to catch behavior drift intentionally

Definition of done for Phase 2 (met):
- You can build a small system by wiring components (not hard-coded)
- You can list parameters/signals, watch key outputs, and adjust inputs live
- The system can run deterministically (step mode) and in real-time (paced mode)
- Tests cover core engine behavior and at least one domain slice

## Phase 3: Headless sim daemon + web dashboard (visual observability)

Goal: make the simulator runnable as a headless process that streams live telemetry to a separate web-based dashboard for easy testing and training visibility.

Status: Completed (January 2026)

Delivered:
1) Headless daemon process (host)
   - Added `SkyNet.Simulator.Daemon` project (ASP.NET Core)
   - Runs `HydraulicTrainingSystem` via `SimulationRunner`
   - Exposes HTTP endpoints for discovery + control:
     - `GET /api/parameters/definitions`
     - `GET /api/parameters/values`
     - `POST /api/parameters/{name}`
     - `GET /api/signals`
     - `GET /api/status`
     - `POST /api/pause`, `POST /api/resume`, `POST /api/step?n=1`

2) Real-time telemetry streaming
   - Added SignalR hub at `/simhub`
   - Broadcasts `TelemetrySnapshot` messages (time/tick + param/signal dictionaries)

3) Web dashboard (separate process)
   - Added `SkyNet.Simulator.Dashboard` (Blazor WebAssembly)
   - Shows live numeric tiles for key signals
   - Includes parameter edit + set controls
   - Includes a basic rolling line-chart view (SVG) for key signal trends

4) Contracts + stability checks
   - Added `SkyNet.Simulator.Contracts` project for shared DTOs
   - Added a minimal JSON round-trip test to guard telemetry contract drift

Definition of done for Phase 3:
- You can run the headless daemon and connect via browser
- Changing a parameter in the dashboard visibly affects downstream signals
- Dashboard can pause/resume and show live, updating telemetry
