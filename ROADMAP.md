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

Planned work:
1) Component graph + ports
   - Formalize inputs/outputs via typed ports (e.g., pressure, flow, position)
   - Define clear update ordering (or explicit dependency graph)
   - Make “systems” composable from components without hard-coded wiring

2) Parameter model upgrades
   - Add metadata: units, min/max, default, description
   - Add validation and clamping rules (e.g., ValveOpening ∈ [0,1])
   - Add change history or last-changed timestamp (optional, CLI-visible)

3) Observability & logging
   - Add a lightweight telemetry layer (sampled signals + structured log output)
   - CLI commands for `watch <signal>` and `signals`/`params` discovery
   - Optionally export snapshots to JSON/CSV for later graphing

4) Simulation control & safety
   - Support pause/resume/step-N-ticks deterministically from CLI
   - Add consistent cancellation + clean shutdown of runner
   - Introduce "real-time drift" reporting when pacing can’t keep up

5) First training-grade domain slice (choose one)
   - Hydraulic slice: pump → relief valve → directional valve → cylinder + load
   - Pneumatic slice: compressor → regulator → valve → actuator

6) Testing strategy expansion
   - Property tests or table-driven tests for component behaviors
   - Golden snapshot tests for short deterministic runs

Definition of done for Phase 2:
- You can build a small system by wiring components (not hard-coded)
- You can list parameters/signals, watch key outputs, and adjust inputs live
- The system can run deterministically (step mode) and in real-time (paced mode)
- Tests cover core engine behavior and at least one domain slice
