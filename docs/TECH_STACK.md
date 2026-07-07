# TECH_STACK.md

Locked stack per ARCHITECTURE_ADDENDUM.md §1. This list is **closed**: adding, removing,
or upgrading a dependency requires editing this file in the same change and user sign-off.

## Platform
- **.NET 10 LTS**, C# 14, `win-x64` primary target. Fallback if a Week-1 spike finds a
  blocking package incompatibility: .NET 9 with a planned bump (never .NET 8 — EOL Nov 2026).
- **SDK pinned: 10.0.301** (verified building + testing this repo on 2026-07-06).

## Local dev environment (this machine — non-standard, deliberate)
The primary C: drive is critically low on space (~0.8 GB free), and the .NET MSI installer
hard-codes `C:\Program Files\dotnet`. Rather than fight that, .NET 10 is installed **off the
MSI**, xcopy-style, onto D: where the project lives:
- **.NET 10 SDK 10.0.301** installed to `D:\dotnet` via the official `dotnet-install.ps1`
  (no admin, no MSI). The machine-wide C: install remains .NET 8 only.
- Persisted **User** environment: `DOTNET_ROOT=D:\dotnet`, `D:\dotnet` prepended to PATH,
  `NUGET_PACKAGES=D:\.nuget-packages` (keeps the package cache off C:),
  `DOTNET_CLI_TELEMETRY_OPTOUT=1`.
- Newly-opened terminals resolve `dotnet` → 10.0.301 automatically. Shells started *before*
  the PATH change (or CI/other machines) should invoke `D:\dotnet\dotnet.exe` explicitly or
  set the three env vars.
- Reversal: delete `D:\dotnet` + `D:\.nuget-packages` and remove the three User env vars.
- CI (GitHub Actions, windows-latest) uses `actions/setup-dotnet@v4` with `10.0.301`; the
  D: layout is a local-machine workaround only and must not leak into build scripts.

## Dependencies (pin exact versions during Phase 1 Week 1 and record them here)

| Package | Version | Used by | Purpose | Engine-rule check | Fallback |
|---|---|---|---|---|---|
| Avalonia (+ .Desktop, .Themes.Fluent) | **12.0.5** | Creator | Desktop MVVM UI | UI toolkit, no game concepts | WPF |
| Silk.NET.Windowing | **2.23.0** | Runtime | Window + GL context + loop callbacks | Thin bindings (GLFW/SDL), zero game functionality | GLFW.NET |
| Silk.NET.Input | **2.23.0** | Runtime | Keyboard/gamepad events | Raw input bindings | SDL input via Silk.NET.SDL |
| Silk.NET.OpenGL | **2.23.0** | Runtime | Raw GL 3.3 core API | We write the renderer; this is the API surface | Silk.NET.Direct3D11 behind IRenderer |
| Silk.NET.OpenAL | Runtime | Audio playback (BGM stream + SFX) | Raw audio API; mixer is ours | miniaudio P/Invoke shim |
| StbImageSharp | Core-adjacent tools, Runtime, Creator | PNG decode | Pure decoder | SixLabors.ImageSharp |
| ZstdSharp.Port | Tools, Runtime | Pack blob compression | Codec | Deflate (System.IO.Compression) |
| System.Text.Json (+ source generators) | Core | Project data, saves, config | BCL | Newtonsoft (last resort) |
| CommunityToolkit.Mvvm | Creator | MVVM source generators (`[ObservableProperty]`/`[RelayCommand]`) | UI helper library, not a game framework; tiny, Microsoft-maintained, no transitive deps | Hand-rolled `ObservableObject`+`RelayCommand` |
| xUnit | Tests | Test framework | — | NUnit |
| Verify.Xunit | Tests | Golden/snapshot tests (battle replays) | — | Hand-rolled file diff |

Version pins verified building on .NET 10.0.301 (2026-07-06 spike). Avalonia is on the **12.x**
stable line (ADR-002 said "11.x" when written in Phase 0; 12 is now the current stable line —
the decision to use Avalonia over WPF is unchanged, only the version moved). Remaining rows
(StbImageSharp, ZstdSharp, Verify) get pinned when first referenced in their owning phase.

The solution uses the **`.slnx`** XML format (`CreatureGameMaker.slnx`), the .NET 10 default.

## Forbidden — absolute, includes transitive "helper" suites
Unity, Godot, Unreal, RPG Maker, GameMaker, Construct, MonoGame, FNA, Raylib / Raylib-cs,
Stride, Flat Red Ball, Duality, osu!framework, SFML wrappers, SDL_gfx-style helper suites,
Silk.NET meta-packages beyond the five bindings listed above, and any package whose
description says "game engine" or "game framework." Physics engines (Box2D etc.) are also
forbidden — grid collision is project code. ECS libraries are forbidden per ADR-004.

## Distribution
- Creator: self-contained zip (`dotnet publish --self-contained`), installer deferred
  (Velopack/MSIX post-vertical-slice).
- Exported games: prebuilt self-contained runtime template exe + `game.cgmpack` +
  `config.json`. End-user machines require **nothing** installed (no .NET, no redists;
  OpenAL via bundled soft_oal.dll).
- Developer machine: .NET 10 SDK + git only.

## Decision log
- 2026-07-06: .NET 10 LTS chosen over .NET 8 (ARCHITECTURE_ADDENDUM §1); stack locked;
  ADR-001…009 recorded in ARCHITECTURE_ADDENDUM §2 (to be split into docs/adr/ in Phase 1).
