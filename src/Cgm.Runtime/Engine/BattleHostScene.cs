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
    private SelectionList? _replacements;
    private int _presented;

    private readonly Func<EntityId, bool>? _spendItem;

    /// <summary><paramref name="spendItem"/> consumes an item an action requires and returns whether
    /// the bag could pay. A refused spend cancels the action, so a device cannot be thrown twice.</summary>
    public BattleHostScene(UiPainter ui, BattleScene battle, int virtualWidth, int virtualHeight,
        Func<EntityId, bool>? spendItem = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(battle);
        _ui = ui;
        _battle = battle;
        _spendItem = spendItem;
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

        // A forced replacement cannot be cancelled: the player must send someone in.
        if (IsChoosingReplacement)
        {
            foreach (GameAction direction in (GameAction[])[GameAction.Up, GameAction.Down])
                if (input.WasPressed(direction))
                    _replacements!.Move(direction);

            if (input.WasPressed(GameAction.Confirm))
                ConfirmReplacement();
            return;
        }

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

        // A fainted creature fills zero pixels; an empty bar draws nothing rather than a zero-width
        // quad, which the batch rejects outright.
        int fill = hp.FillPixels(track.Width);
        if (fill > 0)
            _ui.Panel(track with { Width = fill }, HpColour(hp), layer: 4);

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

        if (IsChoosingReplacement)
        {
            IReadOnlyList<int> options = ReplacementOptions;
            IReadOnlyList<BattlePartyMember> party = snapshot.PlayerParty;
            for (int i = 0; i < options.Count && i < 4; i++)
            {
                BattlePartyMember member = party[options[i]];
                int y = box.Y + 6 + i * _ui.Font.LineHeight;
                _ui.Text($"{member.Name} {member.CurrentHp}/{member.MaxHp}", box.X + 18, y, ink, layer: 6);
                if (_replacements!.Selected == i)
                    _ui.Cursor(box.X + 8, y, new Rgba(0xC0, 0x40, 0x30, 0xFF), layer: 6);
            }
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

        BattleMenuItem chosen = snapshot.Menu[index];

        // Spend before submitting: a device the bag cannot pay for is not thrown at all, rather than
        // thrown and then billed.
        if (chosen.Item is { } item && _spendItem is not null && !_spendItem(item))
            return;

        _battle.Submit(chosen.Action);
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

    /// <summary>The eligible reserves for the slot currently awaiting a player choice: party members
    /// that are neither active nor fainted, in party order.</summary>
    public IReadOnlyList<int> ReplacementOptions
    {
        get
        {
            if (AwaitingReplacement is not { } slot || slot.Side != BattleSide.Player)
                return [];
            IReadOnlyList<BattlePartyMember> party = _battle.Snapshot().PlayerParty;
            return [.. Enumerable.Range(0, party.Count)
                .Where(i => !party[i].IsActive && !party[i].IsFainted)];
        }
    }

    /// <summary>The player slot waiting on a replacement choice, or null when none is.</summary>
    public BattleSlot? AwaitingReplacement { get; private set; }

    /// <summary>True while the player must choose who comes in; the action menu is replaced by the
    /// reserve list. Never true mid-presentation: the player sees the faint before being asked to
    /// answer it, and Confirm keeps meaning "skip this beat" until the log has caught up.</summary>
    public bool IsChoosingReplacement =>
        AwaitingReplacement is not null && !_beats.IsPresenting && ReplacementOptions.Count > 0;

    /// <summary>Fills every pending slot. The player picks their own replacement; the opponent's is
    /// chosen automatically, because Core owns that side's decisions and the player never sees a
    /// menu for it.</summary>
    private void SubmitReplacements()
    {
        BattleSceneSnapshot snapshot = _battle.Snapshot();
        var choices = new List<BattleReplacementSelection>();

        foreach (BattleSlot slot in _battle.PendingReplacementSlots)
        {
            if (slot.Side == BattleSide.Player)
            {
                // Defer to the player unless there is no choice to make.
                IReadOnlyList<int> options = Eligible(snapshot.PlayerParty);
                if (options.Count > 1)
                {
                    AwaitingReplacement = slot;
                    _replacements = new SelectionList(Enumerable.Repeat(true, options.Count));
                    return;   // nothing is submitted until the player answers
                }
                if (options.Count == 1)
                    choices.Add(new BattleReplacementSelection(slot, options[0]));
                continue;
            }

            IReadOnlyList<int> enemy = Eligible(snapshot.EnemyParty);
            if (enemy.Count > 0)
                choices.Add(new BattleReplacementSelection(slot, enemy[0]));
        }

        if (choices.Count > 0)
            _battle.SubmitReplacements(choices);
    }

    /// <summary>Sends in the reserve the player selected.</summary>
    private void ConfirmReplacement()
    {
        if (AwaitingReplacement is not { } slot || _replacements?.Selected is not { } choice)
            return;

        IReadOnlyList<int> options = ReplacementOptions;
        if (choice >= options.Count)
            return;

        AwaitingReplacement = null;
        _replacements = null;
        _battle.SubmitReplacements([new BattleReplacementSelection(slot, options[choice])]);
        Drain();
    }

    private static IReadOnlyList<int> Eligible(IReadOnlyList<BattlePartyMember> party) =>
        [.. Enumerable.Range(0, party.Count).Where(i => !party[i].IsActive && !party[i].IsFainted)];

    private static SelectionList BuildMenu(BattleSceneSnapshot snapshot) =>
        new(Enumerable.Repeat(true, Math.Max(1, snapshot.Menu.Count)));
}
