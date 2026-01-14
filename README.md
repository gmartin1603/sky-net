# Sky Net Platform

The purpose of this project is to learn how to build a training grade simulation of mechanical processes.

## Requirements

Simulation must have inputs that can be adjusted while the program is running to test the downstream effects.

## Stack

I'm thinking c# since it's fast and I need more practice with it.

## Quickstart

- Build: `dotnet build SkyNet.sln`
- Run CLI: `dotnet run --project simulator/SkyNet.Simulator.Cli`
- Tests: `dotnet test SkyNet.sln`

### CLI usage

Commands: `start`, `stop`, `list`, `get <name>`, `set <name> <value>`, `status`, `help`, `quit`

Demo parameters:
- `SupplyPressurePsi`
- `ValveOpening` (0..1)
- `LoadForce`