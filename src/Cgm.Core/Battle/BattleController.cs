using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Battle v0 turn engine (1v1): validates both sides' actions, orders by priority/speed, resolves
/// each move through the formula layer with a fixed RNG draw order (accuracy → crit → damage roll),
/// applies damage, and ends on a faint. Deterministic given the seed + actions — the basis for
/// golden replays. UI submits <see cref="BattleAction"/>s and consumes the <see cref="BattleEvent"/>
/// stream; it never mutates state directly (ADR / CLAUDE.md).
/// </summary>
public sealed class BattleController
{
    private readonly BattleCreature[] _actives; // [0]=Player, [1]=Enemy
    private readonly TypeChart _chart;
    private readonly IRng _rng;
    private readonly List<BattleEvent> _log = [];

    public BattleController(BattleCreature player, BattleCreature enemy, TypeChart chart, IRng rng)
    {
        _actives = [player, enemy];
        _chart = chart;
        _rng = rng;
    }

    public int Turn { get; private set; }
    public BattleOutcome? Outcome { get; private set; }
    public IReadOnlyList<BattleEvent> Log => _log;
    public BattleCreature Active(BattleSide side) => _actives[(int)side];

    /// <summary>Resolves one full turn and returns the events it produced.</summary>
    public IReadOnlyList<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction enemyAction)
    {
        if (Outcome is not null)
            throw new InvalidOperationException("The battle is already over.");

        int pMove = Validate(BattleSide.Player, playerAction);
        int eMove = Validate(BattleSide.Enemy, enemyAction);
        int start = _log.Count;

        bool playerFirst = TurnOrder.AFirst(
            _actives[0].Moves[pMove].Priority, _actives[0].Stats.Spe,
            _actives[1].Moves[eMove].Priority, _actives[1].Stats.Spe, _rng);

        if (playerFirst)
        {
            ResolveMove(BattleSide.Player, pMove);
            ResolveMove(BattleSide.Enemy, eMove);
        }
        else
        {
            ResolveMove(BattleSide.Enemy, eMove);
            ResolveMove(BattleSide.Player, pMove);
        }

        Turn++;
        CheckEnd();
        return _log.GetRange(start, _log.Count - start);
    }

    private int Validate(BattleSide side, BattleAction action)
    {
        if (action is not UseMove use)
            throw new ArgumentException($"Unsupported action for {side}.", nameof(action));

        BattleCreature c = _actives[(int)side];
        if (use.MoveIndex < 0 || use.MoveIndex >= c.Moves.Count)
            throw new ArgumentException($"{side} move index {use.MoveIndex} out of range.", nameof(action));
        if (!c.Moves[use.MoveIndex].HasPp)
            throw new ArgumentException($"{side} move {use.MoveIndex} has no PP.", nameof(action));

        return use.MoveIndex;
    }

    private void ResolveMove(BattleSide side, int moveIndex)
    {
        BattleCreature attacker = _actives[(int)side];
        BattleSide targetSide = Opponent(side);
        BattleCreature target = _actives[(int)targetSide];

        if (attacker.IsFainted || target.IsFainted)
            return; // fainted attacker can't act; no valid target

        BattleMove move = attacker.Moves[moveIndex];
        move.UsePp();
        _log.Add(new MoveUsed(side, move.Move));

        if (move.Power is not int power)
            return; // status move — no damage in v0

        // Fixed RNG draw order: accuracy → crit → damage roll.
        if (!BattleRolls.Hits(move.Accuracy, _rng))
        {
            _log.Add(new MoveMissed(side, move.Move));
            return;
        }

        bool crit = BattleRolls.IsCrit(move.CritStage, _rng);
        int roll = BattleRolls.DamageRoll(_rng);

        bool physical = move.DamageClass == DamageClass.Physical;
        int a = physical ? attacker.Stats.Atk : attacker.Stats.Spa;
        int d = physical ? target.Stats.Def : target.Stats.Spd;
        double eff = _chart.Effectiveness(move.Type, target.Types);
        double stab = TypeChart.Stab(move.Type, attacker.Types);

        int dmg = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn: false);
        target.TakeDamage(dmg);
        _log.Add(new DamageDealt(targetSide, dmg, eff, crit));

        if (target.IsFainted)
            _log.Add(new Fainted(targetSide));
    }

    private void CheckEnd()
    {
        bool playerDown = _actives[0].IsFainted;
        bool enemyDown = _actives[1].IsFainted;
        if (!playerDown && !enemyDown)
            return;

        // In v0 exactly one side can faint per turn (a fainted attacker never acts).
        BattleSide winner = playerDown ? BattleSide.Enemy : BattleSide.Player;
        Outcome = new BattleOutcome(winner);
        _log.Add(new BattleEnded(winner));
    }

    private static BattleSide Opponent(BattleSide s) => s == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
