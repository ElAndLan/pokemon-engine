using Cgm.Core.Model;
using Cgm.Runtime.Engine;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Cgm.Runtime;

internal sealed class RuntimeHost : IDisposable
{
    private const int DefaultWindowWidth = 960;
    private const int DefaultWindowHeight = 640;

    private readonly bool _debug;
    private readonly bool _smoke;
    private readonly RuntimeContent _content;
    private readonly HostLoop _loop = new();
    private readonly QuadBatch _batch = new();

    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private GlRenderer? _renderer;
    private readonly SceneStack _scenes = new();
    private UiResources? _ui;
    private WorldSession? _session;

    private double _logAccumulatorSec;
    private int _failure;

    public RuntimeHost(bool debug, RuntimeContent content, bool smoke = false)
    {
        _debug = debug;
        _content = content;
        _smoke = smoke;
    }

    public int Run()
    {
        int width = _content.Config.VirtualWidth * 4;
        int height = _content.Config.VirtualHeight * 4;
        if (width <= 0 || height <= 0)
        {
            width = DefaultWindowWidth;
            height = DefaultWindowHeight;
        }

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = string.IsNullOrWhiteSpace(_content.Config.WindowTitle)
                ? _content.Config.GameName
                : _content.Config.WindowTitle,
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
            VSync = true,
            IsVisible = !_smoke,
        };

        try
        {
            _window = Window.Create(options);
            _window.Load += () => Guarded(OnLoad);
            _window.Update += delta => Guarded(() => OnUpdate(delta));
            _window.Render += delta => Guarded(() => OnRender(delta));
            _window.Closing += OnClosing;
            _window.Run();
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException
            or DllNotFoundException or EntryPointNotFoundException)
        {
            // Context creation, shader compilation, and driver loading all land here. Phase 16 does
            // not rebuild a lost context: this is a controlled initialization failure.
            Console.Error.WriteLine(new BootDiagnostic(RuntimeExit.Initialization, "initialization",
                $"Renderer initialization failed: {ex.Message}").Format());
            return (int)RuntimeExit.Initialization;
        }
        return _failure;
    }

    /// <summary>Silk swallows exceptions thrown inside window callbacks, so a GL failure would exit
    /// 0 and look like success. Record the first one, then close instead of rendering garbage.</summary>
    private void Guarded(Action body)
    {
        if (_failure != 0)
            return;
        try
        {
            body();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException
            or ObjectDisposedException or DllNotFoundException or EntryPointNotFoundException)
        {
            _failure = (int)RuntimeExit.Initialization;
            Console.Error.WriteLine(new BootDiagnostic(RuntimeExit.Initialization, "initialization",
                $"Renderer failed during a frame: {ex.Message}").Format());
            _window?.Close();
        }
    }

    private void OnLoad()
    {
        _gl = _window!.CreateOpenGL();
        _input = _window!.CreateInput();

        foreach (IKeyboard keyboard in _input.Keyboards)
            keyboard.KeyDown += (_, key, _) =>
            {
                if (key == Key.Escape)
                    _window!.Close();
            };

        _renderer = new GlRenderer(_gl);
        _ui = new UiResources(_renderer, _batch);
        _session = new WorldSession(_content.Db, _ui.Painter, _content.Db.Settings.TileSize,
            _content.Config.VirtualWidth, _content.Config.VirtualHeight);
        _scenes.Push(new TitleScene(_ui.Painter, _content.Config.VirtualWidth, _content.Config.VirtualHeight,
            _content.Config.GameName, continueAvailable: false));

        if (_debug)
        {
            Console.WriteLine($"[runtime] loaded - GL {_gl.GetStringS(StringName.Version)}");
            Console.WriteLine(
                $"[runtime] game='{_content.Config.GameName}' entities={_content.Db.Entities.Count} start={_content.StartMap.Id}");
        }
    }

    private void OnUpdate(double deltaSeconds)
    {
        // One outer frame: poll once, run 0-5 ticks with edges delivered to the first due tick.
        _loop.Frame(deltaSeconds * 1000.0, ReadHeldActions(), Tick);

        if (!_debug)
            return;

        _logAccumulatorSec += deltaSeconds;
        if (_logAccumulatorSec >= 1.0)
        {
            Console.WriteLine(
                $"[runtime] ticks={_loop.TotalTicks} frames={_loop.TotalFrames} "
                + $"alpha={_loop.InterpolationAlpha:F2} dropped={_loop.DroppedTicks}");
            _logAccumulatorSec = 0.0;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        // The scene stack owns presentation; the host only frames it.
        _ = deltaSeconds;
        int vw = _content.Config.VirtualWidth;
        int vh = _content.Config.VirtualHeight;
        Vector2D<int> size = _window!.FramebufferSize;

        _renderer!.BeginFrame(VirtualResolution.Fit(size.X, size.Y, vw, vh), vw, vh,
            new Rgba(0x18, 0x1C, 0x24, 0xFF));

        // The host owns the batch across the whole stack, so overlapping scenes share draw calls.
        _batch.Begin();
        _scenes.Render();
        _ui?.Painter.Fade(vw, vh, _scenes.FadeAlpha);
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();

        if (_smoke)
            _window.Close();
    }

    /// <summary>One simulation tick. 16C's scene stack consumes this; 16A/16B own only the cadence.</summary>
    private void Tick(TickInput input)
    {
        _scenes.Tick(input);
        if (_scenes.IsTransitioning)
            return;

        // The title reports intent; the host owns the transition, so Title never needs to know what
        // Overworld is. New Game and Continue both enter the start map until 16E adds save loading.
        if (_scenes.Active is TitleScene { Choice: not TitleChoice.None })
        {
            ProjectSettings settings = _content.Db.Settings;
            if (_session!.Enter(_content.StartMap.Id, settings.StartPos, settings.StartFacing) is { } start)
                _scenes.Replace(start);
            return;
        }

        if (_scenes.Active is OverworldScene overworld)
            Advance(overworld);
    }

    /// <summary>Acts on what the overworld surfaced. The scene decides nothing about scene flow;
    /// the host owns every transition.</summary>
    private void Advance(OverworldScene overworld)
    {
        _session!.Track(overworld);
        if (overworld.TakePending() is not { } pending)
            return;

        switch (pending)
        {
            case StepOutcome.Warp warp when _session.Follow(warp.Entity) is { } next:
                _scenes.Replace(next);
                break;

            case StepOutcome.Warp warp:
                // Validation rejects a warp to a missing map, so this is a defect, not bad content:
                // report it and keep playing rather than substituting a destination.
                Report($"Warp '{warp.Entity.Key}' targets missing map '{warp.Entity.Target}'.");
                break;

            default:
                // Battles, encounters, and Core-operation actions need session state that 16E owns.
                Report($"Unhandled overworld outcome: {pending.GetType().Name}.");
                break;
        }
    }

    private void Report(string message)
    {
        if (_debug)
            Console.Error.WriteLine($"[runtime] {message}");
    }

    private IReadOnlyList<GameAction> ReadHeldActions()
    {
        if (_input is null)
            return [];

        var actions = new List<GameAction>(3);
        foreach (IKeyboard keyboard in _input.Keyboards)
        {
            if (keyboard.IsKeyPressed(Key.Up) || keyboard.IsKeyPressed(Key.W))
                actions.Add(GameAction.Up);
            if (keyboard.IsKeyPressed(Key.Down) || keyboard.IsKeyPressed(Key.S))
                actions.Add(GameAction.Down);
            if (keyboard.IsKeyPressed(Key.Enter) || keyboard.IsKeyPressed(Key.Space) || keyboard.IsKeyPressed(Key.Z))
                actions.Add(GameAction.Confirm);
        }
        return actions.Distinct().ToList();
    }

    private void OnClosing()
    {
        // GL objects are created and destroyed on the context-owning thread, and the context dies
        // with the window. Releasing here is the only point where both are still valid. Scenes go
        // first: their leases are borrowed from the renderer that outlives them.
        _scenes.Shutdown();
        _ui?.Dispose();          // scenes borrow the atlas, so it goes after them
        _renderer?.Dispose();
        if (_debug)
            Console.WriteLine($"[runtime] closing - {_loop.TotalTicks} ticks, {_loop.TotalFrames} frames total");
    }

    public void Dispose()
    {
        // The renderer is already gone: GL objects must be released in OnClosing while the context
        // is still current. Disposal is idempotent, so this second call is a harmless no-op.
        _renderer?.Dispose();
        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}
