using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>The playable battle scene (ENGINE_RUNTIME_SPEC 16F, PHASE_16_DEMO_PLAN §3.2). It renders
/// the slot layout, drives the action menu, and presents Core's events through a paced beat queue.
///
/// It predicts nothing: legal actions come from Core's menu, every visible number comes from a Core
/// snapshot, and HP bars animate a Core-reported value rather than recomputing damage.</summary>
public sealed class BattleHostScene : IScene
{
    private readonly UiPainter _ui;
    private readonly BattleScene _battle;
    private readonly BattleBeatQueue _beats = new();
    private readonly ResourceBar _playerHp;
    private readonly ResourceBar _enemyHp;
    private readonly int _width;
    private readonly int _height;
    private SelectionList _menu;
    private int _presented;

    public BattleHostScene(UiPainter ui, BattleScene battle, int virtualWidth, int virtualHeight)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(battle);
        _ui = ui;
        _battle = battle;
        _width = virtualWidth;
        _height = virtualHeight;

        BattleSceneSnapshot snapshot = battle.Snapshot();
        _playerHp = new ResourceBar(snapshot.PlayerHp, snapshot.PlayerMaxHp);
        _enemyHp = new ResourceBar(snapshot.EnemyHp, snapshot.EnemyMaxHp);
        _menu = BuildMenu(snapshot);
    }

    public bool IsOverlay => false;

    /// <summary>The battle's result once Core declares one; the host returns to the overworld.</summary>
    public BattleOutcome? Outcome => _battle.Snapshot().Outcome;

    public bool Finished => Outcome is not null && !_beats.IsPresenting;

    public int SelectedIndex => _menu.Selected ?? 0;

    /// <summary>True while events are still being presented; the action menu is hidden then.</summary>
    public bool IsPresenting => _beats.IsPresenting;

    public IReadOnlyList<string> Log => _beats.Shown;

    public void Enter() => Drain();

    public void Update(TickInput input)
    {
        if (_beats.IsPresenting)
        {
            // Presentation owns input while it runs: Confirm skips a beat, holding it speeds up.
            if (input.WasPressed(GameAction.Confirm))
                _beats.Confirm();
            _beats.Tick(input.IsDown(GameAction.Confirm));
            _playerHp.Tick();
            _enemyHp.Tick();
            return;
        }

        if (Outcome is not null)
            return;

        foreach (GameAction direction in (GameAction[])[GameAction.Up, GameAction.Down])
            if (input.WasPressed(direction))
                _menu.Move(direction);

        if (input.WasPressed(GameAction.Confirm))
            Submit();
    }

    public void Render()
    {
        _ui.Panel(new RectI(0, 0, _width, _height), new Rgba(0x18, 0x24, 0x30, 0xFF));
        BattleSceneSnapshot snapshot = _battle.Snapshot();

        // Opponent upper-right on its ground, info panel upper-left (no numeric HP for opponents).
        _ui.Panel(new RectI(_width - 84, 30, 48, 40), new Rgba(0x90, 0x50, 0x40, 0xFF), layer: 1);
        Info(new RectI(8, 12, 108, 34), snapshot.EnemyName, _enemyHp, showNumbers: false);

        // Player lower-left, info panel lower-right with numeric HP.
        _ui.Panel(new RectI(36, _height - 108, 48, 40), new Rgba(0x40, 0x70, 0x90, 0xFF), layer: 1);
        Info(new RectI(_width - 124, _height - 96, 116, 34), snapshot.PlayerName, _playerHp, showNumbers: true);

        Message(snapshot);
    }

    public void Exit() => _beats.Clear();

    public void Dispose() { }

    private void Info(RectI bounds, string name, ResourceBar hp, bool showNumbers)
    {
        _ui.Panel(bounds, new Rgba(0xE8, 0xE4, 0xD0, 0xFF), layer: 2);
        _ui.Text(name, bounds.X + 6, bounds.Y + 5, new Rgba(0x18, 0x18, 0x18, 0xFF), layer: 3);

        var track = new RectI(bounds.X + 6, bounds.Y + 18, bounds.Width - 12, 4);
        _ui.Panel(track, new Rgba(0x40, 0x38, 0x30, 0xFF), layer: 3);
        _ui.Panel(track with { Width = hp.FillPixels(track.Width) }, HpColour(hp), layer: 4);

        if (showNumbers)
            _ui.Text($"{hp.Displayed}/{hp.Max}", bounds.X + 6, bounds.Y + 24,
                new Rgba(0x18, 0x18, 0x18, 0xFF), layer: 3);
    }

    /// <summary>The bottom panel shows the presenting line, or the action menu when idle.</summary>
    private void Message(BattleSceneSnapshot snapshot)
    {
        var box = new RectI(8, _height - 52, _width - 16, 44);
        _ui.Panel(box, new Rgba(0xE8, 0xE4, 0xD0, 0xFF), layer: 5);
        var ink = new Rgba(0x18, 0x18, 0x18, 0xFF);

        if (_beats.Current is { } line)
        {
            _ui.TextBlock(_ui.Font.Wrap(line, box.Width - 16), box.X + 8, box.Y + 8, ink, layer: 6);
            return;
        }

        if (Outcome is not null)
        {
            _ui.Text("BATTLE OVER", box.X + 8, box.Y + 8, ink, layer: 6);
            return;
        }

        for (int i = 0; i < snapshot.Menu.Count && i < 4; i++)
        {
            int y = box.Y + 6 + i * _ui.Font.LineHeight;
            _ui.Text(snapshot.Menu[i].Label, box.X + 18, y, ink, layer: 6);
            if (_menu.Selected == i)
                _ui.Cursor(box.X + 8, y, new Rgba(0xC0, 0x40, 0x30, 0xFF), layer: 6);
        }
    }

    private static Rgba HpColour(ResourceBar hp) => hp.Max <= 0 || hp.Displayed * 2 > hp.Max
        ? new Rgba(0x40, 0xC0, 0x50, 0xFF)
        : hp.Displayed * 5 > hp.Max
            ? new Rgba(0xE0, 0xC0, 0x40, 0xFF)
            : new Rgba(0xD0, 0x50, 0x40, 0xFF);

    private void Submit()
    {
        BattleSceneSnapshot snapshot = _battle.Snapshot();
        if (_menu.Selected is not { } index || index >= snapshot.Menu.Count)
            return;

        _battle.Submit(snapshot.Menu[index].Action);
        Drain();
    }

    /// <summary>Queues every event Core produced since the last drain, in order. Replacements are
    /// resolved first so the battle never stalls waiting for a choice the demo cannot yet offer.</summary>
    private void Drain()
    {
        if (_battle.PendingReplacementSlots.Count > 0)
            SubmitReplacements();

        for (; _presented < _battle.Events.Count; _presented++)
            _beats.Enqueue(BattleEventPresenter.Line(_battle.Events[_presented]));

        BattleSceneSnapshot snapshot = _battle.Snapshot();
        _playerHp.Set(snapshot.PlayerHp, snapshot.PlayerMaxHp);
        _enemyHp.Set(snapshot.EnemyHp, snapshot.EnemyMaxHp);
        _menu = BuildMenu(snapshot);
    }

    private void SubmitReplacements()
    {
        BattleSceneSnapshot snapshot = _battle.Snapshot();
        var choices = new List<BattleReplacementSelection>();
        foreach (BattleSlot slot in _battle.PendingReplacementSlots)
        {
            IReadOnlyList<BattlePartyMember> party = slot.Side == BattleSide.Player
                ? snapshot.PlayerParty
                : snapshot.EnemyParty;
            for (int i = 0; i < party.Count; i++)
                if (!party[i].IsActive && !party[i].IsFainted)
                {
                    choices.Add(new BattleReplacementSelection(slot, i));
                    break;
                }
        }
        if (choices.Count > 0)
            _battle.SubmitReplacements(choices);
    }

    private static SelectionList BuildMenu(BattleSceneSnapshot snapshot) =>
        new(Enumerable.Repeat(true, Math.Max(1, snapshot.Menu.Count)));
}
