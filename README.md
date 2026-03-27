# aerovim [![Build](https://github.com/dontpanic92/aerovim/actions/workflows/build.yml/badge.svg)](https://github.com/dontpanic92/aerovim/actions/workflows/build.yml)
Neovim ❤ Acrylic

![screenshot.jpg](https://github.com/dontpanic92/aerovim/blob/master/screenshot.jpg)

### Features

- [x] Cross-platform: Windows, macOS, and Linux
- [x] Blur/Acrylic/Mica transparency effects (availability varies by platform)
- [x] Font Ligature
- [x] Built with Avalonia UI and SkiaSharp

### Supported Platforms

| Platform | Runtime ID | Transparency Effects |
|----------|-----------|---------------------|
| Windows x64 | `win-x64` | Blur, Acrylic, Mica, Transparent |
| macOS ARM64 (Apple Silicon) | `osx-arm64` | Acrylic, Transparent |
| macOS x64 | `osx-x64` | Acrylic, Transparent |
| Linux x64 | `linux-x64` | Transparent (compositor-dependent) |

### Build

Requires .NET 10 SDK.

```bash
dotnet restore aerovim.sln
dotnet build aerovim.sln --configuration Debug
```

### Publish

```bash
# Windows
dotnet publish AeroVim/AeroVim.csproj -c Release -r win-x64

# macOS (Apple Silicon)
dotnet publish AeroVim/AeroVim.csproj -c Release -r osx-arm64

# macOS (Intel)
dotnet publish AeroVim/AeroVim.csproj -c Release -r osx-x64

# Linux
dotnet publish AeroVim/AeroVim.csproj -c Release -r linux-x64
```