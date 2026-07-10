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
    private readonly InputState _inputState = new();

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
        _inputState.Update(ReadHeldActions());
        _game?.ShowcaseBattle.Update(_inputState);

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
        _gl.ClearColor(0.10f, 0.12f, 0.13f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        Vector2D<int> size = _window!.FramebufferSize;
        int w = size.X;
        int h = size.Y;
        BattleSceneSnapshot s = _game!.ShowcaseBattle.Snapshot();

        FillRect(0, h / 2, w, h / 2, 0.18f, 0.28f, 0.23f);
        FillRect(0, 0, w, h / 2, 0.12f, 0.15f, 0.17f);
        FillRect(w / 18, h * 5 / 8, w / 3, h / 11, 0.70f, 0.82f, 0.56f);
        FillRect(w * 11 / 18, h * 3 / 8, w / 3, h / 11, 0.75f, 0.36f, 0.25f);

        DrawCreaturePanel(28, 24, w * 2 / 5, s.EnemyName, s.EnemyHp, s.EnemyMaxHp, s.EnemyParty);
        DrawCreaturePanel(w / 2 + 36, h / 2 - 28, w * 2 / 5, s.PlayerName, s.PlayerHp, s.PlayerMaxHp, s.PlayerParty);

        int menuTop = h * 11 / 16;
        FillRect(18, menuTop, w / 2 - 28, h - menuTop - 18, 0.92f, 0.89f, 0.78f);
        FillRect(w / 2 + 8, menuTop, w / 2 - 26, h - menuTop - 18, 0.13f, 0.15f, 0.16f);
        for (int i = 0; i < s.Menu.Count && i < 7; i++)
        {
            string prefix = i == s.SelectedIndex ? "> " : "  ";
            DrawText(32, menuTop + 24 + i * 24, prefix + s.Menu[i].Label, 0.08f, 0.09f, 0.10f, 3);
        }

        DrawText(w / 2 + 26, menuTop + 22, "EVENT LOG", 0.86f, 0.88f, 0.80f, 3);
        for (int i = 0; i < s.RecentLog.Count; i++)
            DrawText(w / 2 + 26, menuTop + 52 + i * 22, s.RecentLog[i], 0.78f, 0.82f, 0.74f, 2);
        if (s.Outcome is not null)
            DrawText(w / 2 - 112, h / 2 - 18, $"{s.Outcome.Winner} wins", 0.95f, 0.93f, 0.78f, 4);

        _gl.Disable(EnableCap.ScissorTest);
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

    private void DrawCreaturePanel(int x, int y, int width, string name, int hp, int maxHp, IReadOnlyList<BattlePartyMember> party)
    {
        FillRect(x, y, width, 88, 0.92f, 0.89f, 0.78f);
        DrawText(x + 16, y + 14, name, 0.08f, 0.09f, 0.10f, 3);
        DrawText(x + width - 126, y + 14, $"{hp}/{maxHp}", 0.08f, 0.09f, 0.10f, 3);
        int barWidth = width - 32;
        FillRect(x + 16, y + 48, barWidth, 12, 0.28f, 0.25f, 0.21f);
        int hpWidth = maxHp <= 0 ? 0 : Math.Max(0, barWidth * hp / maxHp);
        FillRect(x + 16, y + 48, hpWidth, 12, 0.12f, 0.55f, 0.24f);
        for (int i = 0; i < party.Count; i++)
        {
            BattlePartyMember member = party[i];
            float r = member.IsFainted ? 0.35f : member.IsActive ? 0.95f : 0.58f;
            float g = member.IsFainted ? 0.35f : member.IsActive ? 0.78f : 0.58f;
            float b = member.IsFainted ? 0.35f : member.IsActive ? 0.20f : 0.58f;
            FillRect(x + 18 + i * 26, y + 68, 16, 10, r, g, b);
        }
    }

    private void FillRect(int x, int y, int width, int height, float r, float g, float b)
    {
        if (width <= 0 || height <= 0)
            return;

        Vector2D<int> size = _window!.FramebufferSize;
        if (x >= size.X || y >= size.Y)
            return;

        int clampedX = Math.Max(0, x);
        int clampedY = Math.Max(0, y);
        int clampedWidth = Math.Min(width - (clampedX - x), size.X - clampedX);
        int clampedHeight = Math.Min(height - (clampedY - y), size.Y - clampedY);
        if (clampedWidth <= 0 || clampedHeight <= 0)
            return;

        int glY = Math.Max(0, size.Y - clampedY - clampedHeight);
        _gl!.Enable(EnableCap.ScissorTest);
        _gl.Scissor(clampedX, glY, (uint)clampedWidth, (uint)clampedHeight);
        _gl.ClearColor(r, g, b, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    private void DrawText(int x, int y, string text, float r, float g, float b, int scale)
    {
        int cursor = x;
        foreach (char ch in text.ToUpperInvariant())
        {
            if (cursor > _window!.FramebufferSize.X - 6 * scale)
                break;
            DrawGlyph(cursor, y, ch, r, g, b, scale);
            cursor += 6 * scale;
        }
    }

    private void DrawGlyph(int x, int y, char ch, float r, float g, float b, int scale)
    {
        if (!Font.TryGetValue(ch, out int[]? rows))
            rows = Font['?'];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if ((rows[row] & (1 << (4 - col))) != 0)
                    FillRect(x + col * scale, y + row * scale, scale, scale, r, g, b);
            }
        }
    }

    private static readonly IReadOnlyDictionary<char, int[]> Font = new Dictionary<char, int[]>
    {
        [' '] = [0, 0, 0, 0, 0, 0, 0],
        ['?'] = [14, 17, 1, 2, 4, 0, 4],
        ['>'] = [16, 8, 4, 2, 4, 8, 16],
        [':'] = [0, 4, 4, 0, 4, 4, 0],
        ['/'] = [1, 1, 2, 4, 8, 16, 16],
        ['-'] = [0, 0, 0, 31, 0, 0, 0],
        ['+'] = [0, 4, 4, 31, 4, 4, 0],
        ['0'] = [14, 17, 19, 21, 25, 17, 14],
        ['1'] = [4, 12, 4, 4, 4, 4, 14],
        ['2'] = [14, 17, 1, 2, 4, 8, 31],
        ['3'] = [30, 1, 1, 14, 1, 1, 30],
        ['4'] = [2, 6, 10, 18, 31, 2, 2],
        ['5'] = [31, 16, 16, 30, 1, 1, 30],
        ['6'] = [14, 16, 16, 30, 17, 17, 14],
        ['7'] = [31, 1, 2, 4, 8, 8, 8],
        ['8'] = [14, 17, 17, 14, 17, 17, 14],
        ['9'] = [14, 17, 17, 15, 1, 1, 14],
        ['A'] = [14, 17, 17, 31, 17, 17, 17],
        ['B'] = [30, 17, 17, 30, 17, 17, 30],
        ['C'] = [14, 17, 16, 16, 16, 17, 14],
        ['D'] = [30, 17, 17, 17, 17, 17, 30],
        ['E'] = [31, 16, 16, 30, 16, 16, 31],
        ['F'] = [31, 16, 16, 30, 16, 16, 16],
        ['G'] = [14, 17, 16, 23, 17, 17, 15],
        ['H'] = [17, 17, 17, 31, 17, 17, 17],
        ['I'] = [14, 4, 4, 4, 4, 4, 14],
        ['J'] = [7, 2, 2, 2, 18, 18, 12],
        ['K'] = [17, 18, 20, 24, 20, 18, 17],
        ['L'] = [16, 16, 16, 16, 16, 16, 31],
        ['M'] = [17, 27, 21, 21, 17, 17, 17],
        ['N'] = [17, 25, 21, 19, 17, 17, 17],
        ['O'] = [14, 17, 17, 17, 17, 17, 14],
        ['P'] = [30, 17, 17, 30, 16, 16, 16],
        ['Q'] = [14, 17, 17, 17, 21, 18, 13],
        ['R'] = [30, 17, 17, 30, 20, 18, 17],
        ['S'] = [15, 16, 16, 14, 1, 1, 30],
        ['T'] = [31, 4, 4, 4, 4, 4, 4],
        ['U'] = [17, 17, 17, 17, 17, 17, 14],
        ['V'] = [17, 17, 17, 17, 17, 10, 4],
        ['W'] = [17, 17, 17, 21, 21, 21, 10],
        ['X'] = [17, 17, 10, 4, 10, 17, 17],
        ['Y'] = [17, 17, 10, 4, 4, 4, 4],
        ['Z'] = [31, 1, 2, 4, 8, 16, 31],
    };

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
