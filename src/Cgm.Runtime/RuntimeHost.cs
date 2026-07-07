using Cgm.Core.Timing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Cgm.Runtime;

/// <summary>
/// Phase 1 runtime host: opens a GL 3.3 core window and drives a fixed-timestep loop
/// (ADR-005) off Core's <see cref="FixedStepClock"/>, clearing to a solid color. There is
/// no game content yet — later phases attach the scene stack, renderer, and sim here.
/// </summary>
internal sealed class RuntimeHost : IDisposable
{
    private const int WindowWidth = 960;
    private const int WindowHeight = 640;

    private readonly bool _debug;
    private readonly FixedStepClock _clock = new();

    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;

    private long _totalTicks;
    private long _totalFrames;
    private double _logAccumulatorSec;

    public RuntimeHost(bool debug) => _debug = debug;

    public int Run()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "Cgm.Runtime — Creature Game Maker",
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
            Console.WriteLine($"[runtime] loaded — GL {_gl.GetStringS(StringName.Version)}");
    }

    private void OnUpdate(double deltaSeconds)
    {
        int ticks = _clock.Advance(deltaSeconds * 1000.0);
        _totalTicks += ticks;

        // Later phases: run the simulation exactly `ticks` times here.

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
        _gl!.Clear(ClearBufferMask.ColorBufferBit);
        _totalFrames++;
    }

    private void OnClosing()
    {
        if (_debug)
            Console.WriteLine($"[runtime] closing — {_totalTicks} ticks, {_totalFrames} frames total");
    }

    public void Dispose()
    {
        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}
