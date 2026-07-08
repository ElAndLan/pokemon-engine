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
    private readonly int[] _spikeLayers = [0, 0];    // entry-hazard side condition (catalog §7.3), per side
    private readonly bool[] _stealthRock = [false, false]; // type-scaled entry hazard, per side
    private Weather _weather = Weather.None;          // field condition (catalog §7.6)
    private int _weatherTurns;
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
                if (Active(side).IsTrapped)
                    throw new ArgumentException($"{side} is trapped and cannot switch.");
                if (Active(side).IsCharging)
                    throw new ArgumentException($"{side} is charging a move and cannot switch.");
                if (Active(side).IsLocked)
                    throw new ArgumentException($"{side} is locked into a move and cannot switch.");
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
        if (action is Switch sw)
            SwitchTo(side, sw.PartyIndex);
    }

    /// <summary>Brings a side's party member into play — the shared path for voluntary, forced (Roar),
    /// and faint-replacement switches: outgoing loses stat stages + volatiles, then the on_switch_in
    /// hazards fire on the incoming creature.</summary>
    private void SwitchTo(BattleSide side, int index)
    {
        BattleCreature outgoing = Active(side);
        outgoing.ResetStages();     // stat stages don't carry across a switch
        outgoing.ClearVolatiles();  // confusion/flinch/trap/etc. clear on switch-out
        _active[(int)side] = index;
        _log.Add(new SwitchedIn(side, index));
        OnSwitchIn(side);
    }

    private void ResolveMoves(BattleAction playerAction, BattleAction enemyAction)
    {
        // Flinch and Protect last only for a turn; clear last turn's before this turn's moves resolve.
        Active(BattleSide.Player).ClearFlinch();
        Active(BattleSide.Enemy).ClearFlinch();
        Active(BattleSide.Player).ClearProtected();
        Active(BattleSide.Enemy).ClearProtected();
        Active(BattleSide.Player).ResetDamageTaken(); // Counter/Mirror Coat only see this turn's hits
        Active(BattleSide.Enemy).ResetDamageTaken();

        bool pMove = playerAction is UseMove;
        bool eMove = enemyAction is UseMove;
        // A charging creature is locked into its two-turn move, so its effective action is that move.
        int pIndex = EffectiveMoveIndex(BattleSide.Player, (playerAction as UseMove)?.MoveIndex ?? -1);
        int eIndex = EffectiveMoveIndex(BattleSide.Enemy, (enemyAction as UseMove)?.MoveIndex ?? -1);

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
            TickRampageLock(first);
            ResolveMove(second, secondIdx);
            TickRampageLock(second);
        }
        else if (pMove)
        {
            ResolveMove(BattleSide.Player, pIndex);
            TickRampageLock(BattleSide.Player);
        }
        else if (eMove)
        {
            ResolveMove(BattleSide.Enemy, eIndex);
            TickRampageLock(BattleSide.Enemy);
        }
    }

    /// <summary>Counts a rampage (Thrash/Outrage) down after its move resolved — whether it hit, missed,
    /// or was blocked. When the lock ends, the user confuses itself.</summary>
    private void TickRampageLock(BattleSide side)
    {
        BattleCreature c = Active(side);
        if (!c.IsLocked)
            return;
        c.TickLock();
        if (!c.IsLocked && !c.IsFainted && !c.IsConfused)
        {
            c.SetConfusion(VolatileEffects.ConfusionDuration(_rng));
            _log.Add(new Confused(side));
        }
    }

    private int Speed(BattleSide side)
    {
        BattleCreature c = Active(side);
        double m = StatusEffects.SpeedMultiplier(c.Status) * StatStages.Multiplier(c.Stage(StatKind.Spe));
        return (int)(c.Stats.Spe * m);
    }

    /// <summary>A charging or rampaging creature is locked into its move; otherwise the submitted index stands.</summary>
    private int EffectiveMoveIndex(BattleSide side, int submitted)
    {
        BattleCreature c = Active(side);
        if (c.IsCharging) return c.ChargingMoveIndex!.Value;
        if (c.IsLocked) return c.LockedMoveIndex;
        return submitted;
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
        if (!move.IsProtect)
            attacker.ResetProtectChain(); // any non-protect move breaks the protect chain

        // Two-turn move: turn 1 charges (PP spent now, no damage); turn 2 fires as a normal hit.
        bool firing = attacker.IsCharging;
        if (move.ChargeTurn && !firing)
        {
            move.UsePp();
            attacker.StartCharging(moveIndex);
            _log.Add(new Charging(side, move.Move));
            return;
        }
        if (firing)
            attacker.StopCharging();

        // Thrash/Outrage: first use locks the creature in for 2–3 turns; the lock ticks after resolution.
        bool continuingLock = attacker.IsLocked;
        if (move.MultiTurnLock && !continuingLock)
            attacker.StartLock(moveIndex, _rng.Next(2, 4));

        // PP is spent only on the first turn — not while firing a charge or continuing a rampage lock.
        if (!firing && !continuingLock)
            move.UsePp();
        _log.Add(new MoveUsed(side, move.Move));

        // Protect: a move aimed at a shielded target is blocked outright (PP already spent).
        if (target.Protected && TargetsOpponent(move))
        {
            _log.Add(new MoveBlocked(side));
            return;
        }

        // OHKO uses a level-scaled accuracy in place of the move's own; accuracyBypass sure-hits.
        int? accuracy = move.Ohko ? EffectMath.OhkoAccuracy(attacker.Level, target.Level) : move.Accuracy;
        if (!move.BypassAccuracy && !BattleRolls.Hits(accuracy, _rng))
        {
            _log.Add(new MoveMissed(side, move.Move));
            if (move.Recoil is { } crash && move.RecoilOnMiss)
                Sap(attacker, side, EffectMath.CrashDamage(attacker.MaxHp, crash.Num, crash.Den), amt => new Recoiled(side, amt));
            return;
        }

        int damageDealt = 0;
        if (move.CounterCategory is { } counterCat)
        {
            // Counter/Mirror Coat: return 2× the damage of that category taken this turn (no draw).
            int received = counterCat == DamageClass.Physical ? attacker.PhysicalDamageTaken : attacker.SpecialDamageTaken;
            if (received > 0)
            {
                int dmg = received * 2;
                target.TakeDamage(dmg);
                damageDealt = dmg;
                _log.Add(new DamageDealt(targetSide, dmg, 1.0, Crit: false));
                if (target.IsFainted)
                    _log.Add(new Fainted(targetSide));
            }
            else
            {
                _log.Add(new MoveMissed(side, move.Move)); // nothing to counter → fizzles
            }
        }
        else if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
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
                target.RecordDamageTaken(move.DamageClass, dmg); // for Counter/Mirror Coat
                damageDealt += dmg;
                _log.Add(new DamageDealt(targetSide, dmg, eff, crit));
            }

            if (target.IsFainted)
                _log.Add(new Fainted(targetSide));
        }

        // Effect-list-driven resolution (EFFECT_TYPES_CATALOG): iterate the move's ordered effects and
        // dispatch each to a shared primitive. Order matches the historical pipeline (target secondaries,
        // then leech/drain/heal/recoil/crit/faint), so the RNG draw order is unchanged.
        var ctx = new EffectContext(move, attacker, side, target, targetSide, damageDealt);
        foreach (MoveEffect effect in move.SecondaryEffects)
            ApplyEffect(ctx, effect);
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

    /// <summary>Dispatches one compiled effect to its shared primitive (ResolvePrimitive,
    /// EFFECT_TYPES_CATALOG §4.1). Each primitive keeps the historical chance-roll semantics so draw
    /// order is preserved.</summary>
    private void ApplyEffect(EffectContext ctx, MoveEffect effect)
    {
        switch (effect)
        {
            case AilmentEffect a: ApplyAilment(ctx, a); break;
            case StatChangeEffect s: ApplyStatChange(ctx, s); break;
            case ConfusionEffect c: ApplyConfusion(ctx, c); break;
            case FlinchEffect f: ApplyFlinch(ctx, f); break;
            case LeechSeedEffect: ApplyLeechSeed(ctx); break;
            case BindEffect when !ctx.Target.IsFainted && !ctx.Target.IsTrapped:
                ctx.Target.SetTrap(_rng.Next(4, 6)); // partial trap lasts 4–5 turns
                _log.Add(new Bound(ctx.TargetSide));
                break;
            case ProtectEffect:
                if (VolatileEffects.ProtectSucceeds(ctx.Source.ProtectChain, _rng))
                {
                    ctx.Source.SetProtected();
                    _log.Add(new Protected(ctx.SourceSide));
                }
                else
                {
                    ctx.Source.ResetProtectChain();
                    _log.Add(new ProtectFailed(ctx.SourceSide));
                }
                break;
            case ForceSwitchEffect when !ctx.Target.IsFainted:
                ForceSwitch(ctx.TargetSide);
                break;
            case DrainEffect d when ctx.DamageDealt > 0:
                Heal(ctx.Source, ctx.SourceSide, EffectMath.DrainHeal(ctx.DamageDealt, d.Fraction.Num, d.Fraction.Den));
                break;
            case HealEffect h:
                Heal(ctx.Source, ctx.SourceSide, EffectMath.HealAmount(ctx.Source.MaxHp, h.Fraction.Num, h.Fraction.Den));
                break;
            case RecoilEffect r when ctx.DamageDealt > 0:
                Sap(ctx.Source, ctx.SourceSide, EffectMath.RecoilDamage(ctx.DamageDealt, r.Fraction.Num, r.Fraction.Den),
                    amt => new Recoiled(ctx.SourceSide, amt));
                break;
            case CritBoostEffect cb:
                ctx.Source.RaiseCrit(cb.Stages);
                _log.Add(new CritBoosted(ctx.SourceSide));
                break;
            case SelfDestructEffect when !ctx.Source.IsFainted:
                ctx.Source.TakeDamage(ctx.Source.MaxHp);
                _log.Add(new Fainted(ctx.SourceSide));
                break;
            case EntryHazardEffect:
                int layers = _spikeLayers[(int)ctx.TargetSide] = Math.Min(3, _spikeLayers[(int)ctx.TargetSide] + 1);
                _log.Add(new HazardSet(ctx.TargetSide, layers));
                break;
            case StealthRockEffect when !_stealthRock[(int)ctx.TargetSide]:
                _stealthRock[(int)ctx.TargetSide] = true;
                _log.Add(new StealthRockSet(ctx.TargetSide));
                break;
            case SetWeatherEffect w when w.Weather != _weather:
                _weather = w.Weather;
                _weatherTurns = WeatherConditions.DefaultTurns;
                _log.Add(new WeatherChanged(w.Weather));
                break;
        }
    }

    private static readonly EntityId RockType = EntityId.Parse("type:rock");

    /// <summary>on_switch_in hook (catalog §7.3): a creature entering a side with entry hazards takes
    /// damage — Stealth Rock (type-scaled) before Spikes (layer-scaled). Draws no RNG. Battle-start
    /// actives never trigger it (no hazards yet).</summary>
    private void OnSwitchIn(BattleSide side)
    {
        BattleCreature c = Active(side);
        if (c.IsFainted)
            return;

        if (_stealthRock[(int)side])
        {
            double eff = _chart.Effectiveness(RockType, c.Types);
            Sap(c, side, EffectMath.TypeScaledHazardDamage(c.MaxHp, eff), amt => new HurtByHazard(side, amt));
        }
        if (!c.IsFainted && _spikeLayers[(int)side] > 0)
            Sap(c, side, EffectMath.HazardDamage(c.MaxHp, _spikeLayers[(int)side]), amt => new HurtByHazard(side, amt));
    }

    /// <summary>Whether a move acts on the opposing creature (so Protect can block it). Damage or any
    /// target-directed secondary counts; self-buffs, heals, weather, and side hazards do not.</summary>
    private static bool TargetsOpponent(BattleMove move) =>
        move.Power.HasValue || move.SecondaryEffects.Any(e =>
            e is AilmentEffect or ConfusionEffect or FlinchEffect or LeechSeedEffect or BindEffect or ForceSwitchEffect
            || (e is StatChangeEffect s && !s.OnSelf));

    /// <summary>Roar/Whirlwind: a wild target flees (battle ends); a trainer's is dragged out to a random
    /// healthy reserve. No reserve → no effect.</summary>
    private void ForceSwitch(BattleSide side)
    {
        if (IsWild && side == BattleSide.Enemy)
        {
            Outcome = new BattleOutcome(Opponent(side)); // scared the wild creature off
            _log.Add(new ForcedOut(side));
            _log.Add(new BattleEnded(Opponent(side)));
            return;
        }

        var reserves = new List<int>();
        for (int i = 0; i < _parties[(int)side].Count; i++)
            if (i != _active[(int)side] && !_parties[(int)side][i].IsFainted)
                reserves.Add(i);
        if (reserves.Count == 0)
            return; // nothing to drag out

        _log.Add(new ForcedOut(side));
        SwitchTo(side, reserves[_rng.Next(reserves.Count)]);
    }

    private void ApplyLeechSeed(EffectContext ctx)
    {
        if (ctx.Target.IsFainted || ctx.Target.Seeded)
            return;
        if (ctx.Target.Types.Contains(ctx.Move.Type)) // a seed of its own type can't take hold (grass immune to grass Leech Seed)
            return;
        ctx.Target.SetSeeded(true);
        _log.Add(new LeechSeeded(ctx.TargetSide));
    }

    private void ApplyAilment(EffectContext ctx, AilmentEffect effect)
    {
        if (ctx.Target.IsFainted)
            return;
        if (!StatusEffects.CanApplyStatus(ctx.Target.Status) || StatusEffects.TypeImmuneToStatus(effect.Status, ctx.Target.Types))
            return;
        if (_rng.Next(100) < effect.Chance)
        {
            ctx.Target.SetStatus(effect.Status);
            _log.Add(new StatusApplied(ctx.TargetSide, effect.Status));
        }
    }

    private void ApplyStatChange(EffectContext ctx, StatChangeEffect effect)
    {
        if (effect.Chance < 100 && _rng.Next(100) >= effect.Chance)
            return;
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSide recipientSide = effect.OnSelf ? ctx.SourceSide : ctx.TargetSide;
        if (recipient.IsFainted)
            return;
        recipient.ChangeStage(effect.Stat, effect.Delta);
        _log.Add(new StatStageChanged(recipientSide, effect.Stat, effect.Delta));
    }

    private void ApplyConfusion(EffectContext ctx, ConfusionEffect effect)
    {
        if (ctx.Target.IsFainted || ctx.Target.IsConfused)
            return;
        if (_rng.Next(100) < effect.Chance)
        {
            ctx.Target.SetConfusion(VolatileEffects.ConfusionDuration(_rng));
            _log.Add(new Confused(ctx.TargetSide));
        }
    }

    private void ApplyFlinch(EffectContext ctx, FlinchEffect effect)
    {
        // Flinch only bites if the target hasn't acted yet — resolution order gives us that for free.
        if (!ctx.Target.IsFainted && VolatileEffects.Flinches(effect.Chance, _rng))
            ctx.Target.SetFlinch();
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

        int dmg = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn);
        dmg = (int)(dmg * WeatherConditions.DamageMultiplier(_weather, move.Type.Slug)); // on_damage_query weather modifier
        return (dmg, crit, eff);
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

        // Partial trap (Bind/Wrap/…): residual chip while trapped, then count down and release.
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (c.IsFainted || !c.IsTrapped)
                continue;
            Sap(c, side, Math.Max(1, c.MaxHp / 8), amt => new BoundHurt(side, amt));
            c.TickTrap();
            if (!c.IsTrapped)
                _log.Add(new BindReleased(side));
        }

        WeatherTurnEnd();
    }

    /// <summary>on_turn_end for the weather field condition: residual chip to non-immune actives, then
    /// the duration counts down and expires. Draws no RNG.</summary>
    private void WeatherTurnEnd()
    {
        if (_weather == Weather.None)
            return;

        WeatherDef def = WeatherConditions.For(_weather);
        if (def.ResidualDenominator > 0)
        {
            foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            {
                BattleCreature c = Active(side);
                if (c.IsFainted || c.Types.Any(t => def.ResidualImmuneTypes.Contains(t.Slug)))
                    continue;
                Sap(c, side, Math.Max(1, c.MaxHp / def.ResidualDenominator), amt => new WeatherDamage(side, amt));
            }
        }

        if (--_weatherTurns <= 0)
        {
            _log.Add(new WeatherEnded(_weather));
            _weather = Weather.None;
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
                SwitchTo(side, next);
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
