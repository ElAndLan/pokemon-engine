using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

public sealed record BattleFormChoice(string FormId, int MoveIndex);
public sealed record BattleMenuItem(string Label, BattleAction Action);
public sealed record BattlePartyMember(string Name, int CurrentHp, int MaxHp, bool IsActive, bool IsFainted);
public sealed record BattleSceneSnapshot(
    string PlayerName,
    int PlayerHp,
    int PlayerMaxHp,
    string EnemyName,
    int EnemyHp,
    int EnemyMaxHp,
    IReadOnlyList<BattlePartyMember> PlayerParty,
    IReadOnlyList<BattlePartyMember> EnemyParty,
    IReadOnlyList<BattleMenuItem> Menu,
    int SelectedIndex,
    IReadOnlyList<string> RecentLog,
    BattleOutcome? Outcome);

public sealed class BattleScene
{
    private readonly BattleController _battle;
    private readonly Func<BattleController, BattleAction, BattleAction> _enemyAction;
    private readonly IReadOnlyList<BattleFormChoice> _formChoices;
    private readonly Func<EntityId, string> _nameOf;
    private readonly List<string> _presented = [];
    private readonly List<BattleEvent> _events = [];
    private List<BattleMenuItem> _menu = [];
    private int _selected;

    public BattleScene(BattleController battle, Func<BattleController, BattleAction> enemyAction,
        IReadOnlyList<BattleFormChoice>? formChoices = null, Func<EntityId, string>? nameOf = null)
        : this(battle, (b, _) => enemyAction(b), formChoices, nameOf) { }

    public BattleScene(BattleController battle, Func<BattleController, BattleAction, BattleAction> enemyAction,
        IReadOnlyList<BattleFormChoice>? formChoices = null, Func<EntityId, string>? nameOf = null)
    {
        _battle = battle;
        _enemyAction = enemyAction;
        _formChoices = formChoices ?? [];
        _nameOf = nameOf ?? (id => id.ToString());
        RefreshMenu();
    }

    public IReadOnlyList<BattleMenuItem> Menu => _menu;
    public int SelectedIndex => _selected;
    public IReadOnlyList<string> Presented => _presented;
    public IReadOnlyList<BattleEvent> Events => _events;
    public IReadOnlyList<BattleSlot> PendingReplacementSlots => _battle.PendingReplacementSlots;

    public BattleSceneSnapshot Snapshot()
    {
        BattleCreature player = _battle.Active(BattleSide.Player);
        BattleCreature enemy = _battle.Active(BattleSide.Enemy);
        return new BattleSceneSnapshot(
            _nameOf(player.Species),
            player.CurrentHp,
            player.MaxHp,
            _nameOf(enemy.Species),
            enemy.CurrentHp,
            enemy.MaxHp,
            PartySnapshot(BattleSide.Player),
            PartySnapshot(BattleSide.Enemy),
            _menu,
            _selected,
            _presented.TakeLast(6).ToList(),
            _battle.Outcome);
    }

    public void Update(InputState input)
    {
        if (_battle.Outcome is not null)
            return;

        RefreshMenu();
        if (_menu.Count == 0)
            return;

        if (input.WasPressed(GameAction.Up))
            _selected = (_selected + _menu.Count - 1) % _menu.Count;
        if (input.WasPressed(GameAction.Down))
            _selected = (_selected + 1) % _menu.Count;
        if (input.WasPressed(GameAction.Confirm))
            Submit(_menu[_selected].Action);
    }

    private IReadOnlyList<BattlePartyMember> PartySnapshot(BattleSide side)
    {
        int active = _battle.ActiveIndex(side);
        return _battle.Party(side)
            .Select((c, i) => new BattlePartyMember(_nameOf(c.Species), c.CurrentHp, c.MaxHp, i == active, c.IsFainted))
            .ToList();
    }

    private void RefreshMenu()
    {
        var items = new List<BattleMenuItem>();
        BattleCreature active = _battle.Active(BattleSide.Player);
        for (int i = 0; i < active.Moves.Count; i++)
        {
            var action = new UseMove(i);
            if (_battle.CanSubmitAction(BattleSide.Player, action))
                items.Add(new BattleMenuItem($"{_nameOf(active.Moves[i].Move)} PP {active.Moves[i].Pp}", action));
        }

        foreach (BattleFormChoice choice in _formChoices)
        {
            var action = new ActivateForm(choice.FormId, choice.MoveIndex);
            if (_battle.CanSubmitAction(BattleSide.Player, action))
            {
                string move = choice.MoveIndex >= 0 && choice.MoveIndex < active.Moves.Count
                    ? _nameOf(active.Moves[choice.MoveIndex].Move)
                    : $"move {choice.MoveIndex + 1}";
                items.Add(new BattleMenuItem($"{Display(choice.FormId)} + {move}", action));
            }
        }

        IReadOnlyList<BattleCreature> party = _battle.Party(BattleSide.Player);
        for (int i = 0; i < party.Count; i++)
        {
            var action = new Switch(i);
            if (_battle.CanSubmitAction(BattleSide.Player, action))
            {
                BattleCreature c = party[i];
                items.Add(new BattleMenuItem($"Switch: {_nameOf(c.Species)} {c.CurrentHp}/{c.MaxHp}", action));
            }
        }

        _menu = items;
        if (_selected >= _menu.Count)
            _selected = Math.Max(0, _menu.Count - 1);
    }

    public void Submit(BattleAction playerAction)
    {
        Present(_battle.ResolveTurn(playerAction, _enemyAction(_battle, playerAction)));
    }

    public void SubmitReplacements(IReadOnlyList<BattleReplacementSelection> selections)
    {
        Present(_battle.ResolveReplacements(selections));
    }

    private void Present(IReadOnlyList<BattleEvent> events)
    {
        _events.AddRange(events);
        foreach (BattleEvent e in events)
            _presented.Add(BattleEventPresenter.Line(e, _nameOf));
        RefreshMenu();
    }

    private static string Display(string id) => id.Replace('_', ' ');
}
