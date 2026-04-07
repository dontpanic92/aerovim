# AGENTS.md

## Project Overview

aerovim is a cross-platform vim/neovim frontend featuring Blur/Acrylic/Mica effects and font ligature support. It is built on .NET 10 with Avalonia UI and SkiaSharp rendering. Supported platforms: Windows, macOS, and Linux.

## Repository Structure

- **AeroVim/** — Main application (C#, .NET 10, cross-platform)
- **NeovimClient/** — Neovim RPC client library (C#, .NET 10) using MsgPack.Cli

Dependency graph: `AeroVim → NeovimClient`

## Build Instructions

This project requires the .NET 10 SDK. On Windows, Visual Studio 2022 can also be used.

### Restore packages

```powershell
dotnet restore aerovim.sln
```

### Build the solution

```powershell
dotnet build aerovim.sln --configuration Debug
```

For a release build:

```powershell
dotnet build aerovim.sln --configuration Release
```

Build output locations:
- `AeroVim\bin\x64\{Configuration}\net10.0\aerovim.exe` (Windows)
- `NeovimClient\bin\x64\{Configuration}\net10.0\AeroVim.NeovimClient.dll`

## Platform Notes

- **Keyboard input** uses Avalonia's `TextInput` event for character keys and `KeyMapping.cs` for special keys and modifier combinations (Ctrl/Alt + key).
- **Transparency effects** vary by platform. `Helpers.cs` contains platform-aware availability checks.
- **Default font** is platform-specific: Consolas (Windows), Menlo (macOS), DejaVu Sans Mono (Linux).

## Tests

There are no test projects in this solution.

## Code Style

- **StyleCop.Analyzers** (1.1.118) is enabled on both C# projects.
- Both C# projects set `TreatWarningsAsErrors: true` — all warnings are build errors.
- StyleCop configuration is in `stylecop.json` at the repo root.
- Copyright header: GPLv2, attributed to "aerovim Developers".
- No `.editorconfig` is present; StyleCop is the primary style enforcer.
