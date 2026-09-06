# BB Game Launcher

An original Windows x64 launcher shell with a calm, late-2000s console-menu feel: blue space haze, floating glass cubes, luminous menu text, keyboard navigation, and an animated startup sequence.

The implementation deliberately uses no PlayStation, PSBBN, or other third-party branding, logos, media, or assets. Everything visual is drawn at runtime with WPF.

## What works

- Animated startup sequence followed by a responsive starfield and floating cubes
- Main menu and System Settings submenu
- Keyboard controls: Up/Down, Enter, Escape, and F11
- Mouse navigation and a minimal borderless window frame
- Windows x64 single-file, self-contained publishing configuration

The Game Library and Applications entries are placeholders for the next phase: real game discovery, artwork, per-game commands, and controller integration.

## Build on Windows

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run this from the repository root in PowerShell:

```powershell
dotnet publish .\src\BBGameLauncher\BBGameLauncher.csproj -c Release -r win-x64 -o .\publish
```

Run `publish\BBGameLauncher.exe`.

To work on it in Visual Studio, open `BBGameLauncher.sln`; Visual Studio 2022 with the **.NET desktop development** workload is the intended environment.

## Project structure

```text
src/BBGameLauncher/
  Controls/SpaceScene.cs      animated, procedural background
  Controls/StartupOverlay.cs  original startup flourish
  MainWindow.xaml             launcher composition
  MainWindow.xaml.cs          navigation and menu behavior
```

## GitHub Actions

Every push to `main` builds a self-contained `win-x64` artifact called `BBGameLauncher-win-x64`. The workflow can also be run manually from the Actions tab.

## License

MIT — see [LICENSE](LICENSE).
