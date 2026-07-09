using Cgm.Core.Timing;
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
    private readonly ExportedGame? _game;
    private readonly FixedStepClock _clock = new();

    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;

    private long _totalTicks;
    private long _totalFrames;
    private double _logAccumulatorSec;

    public RuntimeHost(bool debug, ExportedGame? game = null)
    {
        _debug = debug;
        _game = game;
    }

    public int Run()
    {
        int width = (_game?.Config.VirtualWidth ?? 240) * 4;
        int height = (_game?.Config.VirtualHeight ?? 160) * 4;
        if (width <= 0 || height <= 0)
        {
            width = DefaultWindowWidth;
            height = DefaultWindowHeight;
        }

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = _game?.Config.WindowTitle ?? "Cgm.Runtime - Creature Game Maker",
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
            VSync = true,
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;
        _window.Run();
        return 0;
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

        _gl.ClearColor(0.09f, 0.11f, 0.14f, 1.0f);

        if (_debug)
        {
            Console.WriteLine($"[runtime] loaded - GL {_gl.GetStringS(StringName.Version)}");
            if (_game is not null)
                Console.WriteLine($"[runtime] game='{_game.Config.GameName}' entities={_game.Db.Entities.Count} start={_game.StartMap.Id}");
        }
    }

    private void OnUpdate(double deltaSeconds)
    {
        int ticks = _clock.Advance(deltaSeconds * 1000.0);
        _totalTicks += ticks;

        if (!_debug)
            return;

        _logAccumulatorSec += deltaSeconds;
        if (_logAccumulatorSec >= 1.0)
        {
            Console.WriteLine(
                $"[runtime] ticks={_totalTicks} frames={_totalFrames} alpha={_clock.InterpolationAlpha:F2}");
            _logAccumulatorSec = 0.0;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        _ = deltaSeconds;
        if (_game is null)
        {
            _gl!.Clear(ClearBufferMask.ColorBufferBit);
        }
        else
        {
            RenderBattleShowcase();
        }
        _totalFrames++;
    }

    private void RenderBattleShowcase()
    {
        _gl!.Disable(EnableCap.ScissorTest);
        _gl.ClearColor(0.12f, 0.16f, 0.18f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        Vector2D<int> size = _window!.FramebufferSize;
        int w = size.X;
        int h = size.Y;
        FillRect(0, h / 2, w, h / 2, 0.20f, 0.32f, 0.25f);
        FillRect(w / 12, h * 5 / 8, w / 3, h / 10, 0.70f, 0.82f, 0.56f);
        FillRect(w * 7 / 12, h * 3 / 8, w / 3, h / 10, 0.75f, 0.36f, 0.25f);
        FillRect(w / 16, h / 12, w * 7 / 8, h / 6, 0.93f, 0.91f, 0.82f);
        FillRect(w / 12, h / 10, w / 3, h / 36, 0.12f, 0.55f, 0.24f);
        FillRect(w * 7 / 12, h / 10, w / 4, h / 36, 0.12f, 0.55f, 0.24f);
        _gl.Disable(EnableCap.ScissorTest);
    }

    private void FillRect(int x, int y, int width, int height, float r, float g, float b)
    {
        if (width <= 0 || height <= 0)
            return;

        Vector2D<int> size = _window!.FramebufferSize;
        int glY = Math.Max(0, size.Y - y - height);
        _gl!.Enable(EnableCap.ScissorTest);
        _gl.Scissor(x, glY, (uint)width, (uint)height);
        _gl.ClearColor(r, g, b, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    private void OnClosing()
    {
        if (_debug)
            Console.WriteLine($"[runtime] closing - {_totalTicks} ticks, {_totalFrames} frames total");
    }

    public void Dispose()
    {
        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}