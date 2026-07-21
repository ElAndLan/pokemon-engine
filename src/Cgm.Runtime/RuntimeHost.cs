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
    private readonly RuntimeContent _content;
    private readonly FixedStepClock _clock = new();
    private readonly InputState _inputState = new();

    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;

    private long _totalTicks;
    private long _totalFrames;
    private double _logAccumulatorSec;

    public RuntimeHost(bool debug, RuntimeContent content)
    {
        _debug = debug;
        _content = content;
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
            Console.WriteLine(
                $"[runtime] game='{_content.Config.GameName}' entities={_content.Db.Entities.Count} start={_content.StartMap.Id}");
        }
    }

    private void OnUpdate(double deltaSeconds)
    {
        int ticks = _clock.Advance(deltaSeconds * 1000.0);
        _totalTicks += ticks;
        _inputState.Update(ReadHeldActions());

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
        // 16A reaches a validated aggregate and clears the screen. Real presentation is 16B's
        // renderer and 16C's BootScene; nothing here may assume content.
        _ = deltaSeconds;
        _gl!.Clear(ClearBufferMask.ColorBufferBit);
        _totalFrames++;
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
