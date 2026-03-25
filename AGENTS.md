# AGENTS.md

## Project Overview

dotnvim is a Neovim GUI for Windows featuring Blur/Acrylic effects and font ligature support. It is built on .NET 10 with DirectX rendering via Vortice.Windows.

## Repository Structure

- **Dotnvim/** — Main WinExe application (C#, .NET 10, x64)
- **NeovimClient/** — Neovim RPC client library (C#, .NET 10) using MsgPack.Cli

Dependency graph: `Dotnvim → NeovimClient`

## Build Instructions

This project requires Visual Studio 2022 with the .NET 10 SDK.

### Restore packages

```powershell
dotnet restore dotnvim.sln
```

### Build the solution

```powershell
dotnet build dotnvim.sln --configuration Debug
```

For a release build:

```powershell
dotnet build dotnvim.sln --configuration Release
```

Build output locations:
- `Dotnvim\bin\x64\{Configuration}\net10.0-windows\dotnvim.exe`
- `NeovimClient\bin\x64\{Configuration}\net10.0-windows\Dotnvim.NeovimClient.dll`

## Tests

There are no test projects in this solution.

## Code Style

- **StyleCop.Analyzers** (1.1.118) is enabled on both C# projects.
- Both C# projects set `TreatWarningsAsErrors: true` — all warnings are build errors.
- StyleCop configuration is in `stylecop.json` at the repo root.
- Copyright header: GPLv2, attributed to "dotnvim Developers".
- No `.editorconfig` is present; StyleCop is the primary style enforcer.
