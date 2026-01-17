# Sky Net (SkyNet) - Copilot Coding Agent Instructions

## Big picture
SkyNet is a training-grade simulation sandbox built around a deterministic fixed-step runner (default 60Hz) plus optional real-time pacing. “Systems” expose runtime-adjustable parameters and observable signals, and are composed from small components wired via a dependency graph.

Projects under `simulator/`:
- `SkyNet.Simulator.Core`: engine primitives (parameters, signals, units, runner, component graph)
- `SkyNet.Simulator.Cli`: CLI REPL to run/step/pause and edit parameters live
- `SkyNet.Simulator.Daemon`: ASP.NET Core host exposing HTTP APIs + SignalR telemetry
- `SkyNet.Simulator.Dashboard`: Blazor WASM UI consuming HTTP + SignalR
- `SkyNet.Simulator.Contracts`: shared DTOs for daemon↔dashboard (`TelemetrySnapshot`, etc.)

## Core architecture & patterns (follow these)
- **Fixed step time**: simulation time advances only via `SimulationRunner.StepOnce/Step` ([simulator/SkyNet.Simulator.Core/Simulation/SimulationRunner.cs](simulator/SkyNet.Simulator.Core/Simulation/SimulationRunner.cs)). `RunRealTimeAsync` is best-effort pacing and may fall behind (see `LateTicks/MaxBehindSeconds`).
- **Systems** implement `ISimSystem` and surface `Parameters` + `Signals` (see `HydraulicTrainingSystem`). Prefer building systems from components via `SimSystemBuilder`.
- **Component graph**: components implement `ISimComponent` and declare `Reads`/`Writes` with `(Name, UnitType)` dependencies ([simulator/SkyNet.Simulator.Core/Components/ISimComponent.cs](simulator/SkyNet.Simulator.Core/Components/ISimComponent.cs)). `SimSystemBuilder` topologically sorts components and enforces:
  - single writer per signal name
  - unit-type match between writers/readers
  - cycle detection
- **Typed units & keys**: prefer `SignalKey<TUnit>` + `ParameterKey<TUnit>` and unit structs in `Core/Units/`. Example names in `HydraulicTrainingSystem`: `SupplyPressurePsi`, `ValveOpening`, `DownstreamPressurePsi`.
- **Parameter validation**: define parameters with metadata (`default/min/max/description`) and rely on `ParameterStore.Set` to clamp and emit `ParameterChanged` with requested vs applied values ([simulator/SkyNet.Simulator.Core/Parameters/ParameterStore.cs](simulator/SkyNet.Simulator.Core/Parameters/ParameterStore.cs)).
- **Signals**: `SignalBus` stores raw doubles keyed by name; typed `Get/Set` wrap unit conversions ([simulator/SkyNet.Simulator.Core/Signals/SignalBus.cs](simulator/SkyNet.Simulator.Core/Signals/SignalBus.cs)).

## Daemon ↔ Dashboard integration
- Daemon HTTP endpoints live in [simulator/SkyNet.Simulator.Daemon/Program.cs](simulator/SkyNet.Simulator.Daemon/Program.cs) (`/api/status`, `/api/parameters/*`, `/api/signals`, `/api/pause|resume|step`). `POST /api/step` requires the runner to be paused.
- Real-time telemetry uses SignalR hub `/simhub`; daemon pushes `TelemetrySnapshot` via event name `"snapshot"` ([simulator/SkyNet.Simulator.Daemon/SimHostService.cs](simulator/SkyNet.Simulator.Daemon/SimHostService.cs)). Contracts are in [simulator/SkyNet.Simulator.Contracts/Dtos.cs](simulator/SkyNet.Simulator.Contracts/Dtos.cs).

## Dev workflows (Windows/.NET)
- Build: `dotnet build SkyNet.sln -c Debug`
- Tests: `dotnet test SkyNet.sln -c Debug` (includes a golden snapshot test for `HydraulicTrainingSystem` behavior drift)
- Run CLI: `dotnet run --project simulator/SkyNet.Simulator.Cli`
- Run daemon: `dotnet run --project simulator/SkyNet.Simulator.Daemon --launch-profile http`
- Run dashboard: `dotnet run --project simulator/SkyNet.Simulator.Dashboard --launch-profile http` (expects daemon at `http://localhost:5070` by default)

## When changing sim behavior
- If you change the deterministic behavior of `HydraulicTrainingSystem`, update the golden values intentionally in [simulator/SkyNet.Simulator.Tests/HydraulicTrainingSystemGoldenSnapshotTests.cs](simulator/SkyNet.Simulator.Tests/HydraulicTrainingSystemGoldenSnapshotTests.cs).
