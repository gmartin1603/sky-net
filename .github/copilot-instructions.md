# Sky Net Platform - AI Agent Instructions

## Project Overview

Sky Net is a training-grade simulation platform for mechanical processes, designed to model real-world systems with real-time adjustable parameters. The goal is to create realistic simulations where users can modify inputs during runtime and observe downstream effects on the system.

## Technology Stack

- **Language**: C# (chosen for performance and learning objectives)
- **Target**: .NET simulation framework with interactive parameter control
- **Focus Systems**: Hydraulic and pneumatic mechanical processes
- **UI Approach**: CLI first for core functionality, 3D GUI to follow
- **Performance Target**: 60Hz update rate for real-time simulation

## Project Structure

```
sky-net/
├── simulator/          # Core simulation engine (planned)
└── README.md          # Project requirements and vision
```

## Development Principles

### Core Requirements
- **Real-time Parameter Adjustment**: All simulations must support modifying inputs while running
- **Observable Effects**: Changes should propagate through the system with visible downstream impacts
- **Training Grade**: Simulations should be educational, balancing realism with clarity

### Planned Architecture (To Be Implemented)
- Simulation engine with 60Hz time-stepped physics/process models
- Input parameter system supporting runtime modification
- Observable output streams for monitoring system state
- Modular mechanical process components:
  - **Hydraulic**: Pumps, valves, cylinders, accumulators, pressure sensors
  - **Pneumatic**: Compressors, air valves, pneumatic actuators, pressure regulators
- CLI interface for initial parameter control and monitoring
- Future 3D GUI with interactive assets that respond to simulation state

## Code Conventions (When Implementing)

### C# Standards
- Follow standard C# naming conventions (PascalCase for public members, camelCase for private)
- Use modern C# features (records, pattern matching, async/await where appropriate)
- Prefer composition over inheritance for mechanical component modeling

### Simulation Design Patterns
- **Component-based architecture**: Each mechanical element should be an independent, composable unit
- **State management**: Track simulation state explicitly for reproducibility and debugging
- **Time-step integration**: Use consistent time-stepping for predictable behavior
- **Parameter observers**: Implement observable pattern for inputs to trigger recalculations

## Key Workflows (To Be Established)

When implementing, consider:
- How to hot-reload parameter changes without restarting the simulation
- CLI command structure for real-time parameter adjustment during simulation
- Logging strategy for observing system behavior (preparing for 3D visualization)
- Testing strategy for verifying hydraulic/pneumatic process accuracy
- Performance profiling to maintain 60Hz update rate
- 3D asset integration points for future GUI (position, rotation, state updates)

## Next Steps for Development

1. Set up C# project structure (.sln, .csproj files)
2. Design core simulation loop with 60Hz time-stepping mechanism
3. Implement first hydraulic or pneumatic component as proof-of-concept
4. Create CLI interface for runtime parameter modification
5. Add console-based output monitoring for system state
6. Implement additional hydraulic/pneumatic components
7. Design 3D asset state interface for future GUI integration
8. Add 3D visualization with interactive custom assets

---

*Note: This is an early-stage project. Update these instructions as patterns emerge and the codebase matures.*
