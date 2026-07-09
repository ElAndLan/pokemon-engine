using Cgm.Core.Battle;

namespace Cgm.Runtime.Engine;

public sealed record BattleFormChoice(string FormId, int MoveIndex);
public sealed record BattleMenuItem(string Label, BattleAction Action);

public sealed class BattleScene
{
    private readonly BattleController _battle;
    private readonly Func<BattleController, BattleAction> _enemyAction;
    private readonly IReadOnlyList<BattleFormChoice> _formChoices;
    private readonly List<string> _presented = [];
    private List<BattleMenuItem> _menu = [];
    private int _selected;

    public BattleScene(BattleController battle, Func<BattleController, BattleAction> enemyAction,
        IReadOnlyList<BattleFormChoice>? formChoices = null)
    {
        _battle = battle;
        _enemyAction = enemyAction;
        _formChoices = formChoices ?? [];
        RefreshMenu();
    }

    public IReadOnlyList<BattleMenuItem> Menu => _menu;
    public int SelectedIndex => _selected;
    public IReadOnlyList<string> Presented => _presented;

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

    private void RefreshMenu()
    {
        var items = new List<BattleMenuItem>();
        BattleCreature active = _battle.Active(BattleSide.Player);
        for (int i = 0; i < active.Moves.Count; i++)
        {
            var action = new UseMove(i);
            if (_battle.CanSubmitAction(BattleSide.Player, action))
                items.Add(new BattleMenuItem(active.Moves[i].Move.ToString(), action));
        }

        foreach (BattleFormChoice choice in _formChoices)
        {
            var action = new ActivateForm(choice.FormId, choice.MoveIndex);
            if (_battle.CanSubmitAction(BattleSide.Player, action))
                items.Add(new BattleMenuItem($"{choice.FormId} + move {choice.MoveIndex + 1}", action));
        }

        _menu = items;
        if (_selected >= _menu.Count)
            _selected = Math.Max(0, _menu.Count - 1);
    }

    public void Submit(BattleAction playerAction)
    {
        IReadOnlyList<BattleEvent> events = _battle.ResolveTurn(playerAction, _enemyAction(_battle));
        foreach (BattleEvent e in events)
            _presented.Add(BattleEventPresenter.Line(e));
        RefreshMenu();
    }
}

public static class BattleEventPresenter
{
    public static string Line(BattleEvent e) => e switch
    {
        FormChanged form => form.FormId is null
            ? $"{form.Side} reverted form"
            : $"{form.Side} changed form to {form.FormId}",
        MoveUsed move => $"{move.Side} used {move.Move}",
        DamageDealt damage => $"{damage.Target} took {damage.Amount} damage",
        Fainted fainted => $"{fainted.Side} fainted",
        BattleEnded ended => $"{ended.Winner} won",
        _ => e.ToString() ?? e.GetType().Name,
    };
}
