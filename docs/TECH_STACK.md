# TECH_STACK.md

Locked stack per ARCHITECTURE_ADDENDUM.md section 1. This list is **closed**: adding,
removing, or upgrading a dependency requires editing this file in the same change and
user sign-off.

## Platform
- **.NET 10 LTS**, C# 14, `win-x64` primary target. Fallback if a blocking package
  incompatibility is found: .NET 9 with a planned bump. Never .NET 8; it reaches EOL
  during this project's expected development window.
- **SDK pinned: 10.0.301**.

## Local dev environment
The primary C: drive is critically low on space, and the .NET MSI installer hard-codes
`C:\Program Files\dotnet`. This machine uses an xcopy-style .NET install on D: instead.

- .NET 10 SDK 10.0.301 is installed at `D:\dotnet`.
- User environment should include `DOTNET_ROOT=D:\dotnet`, `D:\dotnet` before the C:
  dotnet install on `PATH`, `NUGET_PACKAGES=D:\.nuget-packages`, and
  `DOTNET_CLI_TELEMETRY_OPTOUT=1`.
- Existing terminals may still resolve `dotnet` to the C: .NET 8 install. Use
  `D:\dotnet\dotnet.exe` when in doubt.
- CI uses `actions/setup-dotnet@v4` with 10.0.301; the D: layout is local only.

## Installed Dependencies

| Package | Version | Used by | Purpose | Engine-rule check | Fallback |
|---|---|---|---|---|---|
| Avalonia | 12.0.5 | Creator | Desktop MVVM UI | UI toolkit, no game concepts | WPF |
| Avalonia.Desktop | 12.0.5 | Creator | Desktop app host | UI toolkit, no game concepts | WPF |
| Avalonia.Themes.Fluent | 12.0.5 | Creator | Default UI theme | Theme assets only | Custom styles |
| CommunityToolkit.Mvvm | 8.4.2 | Creator | MVVM source generators | UI helper, not a game framework | Hand-rolled ObservableObject/RelayCommand |
| StbImageSharp | 2.30.15 | Creator | PNG decode for import/slicing | Pure decoder | SixLabors.ImageSharp |
| Silk.NET.Windowing | 2.23.0 | Runtime | Window + GL context callbacks | Thin bindings, zero game functionality | GLFW.NET |
| Silk.NET.Input | 2.23.0 | Runtime | Keyboard/gamepad input | Raw input bindings | SDL input via Silk.NET.SDL |
| Silk.NET.OpenGL | 2.23.0 | Runtime | Raw GL 3.3 API | We write the renderer | Silk.NET.Direct3D11 behind IRenderer |
| System.Text.Json | BCL | Core | Project data, saves, config | BCL | Newtonsoft only as last resort |
| Microsoft.NET.Test.Sdk | 17.14.1 | Tests | Test host | n/a | n/a |
| xUnit | 2.9.3 | Tests | Test framework | n/a | NUnit |
| xunit.runner.visualstudio | 3.1.4 | Tests | Test discovery/runner adapter | n/a | NUnit adapter |
| coverlet.collector | 6.0.4 | Tests | Coverage collection support | n/a | Remove if unused |

Avalonia is on the 12.x stable line. ADR-002 originally said 11.x; the decision to use
Avalonia over WPF is unchanged, only the version moved.

The solution uses the `.slnx` XML format (`CreatureGameMaker.slnx`), the .NET 10 default.

## Planned / Not Currently Referenced

These remain allowed by architecture, but they are not installed in the current project
files. Adding them still requires the package reference and this doc to change together.

| Package | Planned phase/use | Current status |
|---|---|---|
| Silk.NET.OpenAL 2.23.0 | Phase 16E audio playback | User-approved 2026-07-11; not referenced until Phase 16E; native clean-machine payload/license verified by the 16E gate |
| ZstdSharp.Port | Optional pack blob compression | Not referenced; Phase 12 pack uses stdlib Deflate |
| Verify.Xunit | Golden/snapshot tests | Not referenced; current tests are plain xUnit |

## Forbidden

Unity, Godot, Unreal, RPG Maker, GameMaker, Construct, MonoGame, FNA, Raylib/Raylib-cs,
Stride, Flat Red Ball, Duality, osu!framework, SFML wrappers, SDL_gfx-style helper
suites, Silk.NET meta-packages beyond the binding packages listed above, and any package
whose description says "game engine" or "game framework." Physics engines such as Box2D
are also forbidden; grid collision is project code. ECS libraries are forbidden per
ADR-004.

## Distribution
- Creator: self-contained zip (`dotnet publish --self-contained`), installer deferred
  to post-vertical-slice.
- Exported games: prebuilt self-contained runtime template exe + `game.cgmpack` + `config.json`.
  Data pack/config generation, local template copy/rename, and Runtime smoke are implemented.
  CI-published self-contained templates, icon/version patching, Creator export UI, and clean-VM
  verification remain Phase 18 work.
- Developer machine: .NET 10 SDK + git only.

## Decision Log
- 2026-07-06: .NET 10 LTS chosen over .NET 8; stack locked in
  `ARCHITECTURE_ADDENDUM.md`.
- 2026-07-08: Reconciled this file against actual package references. Moved OpenAL,
  ZstdSharp, and Verify.Xunit to planned/not-referenced because they are not currently in
  project files.
- 2026-07-11: User approved Silk.NET.OpenAL 2.23.0 for the Phase 16E Runtime audio backend.
  Approval does not authorize adding the reference or audio code before Phase 16E.
