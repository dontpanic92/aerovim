<p align="center">
  <img src="https://dontpanic92.github.io/aerovim/logo.svg" alt="AeroVim logo" width="128">
</p>

<h1 align="center">AeroVim</h1>

<p align="center">
  <strong>Neovim ❤️ Acrylic</strong><br>
  A fast, elegant, and cross-platform vim/neovim frontend
</p>

<p align="center">
  <a href="https://github.com/dontpanic92/aerovim/actions/workflows/build.yml"><img src="https://github.com/dontpanic92/aerovim/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  &nbsp;
  <a href="https://dontpanic92.github.io/aerovim/">🌐 Homepage</a>
  &nbsp;
  <a href="https://github.com/dontpanic92/aerovim/releases">📦 Download</a>
</p>

<p align="center">
  <img src="https://dontpanic92.github.io/aerovim/screenshot.jpg" alt="AeroVim screenshot" width="720">
</p>

## Features

- ⚡ **Fast** — Hardware-accelerated SkiaSharp rendering for a smooth, responsive editing experience
- ✨ **Elegant** — Blur, Acrylic, Mica transparency effects and full font ligature support
- 🐚 **Shell Integration** — File type associations and system context menu support
- 🖥️ **Cross-Platform** — Runs natively on Windows, macOS, and Linux

## Supported Platforms

| Platform | Runtime ID |
|----------|-----------|
| Windows x64 | `win-x64` |
| macOS ARM64 (Apple Silicon) | `osx-arm64` |
| macOS x64 | `osx-x64` |
| Linux x64 | `linux-x64` |

## Download

Grab the latest release for your platform from **[GitHub Releases](https://github.com/dontpanic92/aerovim/releases)**.

> **macOS note:** Unsigned builds may be blocked by Gatekeeper. Run `xattr -cr /Applications/AeroVim.app` before first launch.

## Build from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore aerovim.sln
dotnet build aerovim.sln -c Release
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

On macOS, publishing automatically creates an `AeroVim.app` bundle. Copy it to `/Applications/`:

```bash
cp -R AeroVim/bin/Release/net10.0/osx-arm64/AeroVim.app /Applications/
```

## License

[GNU General Public License v2](LICENSE) © aerovim Developers