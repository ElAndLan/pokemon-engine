using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Battle turn engine (v0–v4): parties per side with one active each, action validation, ordering
/// by priority/effective-speed, move resolution through the formula layer with a fixed RNG draw
/// order, statuses/stat-stages, capture, switching, and end-on-party-wipe. Deterministic given the
/// seed + actions (golden replays). UI submits <see cref="BattleAction"/>s and consumes the
/// <see cref="BattleEvent"/> stream; it never mutates state directly.
/// </summary>
public sealed class BattleController
{
    private readonly List<BattleCreature>[] _parties;
    private readonly int[] _active = [0, 0];
    private readonly TypeChart _chart;
    private readonly IRng _rng;
    private readonly List<BattleEvent> _log = [];

    public BattleController(BattleCreature player, BattleCreature enemy, TypeChart chart, IRng rng, bool isWild = false)
        : this([player], [enemy], chart, rng, isWild) { }

    public BattleController(IReadOnlyList<BattleCreature> playerParty, IReadOnlyList<BattleCreature> enemyParty,
        TypeChart chart, IRng rng, bool isWild = false)
    {
        _parties = [[.. playerParty], [.. enemyParty]];
        _chart = chart;
        _rng = rng;
        IsWild = isWild;
    }

    public int Turn { get; private set; }
    public bool IsWild { get; }
    public bool Captured { get; private set; }
    public BattleOutcome? Outcome { get; private set; }
    public IReadOnlyList<BattleEvent> Log => _log;

    public BattleCreature Active(BattleSide side) => _parties[(int)side][_active[(int)side]];

    public IReadOnlyList<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction enemyAction)
    {
        if (Outcome is not null)
            throw new InvalidOperationException("The battle is already over.");

        int start = _log.Count;
        Validate(BattleSide.Player, playerAction);
        Validate(BattleSide.Enemy, enemyAction);

        // 1. Switches happen before anything else.
        ApplySwitch(BattleSide.Player, playerAction);
        ApplySwitch(BattleSide.Enemy, enemyAction);

        // 2. Capture (wild, player only).
        if (playerAction is ThrowBall ball)
            ResolveCapture(ball);

        // 3. Moves, ordered by priority then effective speed.
        if (Outcome is null)
            ResolveMoves(playerAction, enemyAction);

        // 4. End-of-turn residuals, then auto-replace any fainted actives with reserves.
        if (Outcome is null)
        {
            EndOfTurn();
            AutoReplaceFainted();
        }

        Turn++;
        CheckEnd();
        return _log.GetRange(start, _log.Count - start);
    }

    private void Validate(BattleSide side, BattleAction action)
    {
        switch (action)
        {
            case UseMove use:
                BattleCreature c = Active(side);
                if (use.MoveIndex < 0 || use.MoveIndex >= c.Moves.Count)
                    throw new ArgumentException($"{side} move index {use.MoveIndex} out of range.");
                if (!c.Moves[use.MoveIndex].HasPp)
                    throw new ArgumentException($"{side} move {use.MoveIndex} has no PP.");
                break;

            case Switch sw:
                List<BattleCreature> party = _parties[(int)side];
                if (sw.PartyIndex < 0 || sw.PartyIndex >= party.Count)
                    throw new ArgumentException($"{side} switch index {sw.PartyIndex} out of range.");
                if (sw.PartyIndex == _active[(int)side])
                    throw new ArgumentException($"{side} is already on party member {sw.PartyIndex}.");
                if (party[sw.PartyIndex].IsFainted)
                    throw new ArgumentException($"{side} cannot switch to a fainted member.");
                break;

            case ThrowBall when side != BattleSide.Player:
                throw new ArgumentException("Only the player can throw a ball.");
            case ThrowBall when !IsWild:
                throw new ArgumentException("Cannot capture in a trainer battle.");
            case ThrowBall:
                break;

            default:
                throw new ArgumentException($"Unsupported action for {side}.");
        }
    }

    private void ApplySwitch(BattleSide side, BattleAction action)
    {
        if (action is not Switch sw)
            return;
        Active(side).ResetStages();     // stat stages don't carry across a switch
        Active(side).ClearVolatiles();  // confusion/flinch clear on switch-out
        _active[(int)side] = sw.PartyIndex;
        _log.Add(new SwitchedIn(side, sw.PartyIndex));
    }

    private void ResolveMoves(BattleAction playerAction, BattleAction enemyAction)
    {
        // Flinch lasts only until the flincher's next action window; clear before this turn's moves.
        Active(BattleSide.Player).ClearFlinch();
        Active(BattleSide.Enemy).ClearFlinch();

        bool pMove = playerAction is UseMove;
        bool eMove = enemyAction is UseMove;
        int pIndex = (playerAction as UseMove)?.MoveIndex ?? -1;
        int eIndex = (enemyAction as UseMove)?.MoveIndex ?? -1;

        if (pMove && eMove)
        {
            bool playerFirst = TurnOrder.AFirst(
                Active(BattleSide.Player).Moves[pIndex].Priority, Speed(BattleSide.Player),
                Active(BattleSide.Enemy).Moves[eIndex].Priority, Speed(BattleSide.Enemy), _rng);

            BattleSide first = playerFirst ? BattleSide.Player : BattleSide.Enemy;
            BattleSide second = Opponent(first);
            int firstIdx = playerFirst ? pIndex : eIndex;
            int secondIdx = playerFirst ? eIndex : pIndex;

            ResolveMove(first, firstIdx);
            ResolveMove(second, secondIdx);
        }
        else if (pMove)
        {
            ResolveMove(BattleSide.Player, pIndex);
        }
        else if (eMove)
        {
            ResolveMove(BattleSide.Enemy, eIndex);
        }
    }

    private int Speed(BattleSide side)
    {
        BattleCreature c = Active(side);
        double m = StatusEffects.SpeedMultiplier(c.Status) * StatStages.Multiplier(c.Stage(StatKind.Spe));
        return (int)(c.Stats.Spe * m);
    }

    private void ResolveMove(BattleSide side, int moveIndex)
    {
        BattleCreature attacker = Active(side);
        BattleSide targetSide = Opponent(side);
        BattleCreature target = Active(targetSide);

        if (attacker.IsFainted || target.IsFainted)
            return;

        if (!CanAct(attacker, side))
            return; // frozen/asleep/fully-paralyzed — no PP spent, no move

        if (attacker.Flinched)
        {
            _log.Add(new Flinched(side));
            return; // flinch costs the turn but no PP
        }

        if (!PushesThroughConfusion(attacker, side))
            return; // hurt itself in confusion — no PP, no move

        BattleMove move = attacker.Moves[moveIndex];
        move.UsePp();
        _log.Add(new MoveUsed(side, move.Move));

        // OHKO uses a level-scaled accuracy in place of the move's own.
        int? accuracy = move.Ohko ? EffectMath.OhkoAccuracy(attacker.Level, target.Level) : move.Accuracy;
        if (!BattleRolls.Hits(accuracy, _rng))
        {
            _log.Add(new MoveMissed(side, move.Move));
            if (move.Recoil is { } crash && move.RecoilOnMiss)
                Sap(attacker, side, EffectMath.CrashDamage(attacker.MaxHp, crash.Num, crash.Den), amt => new Recoiled(side, amt));
            return;
        }

        int damageDealt = 0;
        if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
        {
            // Formula-bypassing hit: no crit/STAB/roll (no RNG draws), but type immunity still voids it.
            double eff = _chart.Effectiveness(move.Type, target.Types);
            int dmg = eff <= 0 ? 0
                : move.Ohko ? target.CurrentHp
                : move.FixedDamageLevel ? attacker.Level
                : move.FixedDamage!.Value;
            target.TakeDamage(dmg);
            damageDealt = dmg;
            _log.Add(new DamageDealt(targetSide, dmg, eff, Crit: false));
            if (target.IsFainted)
                _log.Add(new Fainted(targetSide));
        }
        else if (move.Power is int power)
        {
            // Single-hit is a 1-iteration loop, so the crit→roll draw order is identical to before;
            // HitCount only draws for actual multi-hit moves. Each hit rolls crit/damage independently.
            int hits = move.MultiHitMax >= 2 ? EffectMath.HitCount(_rng, move.MultiHitMin, move.MultiHitMax) : 1;
            for (int h = 0; h < hits && !target.IsFainted; h++)
            {
                (int dmg, bool crit, double eff) = ComputeHit(attacker, target, move, power);
                target.TakeDamage(dmg);
                damageDealt += dmg;
                _log.Add(new DamageDealt(targetSide, dmg, eff, crit));
            }

            if (target.IsFainted)
                _log.Add(new Fainted(targetSide));
        }

        // Secondary effects skip a fainted target (guarded); drain/recoil/heal act on the user.
        TryInflictAilment(move, target, targetSide);
        TryApplyStageEffect(move, attacker, side, target, targetSide);
        TryConfuse(move, target, targetSide);
        TryFlinch(move, target);
        TryLeechSeed(move, target, targetSide);
        ApplyDrainRecoilHeal(move, attacker, side, damageDealt);

        if (move.CritBoost > 0)
        {
            attacker.RaiseCrit(move.CritBoost);
            _log.Add(new CritBoosted(side));
        }

        // Explosion: the user faints after connecting.
        if (move.SelfDestruct && !attacker.IsFainted)
        {
            attacker.TakeDamage(attacker.MaxHp);
            _log.Add(new Fainted(side));
        }
    }

    /// <summary>Confusion pre-move check: counts down, may snap out or force a self-hit.
    /// Returns false only when the creature hurt itself and loses the turn.</summary>
    private bool PushesThroughConfusion(BattleCreature c, BattleSide side)
    {
        if (!c.IsConfused)
            return true;

        c.TickConfusion();
        if (!c.IsConfused)
        {
            _log.Add(new ConfusionEnded(side));
            return true; // snapped out this turn — acts freely
        }

        if (!VolatileEffects.HitsSelfInConfusion(_rng))
            return true;

        int dmg = VolatileEffects.ConfusionSelfDamage(c.Level, c.Stats.Atk, c.Stats.Def);
        c.TakeDamage(dmg);
        _log.Add(new HurtInConfusion(side, dmg));
        if (c.IsFainted)
            _log.Add(new Fainted(side));
        return false;
    }

    private void TryConfuse(BattleMove move, BattleCreature target, BattleSide targetSide)
    {
        if (move.ConfuseChance <= 0 || target.IsFainted || target.IsConfused)
            return;
        if (_rng.Next(100) < move.ConfuseChance)
        {
            target.SetConfusion(VolatileEffects.ConfusionDuration(_rng));
            _log.Add(new Confused(targetSide));
        }
    }

    private void TryFlinch(BattleMove move, BattleCreature target)
    {
        // Flinch only bites if the target hasn't acted yet — resolution order gives us that for free.
        if (!target.IsFainted && VolatileEffects.Flinches(move.FlinchChance, _rng))
            target.SetFlinch();
    }

    private void TryLeechSeed(BattleMove move, BattleCreature target, BattleSide targetSide)
    {
        if (!move.LeechSeed || target.IsFainted || target.Seeded)
            return;
        if (target.Types.Contains(move.Type)) // a seed of its own type can't take hold (grass immune to grass Leech Seed)
            return;
        target.SetSeeded(true);
        _log.Add(new LeechSeeded(targetSide));
    }

    /// <summary>Battle v5 numeric ops on the user: drain (heal from damage), on-hit recoil, and a flat
    /// heal. These draw no RNG, so v0–v4 goldens are unaffected. Amounts come from <see cref="EffectMath"/>;
    /// the actual HP changes route through the shared <see cref="Heal"/>/<see cref="Sap"/> primitives.</summary>
    private void ApplyDrainRecoilHeal(BattleMove move, BattleCreature attacker, BattleSide side, int damageDealt)
    {
        if (move.Drain is { } d && damageDealt > 0)
            Heal(attacker, side, EffectMath.DrainHeal(damageDealt, d.Num, d.Den));

        if (move.Heal is { } h)
            Heal(attacker, side, EffectMath.HealAmount(attacker.MaxHp, h.Num, h.Den));

        if (move.Recoil is { } r && !move.RecoilOnMiss && damageDealt > 0)
            Sap(attacker, side, EffectMath.RecoilDamage(damageDealt, r.Num, r.Den), amt => new Recoiled(side, amt));
    }

    // ---- Reusable effect primitives (shared by every op that heals or saps HP) ----

    /// <summary>Restore HP and log it. No-op if the creature is full or the amount is ≤0. Used by
    /// drain, healFraction, and Leech Seed's beneficiary.</summary>
    private void Heal(BattleCreature c, BattleSide side, int amount)
    {
        if (amount <= 0 || c.CurrentHp >= c.MaxHp)
            return;
        c.Heal(amount);
        _log.Add(new Healed(side, amount));
    }

    /// <summary>Deal non-move HP loss (recoil/crash/leech), log it via the supplied event factory, and
    /// record a faint. Used by recoil, crash-on-miss, and Leech Seed's victim.</summary>
    private void Sap(BattleCreature c, BattleSide side, int amount, Func<int, BattleEvent> lossEvent)
    {
        if (amount <= 0)
            return;
        c.TakeDamage(amount);
        _log.Add(lossEvent(amount));
        if (c.IsFainted)
            _log.Add(new Fainted(side));
    }

    /// <summary>Sap HP from a victim and heal it to a beneficiary — the shared "drain life" primitive
    /// behind Leech Seed (Absorb/Mega Drain use <see cref="Heal"/> against damage already dealt).</summary>
    private void DrainLife(BattleCreature victim, BattleSide victimSide, BattleCreature beneficiary, BattleSide beneficiarySide, int amount)
    {
        Sap(victim, victimSide, amount, amt => new LeechSapped(victimSide, amt));
        Heal(beneficiary, beneficiarySide, amount);
    }

    /// <summary>One hit of a damaging move — draws crit then damage roll (fixed order), applies
    /// crit's stat-stage ignore rule and burn. Returned <c>eff</c> feeds the DamageDealt event.</summary>
    private (int Dmg, bool Crit, double Eff) ComputeHit(BattleCreature attacker, BattleCreature target, BattleMove move, int power)
    {
        bool physical = move.DamageClass == DamageClass.Physical;
        bool crit = BattleRolls.IsCrit(move.CritStage + attacker.CritStageBonus, _rng);
        int roll = BattleRolls.DamageRoll(_rng);

        StatKind offStat = physical ? StatKind.Atk : StatKind.Spa;
        StatKind defStat = physical ? StatKind.Def : StatKind.Spd;
        int aStage = attacker.Stage(offStat);
        int dStage = target.Stage(defStat);
        if (crit)
        {
            aStage = Math.Max(0, aStage);
            dStage = Math.Min(0, dStage);
        }

        int a = (int)((physical ? attacker.Stats.Atk : attacker.Stats.Spa) * StatStages.Multiplier(aStage));
        int d = Math.Max(1, (int)((physical ? target.Stats.Def : target.Stats.Spd) * StatStages.Multiplier(dStage)));
        double eff = _chart.Effectiveness(move.Type, target.Types);
        double stab = TypeChart.Stab(move.Type, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical;

        return (DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn), crit, eff);
    }

    private bool CanAct(BattleCreature c, BattleSide side)
    {
        switch (c.Status)
        {
            case PersistentStatus.Freeze:
                if (StatusEffects.Thaws(_rng)) { c.ClearStatus(); _log.Add(new Thawed(side)); return true; }
                _log.Add(new StillFrozen(side));
                return false;

            case PersistentStatus.Sleep:
                if (c.StatusCounter > 0) { c.TickSleep(); _log.Add(new StillAsleep(side)); return false; }
                c.ClearStatus();
                _log.Add(new WokeUp(side));
                return true;

            case PersistentStatus.Paralysis:
                if (StatusEffects.FullyParalyzed(_rng)) { _log.Add(new FullyParalyzed(side)); return false; }
                return true;

            default:
                return true;
        }
    }

    private void TryInflictAilment(BattleMove move, BattleCreature target, BattleSide targetSide)
    {
        if (move.Ailment is not { } ailment || move.AilmentChance <= 0 || target.IsFainted)
            return;
        if (!StatusEffects.CanApplyStatus(target.Status) || StatusEffects.TypeImmuneToStatus(ailment, target.Types))
            return;

        if (_rng.Next(100) < move.AilmentChance)
        {
            target.SetStatus(ailment);
            _log.Add(new StatusApplied(targetSide, ailment));
        }
    }

    private void TryApplyStageEffect(BattleMove move, BattleCreature attacker, BattleSide side,
        BattleCreature target, BattleSide targetSide)
    {
        if (move.StageEffect is not { } effect || effect.Chance <= 0)
            return;
        if (effect.Chance < 100 && _rng.Next(100) >= effect.Chance)
            return;

        BattleCreature recipient = effect.OnSelf ? attacker : target;
        BattleSide recipientSide = effect.OnSelf ? side : targetSide;
        if (recipient.IsFainted)
            return;

        recipient.ChangeStage(effect.Stat, effect.Delta);
        _log.Add(new StatStageChanged(recipientSide, effect.Stat, effect.Delta));
    }

    private void ResolveCapture(ThrowBall ball)
    {
        BattleCreature target = Active(BattleSide.Enemy);
        _log.Add(new BallThrown());

        CaptureResult result = CaptureCalc.Attempt(
            target.MaxHp, target.CurrentHp, target.CatchRate, ball.BallBonus, ball.StatusBonus, _rng);
        _log.Add(new CaptureShakes(result.Shakes));

        if (result.Caught)
        {
            Captured = true;
            Outcome = new BattleOutcome(BattleSide.Player);
            _log.Add(new Captured(BattleSide.Enemy));
            _log.Add(new BattleEnded(BattleSide.Player));
        }
        else
        {
            _log.Add(new BrokeFree());
        }
    }

    private void EndOfTurn()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (c.IsFainted || c.Status is not { } status)
                continue;

            int dmg = StatusEffects.ResidualDamage(status, c.MaxHp, c.StatusCounter);
            if (dmg > 0)
            {
                c.TakeDamage(dmg);
                _log.Add(new StatusDamage(side, dmg));
                if (c.IsFainted)
                    _log.Add(new Fainted(side));
            }
            c.AdvanceToxic();
        }

        // Leech Seed: each seeded active is sapped and the opposing active recovers that HP.
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (!c.IsFainted && c.Seeded)
                DrainLife(c, side, Active(Opponent(side)), Opponent(side), Math.Max(1, c.MaxHp / 8));
        }
    }

    /// <summary>After a faint, auto-switch a side to its first healthy reserve.</summary>
    // ponytail: auto-picks the first reserve; a UI-driven forced-switch choice is a later refinement.
    private void AutoReplaceFainted()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            if (!Active(side).IsFainted)
                continue;
            int next = _parties[(int)side].FindIndex(c => !c.IsFainted);
            if (next >= 0)
            {
                _active[(int)side] = next;
                Active(side).ResetStages();
                _log.Add(new SwitchedIn(side, next));
            }
        }
    }

    private void CheckEnd()
    {
        if (Outcome is not null)
            return;

        bool playerDown = _parties[0].All(c => c.IsFainted);
        bool enemyDown = _parties[1].All(c => c.IsFainted);
        if (!playerDown && !enemyDown)
            return;

        BattleSide winner = playerDown ? BattleSide.Enemy : BattleSide.Player;
        Outcome = new BattleOutcome(winner);
        _log.Add(new BattleEnded(winner));
    }

    private static BattleSide Opponent(BattleSide s) => s == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
