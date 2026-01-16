# Sky Net Platform

The purpose of this project is to learn how to build a training grade simulation of mechanical processes.

## Requirements

Simulation must have inputs that can be adjusted while the program is running to test the downstream effects.

## Stack

I'm thinking c# since it's fast and I need more practice with it.

## Quickstart

- Build: `dotnet build SkyNet.sln`
- Run CLI: `dotnet run --project simulator/SkyNet.Simulator.Cli`
- Run headless daemon (HTTP): `dotnet run --project simulator/SkyNet.Simulator.Daemon --launch-profile http`
- Run web dashboard (HTTP): `dotnet run --project simulator/SkyNet.Simulator.Dashboard --launch-profile http`
- Tests: `dotnet test SkyNet.sln`

## VS Code

- Tasks: Build/Test and run targets are in `.vscode/tasks.json`.
- Launch: Debug profiles (CLI/Daemon) + a compound Daemon+Dashboard launcher are in `.vscode/launch.json`.

## CI / PR gating

A GitHub Actions workflow runs `dotnet restore`, `dotnet build`, and `dotnet test` on PRs to `main`.

To *enforce* “green builds only” before merging, enable branch protection on `main` and require the CI status check.

### Web dashboard

1) Start the daemon, then start the dashboard.
2) Open `http://localhost:5170`.

Defaults:
- Daemon: `http://localhost:5070` (SignalR hub at `/simhub`)
- Dashboard: `http://localhost:5170` (configured to connect to the daemon)

### CLI usage

Commands: `start`, `stop`, `list`, `get <name>`, `set <name> <value>`, `status`, `help`, `quit`

Demo parameters:
- `SupplyPressurePsi`
- `ValveOpening` (0..1)
- `LoadForce`