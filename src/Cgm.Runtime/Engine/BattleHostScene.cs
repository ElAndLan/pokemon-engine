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
    /// <summary>Which battle panel currently owns input: the four-way root, or a sub-screen.</summary>
    private enum Panel { Root, Fight, Party, Items }

    private static readonly string[] RootLabels = ["FIGHT", "PARTY", "ITEMS", "RUN"];

    private readonly UiPainter _ui;
    private readonly BattleScene _battle;
    private readonly BattleBeatQueue _beats = new();
    private readonly ResourceBar _playerHp;
    private readonly ResourceBar _enemyHp;
    private readonly int _width;
    private readonly int _height;

    private Panel _panel = Panel.Root;
    private SelectionList _root;      // FIGHT / PARTY / ITEMS / RUN, 2x2
    private SelectionList _sub;       // the active sub-panel's list
    // Sub-panel entries map a display row back to the Core-validated menu action.
    private IReadOnlyList<int> _fight = [];    // snapshot.Menu indices that are UseMove
    private IReadOnlyList<int> _items = [];    // snapshot.Menu indices that are ThrowBall / UseBattleItem
    private IReadOnlyList<int> _party = [];    // party indices, in party order (all shown)
    private int _runMenuIndex = -1;            // snapshot.Menu index of the Run action, or -1
    private SelectionList? _replacements;
    private int _presented;

    private readonly Func<EntityId, bool>? _spendItem;
    private readonly SpriteAtlas? _sprites;
    private readonly Func<EntityId, SpeciesSprites?>? _speciesSprites;

    /// <summary><paramref name="spendItem"/> consumes an item an action requires and returns whether
    /// the bag could pay. A refused spend cancels the action, so a device cannot be thrown twice.
    /// <paramref name="sprites"/> and <paramref name="speciesSprites"/> draw the combatants; without
    /// them the scene falls back to the coloured platform markers.</summary>
    public BattleHostScene(UiPainter ui, BattleScene battle, int virtualWidth, int virtualHeight,
        Func<EntityId, bool>? spendItem = null, SpriteAtlas? sprites = null,
        Func<EntityId, SpeciesSprites?>? speciesSprites = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(battle);
        _ui = ui;
        _battle = battle;
        _spendItem = spendItem;
        _sprites = sprites;
        _speciesSprites = speciesSprites;
        _width = virtualWidth;
        _height = virtualHeight;

        BattleSceneSnapshot snapshot = battle.Snapshot();
        _playerHp = new ResourceBar(snapshot.PlayerHp, snapshot.PlayerMaxHp);
        _enemyHp = new ResourceBar(snapshot.EnemyHp, snapshot.EnemyMaxHp);
        _root = _sub = new SelectionList([true]);
        RebuildMenus(snapshot);
    }

    public bool IsOverlay => false;

    /// <summary>The battle's result once Core declares one; the host returns to the overworld.</summary>
    public BattleOutcome? Outcome => _battle.Snapshot().Outcome;

    public bool Finished => Outcome is not null && !_beats.IsPresenting;

    public int SelectedIndex => (_panel == Panel.Root ? _root.Selected : _sub.Selected) ?? 0;

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

        SelectionList active = _panel == Panel.Root ? _root : _sub;
        foreach (GameAction direction in (GameAction[])[GameAction.Up, GameAction.Down, GameAction.Left, GameAction.Right])
            if (input.WasPressed(direction))
                active.Move(direction);

        if (input.WasPressed(GameAction.Confirm))
            Confirm();
        else if (input.WasPressed(GameAction.Cancel) && _panel != Panel.Root)
            _panel = Panel.Root;   // back out of a sub-panel to the four-way menu
    }

    public void Render()
    {
        _ui.Panel(new RectI(0, 0, _width, _height), new Rgba(0x18, 0x24, 0x30, 0xFF));
        BattleSceneSnapshot snapshot = _battle.Snapshot();

        // Opponent upper-right on its ground platform, info panel upper-left (no numeric HP).
        var enemyGround = new RectI(_width - 84, 30, 48, 40);
        _ui.Panel(enemyGround, new Rgba(0x90, 0x50, 0x40, 0xFF), layer: 1);
        DrawCombatant(FrontOf(snapshot.EnemySpecies), enemyGround);
        Info(new RectI(8, 12, 108, 34), snapshot.EnemyName, _enemyHp, showNumbers: false);

        // Player lower-left, info panel lower-right with numeric HP.
        var playerGround = new RectI(36, _height - 108, 48, 40);
        _ui.Panel(playerGround, new Rgba(0x40, 0x70, 0x90, 0xFF), layer: 1);
        DrawCombatant(BackOf(snapshot.PlayerSpecies), playerGround);
        Info(new RectI(_width - 124, _height - 96, 116, 34), snapshot.PlayerName, _playerHp, showNumbers: true);

        Message(snapshot);
    }

    private EntityId? FrontOf(EntityId species) => _speciesSprites?.Invoke(species)?.Front;
    private EntityId? BackOf(EntityId species) => _speciesSprites?.Invoke(species)?.Back;

    /// <summary>Draws a combatant sprite centred over its ground platform and standing on it (feet at
    /// the platform's vertical centre), on layer 2 so it sits above the platform. Draws nothing when
    /// there is no atlas or the sprite does not resolve — the platform marker then stands in.</summary>
    private void DrawCombatant(EntityId? sprite, RectI ground)
    {
        if (_sprites is null || sprite is not { } id || !_sprites.TryGet(id, out TextureHandle texture, out RectI source))
            return;

        var dest = new RectI(
            ground.X + (ground.Width - source.Width) / 2,
            ground.Y + ground.Height / 2 - source.Height,
            source.Width, source.Height);
        _ui.Sprite(texture, source, dest, layer: 2);
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

        switch (_panel)
        {
            case Panel.Root: DrawRoot(box, ink); break;
            case Panel.Fight: DrawActionList(box, ink, _fight, snapshot); break;
            case Panel.Items: DrawActionList(box, ink, _items, snapshot); break;
            case Panel.Party: DrawParty(box, ink, snapshot); break;
        }
    }

    private static readonly Rgba Cursor = new(0xC0, 0x40, 0x30, 0xFF);
    private static readonly Rgba Disabled = new(0xA0, 0x9C, 0x90, 0xFF);

    /// <summary>The four-way root menu as a 2x2 grid: FIGHT / PARTY over ITEMS / RUN.</summary>
    private void DrawRoot(RectI box, Rgba ink)
    {
        bool[] enabled = [_fight.Count > 0, _party.Count > 0, _items.Count > 0, _runMenuIndex >= 0];
        int colW = (box.Width - 24) / 2;
        for (int i = 0; i < 4; i++)
        {
            int x = box.X + 20 + i % 2 * colW;
            int y = box.Y + 6 + i / 2 * _ui.Font.LineHeight;
            _ui.Text(RootLabels[i], x, y, enabled[i] ? ink : Disabled, layer: 6);
            if (_root.Selected == i)
                _ui.Cursor(x - 10, y, Cursor, layer: 6);
        }
    }

    /// <summary>A one-column list of the given menu actions (moves, or items), with the cursor.</summary>
    private void DrawActionList(RectI box, Rgba ink, IReadOnlyList<int> map, BattleSceneSnapshot snapshot)
    {
        for (int i = 0; i < map.Count && i < 4; i++)
        {
            int y = box.Y + 6 + i * _ui.Font.LineHeight;
            _ui.Text(snapshot.Menu[map[i]].Label, box.X + 18, y, ink, layer: 6);
            if (_sub.Selected == i)
                _ui.Cursor(box.X + 8, y, Cursor, layer: 6);
        }
    }

    /// <summary>The party list: name + HP for each member; the active one is marked, switchable
    /// members are ink, and the rest (active or fainted) are greyed.</summary>
    private void DrawParty(RectI box, Rgba ink, BattleSceneSnapshot snapshot)
    {
        IReadOnlyList<BattlePartyMember> party = snapshot.PlayerParty;
        for (int i = 0; i < party.Count && i < 4; i++)
        {
            BattlePartyMember m = party[i];
            int y = box.Y + 6 + i * _ui.Font.LineHeight;
            string tag = m.IsActive ? "* " : m.IsFainted ? "x " : "  ";
            _ui.Text($"{tag}{m.Name}  {m.CurrentHp}/{m.MaxHp}", box.X + 18, y,
                CanSwitchTo(i) ? ink : Disabled, layer: 6);
            if (_sub.Selected == i)
                _ui.Cursor(box.X + 8, y, Cursor, layer: 6);
        }
    }

    private static Rgba HpColour(ResourceBar hp) => hp.Max <= 0 || hp.Displayed * 2 > hp.Max
        ? new Rgba(0x40, 0xC0, 0x50, 0xFF)
        : hp.Displayed * 5 > hp.Max
            ? new Rgba(0xE0, 0xC0, 0x40, 0xFF)
            : new Rgba(0xD0, 0x50, 0x40, 0xFF);

    /// <summary>Acts on the current panel's selection: the root opens a sub-panel (or runs), a
    /// sub-panel submits the chosen Core action.</summary>
    private void Confirm()
    {
        if (_panel == Panel.Root)
        {
            OpenRootChoice();
            return;
        }

        // Sub-panel: map the selected row to a Core-validated menu action and submit it.
        int? menuIndex = _panel switch
        {
            Panel.Fight => Index(_fight, _sub.Selected),
            Panel.Items => Index(_items, _sub.Selected),
            Panel.Party => PartySwitchMenuIndex(_sub.Selected),
            _ => null,
        };
        if (menuIndex is { } index)
            SubmitMenu(index);
    }

    private void OpenRootChoice()
    {
        switch (_root.Selected)
        {
            case 0 when _fight.Count > 0: OpenPanel(Panel.Fight, _fight.Count); break;
            case 1: OpenPanel(Panel.Party, Math.Max(1, _party.Count),
                enabled: [.. _party.Select(CanSwitchTo)]); break;
            case 2 when _items.Count > 0: OpenPanel(Panel.Items, _items.Count); break;
            case 3 when _runMenuIndex >= 0: SubmitMenu(_runMenuIndex); break;
        }
    }

    private void OpenPanel(Panel panel, int count, IReadOnlyList<bool>? enabled = null)
    {
        _panel = panel;
        _sub = new SelectionList(enabled ?? [.. Enumerable.Repeat(true, count)]);
    }

    /// <summary>Submits a menu action, spending its item first so a device the bag cannot pay for is
    /// not used at all, then returns to the root panel and drains the resulting events.</summary>
    private void SubmitMenu(int index)
    {
        IReadOnlyList<BattleMenuItem> menu = _battle.Snapshot().Menu;
        if (index < 0 || index >= menu.Count)
            return;

        BattleMenuItem chosen = menu[index];
        if (chosen.Item is { } item && _spendItem is not null && !_spendItem(item))
            return;

        _panel = Panel.Root;
        _battle.Submit(chosen.Action);
        Drain();
    }

    private static int? Index(IReadOnlyList<int> map, int? row) =>
        row is { } r && r >= 0 && r < map.Count ? map[r] : null;

    /// <summary>The Switch menu index for the party row, if that member can be switched in.</summary>
    private int? PartySwitchMenuIndex(int? row)
    {
        if (row is not { } r || r < 0 || r >= _party.Count || !CanSwitchTo(_party[r]))
            return null;
        IReadOnlyList<BattleMenuItem> menu = _battle.Snapshot().Menu;
        for (int i = 0; i < menu.Count; i++)
            if (menu[i].Action is Switch s && s.PartyIndex == _party[r])
                return i;
        return null;
    }

    private bool CanSwitchTo(int partyIndex)
    {
        IReadOnlyList<BattleMenuItem> menu = _battle.Snapshot().Menu;
        return menu.Any(item => item.Action is Switch s && s.PartyIndex == partyIndex);
    }

    /// <summary>Categorises the Core-validated action menu into the sub-panels and (re)builds the
    /// four-way root, disabling any choice with nothing behind it.</summary>
    private void RebuildMenus(BattleSceneSnapshot snapshot)
    {
        var fight = new List<int>();
        var items = new List<int>();
        _runMenuIndex = -1;
        for (int i = 0; i < snapshot.Menu.Count; i++)
        {
            switch (snapshot.Menu[i].Action)
            {
                case UseMove or ActivateForm or UseFallback: fight.Add(i); break;
                case ThrowBall or UseBattleItem: items.Add(i); break;
                case Run: _runMenuIndex = i; break;
            }
        }
        _fight = fight;
        _items = items;
        _party = [.. Enumerable.Range(0, snapshot.PlayerParty.Count)];

        bool[] rootEnabled = [_fight.Count > 0, _party.Count > 0, _items.Count > 0, _runMenuIndex >= 0];
        _root = new SelectionList(rootEnabled, columns: 2);
        if (_panel != Panel.Root && (_panel switch
        {
            Panel.Fight => _fight.Count == 0,
            Panel.Items => _items.Count == 0,
            _ => false,
        }))
            _panel = Panel.Root;   // the sub-panel emptied out (last move's PP, last ball); fall back
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
        RebuildMenus(snapshot);
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
}
