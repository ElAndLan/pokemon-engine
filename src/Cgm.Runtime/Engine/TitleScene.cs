namespace Cgm.Runtime.Engine;

/// <summary>What the player chose at the title screen. The scene reports intent; the host owns the
/// transition, so Title never needs to know what Overworld is.</summary>
public enum TitleChoice { None, NewGame, Continue }

/// <summary>Title screen: New Game, and Continue when a save exists (ENGINE_RUNTIME_SPEC 16C flow
/// Boot → Title → New/Continue → Overworld). Entry labels are supplied by the caller, so the scene
/// names no content and display text stays data.</summary>
public sealed class TitleScene : IScene
{
    private readonly UiPainter _ui;
    private readonly int _width;
    private readonly int _height;
    private readonly string _title;
    private readonly IReadOnlyList<string> _labels;
    private readonly SelectionList _entries;

    public TitleScene(UiPainter ui, int virtualWidth, int virtualHeight, string title,
        bool continueAvailable, IReadOnlyList<string>? labels = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        _width = virtualWidth;
        _height = virtualHeight;
        _title = title ?? "";
        _labels = labels ?? ["NEW GAME", "CONTINUE"];
        // Continue stays visible but unselectable with no save, rather than vanishing.
        _entries = new SelectionList([true, continueAvailable]);
        ContinueAvailable = continueAvailable;
    }

    public bool ContinueAvailable { get; }

    public bool IsOverlay => false;

    /// <summary>Set once the player confirms; the host consumes it and transitions.</summary>
    public TitleChoice Choice { get; private set; }

    public int? SelectedIndex => _entries.Selected;

    public void Enter() => Choice = TitleChoice.None;

    public void Update(TickInput input)
    {
        if (Choice != TitleChoice.None)
            return; // already decided; the host is mid-transition

        foreach (GameAction direction in (GameAction[])[GameAction.Up, GameAction.Down])
            if (input.WasPressed(direction))
                _entries.Move(direction);

        if (input.WasPressed(GameAction.Confirm) && _entries.Selected is { } index)
            Choice = index == 0 ? TitleChoice.NewGame : TitleChoice.Continue;
    }

    public void Render()
    {
        _ui.Panel(new RectI(0, 0, _width, _height), new Rgba(0x10, 0x14, 0x1C, 0xFF));
        _ui.Text(_title, 16, 24, new Rgba(0xF0, 0xEC, 0xD8, 0xFF));

        int top = _height / 2;
        for (int i = 0; i < _labels.Count; i++)
        {
            bool enabled = _entries.IsEnabled(i);
            // Disabled entries stay visible and dimmed, never hidden.
            var colour = enabled ? new Rgba(0xF0, 0xEC, 0xD8, 0xFF) : new Rgba(0x60, 0x60, 0x60, 0xFF);
            int y = top + i * _ui.Font.LineHeight * 2;
            _ui.Text(_labels[i], 32, y, colour);
            if (_entries.Selected == i)
                _ui.Cursor(20, y, new Rgba(0xF0, 0xC0, 0x40, 0xFF));
        }
    }

    public void Exit() { }

    public void Dispose() { }
}

/// <summary>The pause menu, pushed as an overlay so the scene beneath keeps rendering
/// (ENGINE_RUNTIME_SPEC 16C). Cancel closes it and returns focus to whatever opened it.</summary>
public sealed class MenuScene : IScene
{
    private readonly UiPainter _ui;
    private readonly int _width;
    private readonly int _height;
    private readonly IReadOnlyList<string> _labels;
    private readonly SelectionList _entries;

    public MenuScene(UiPainter ui, int virtualWidth, int virtualHeight, IReadOnlyList<string> labels,
        IReadOnlyList<bool>? enabled = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(labels);
        _ui = ui;
        _width = virtualWidth;
        _height = virtualHeight;
        _labels = labels;
        _entries = new SelectionList(enabled ?? Enumerable.Repeat(true, labels.Count), wrap: true);
    }

    public bool IsOverlay => true;

    /// <summary>True once Cancel was pressed; the host pops the overlay on the next tick.</summary>
    public bool Closed { get; private set; }

    /// <summary>The entry confirmed this session, or null if none.</summary>
    public int? Chosen { get; private set; }

    public int? SelectedIndex => _entries.Selected;

    public void Enter()
    {
        Closed = false;
        Chosen = null;
    }

    public void Update(TickInput input)
    {
        if (Closed)
            return;

        foreach (GameAction direction in (GameAction[])[GameAction.Up, GameAction.Down])
            if (input.WasPressed(direction))
                _entries.Move(direction);

        // An empty control still accepts Cancel, so a menu can never trap the player.
        if (input.WasPressed(GameAction.Cancel))
            Closed = true;
        else if (input.WasPressed(GameAction.Confirm) && _entries.Selected is { } index)
            Chosen = index;
    }

    public void Render()
    {
        var bounds = new RectI(_width / 2, 8, _width / 2 - 8, _height - 16);
        _ui.Panel(bounds, new Rgba(0x20, 0x24, 0x30, 0xF0));
        for (int i = 0; i < _labels.Count; i++)
        {
            bool enabled = _entries.IsEnabled(i);
            var colour = enabled ? new Rgba(0xF0, 0xEC, 0xD8, 0xFF) : new Rgba(0x60, 0x60, 0x60, 0xFF);
            int y = bounds.Y + 8 + i * _ui.Font.LineHeight;
            _ui.Text(_labels[i], bounds.X + 14, y, colour);
            if (_entries.Selected == i)
                _ui.Cursor(bounds.X + 4, y, new Rgba(0xF0, 0xC0, 0x40, 0xFF));
        }
    }

    public void Exit() { }

    public void Dispose() { }
}
