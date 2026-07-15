using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>A stat-stage change a move applies — to the user or the target, at a given chance.</summary>
public sealed record StageEffect(StatKind Stat, int Delta, bool OnSelf, int Chance);

/// <summary>A chance-gated all-stat stage bundle. It expands to Atk/Def/SpA/SpD/Spe at resolution time.</summary>
public sealed record StageAllEffect(int Delta, bool OnSelf, int Chance);

/// <summary>A rational fraction (num/den) for drain/recoil/heal effect amounts.</summary>
public readonly record struct Fraction(int Num, int Den);

public sealed record TargetHpThresholdPower(Fraction Threshold, Fraction Multiplier, bool Inclusive = true);
public enum HpRatioPowerSource { User, Target }
public enum HpRatioPowerBasis { Current, Missing }
public sealed record HpRatioPower(HpRatioPowerSource Source, HpRatioPowerBasis Basis = HpRatioPowerBasis.Current,
    int? Scale = null, int Offset = 0);
public sealed record HpPowerBand(int UpperInclusive, int Power);
public sealed record HpBandPower(HpRatioPowerSource Source, int Scale, IReadOnlyList<HpPowerBand> Bands);

/// <summary>A move as it exists in battle: static data + remaining PP (BATTLE_SYSTEM_SPEC).</summary>
public sealed class BattleMove
{
    public BattleMove(EntityId move, EntityId type, DamageClass damageClass,
        int? power, int? accuracy, int pp, int priority, int critStage,
        PersistentStatus? ailment = null, int ailmentChance = 0, StageEffect? stageEffect = null,
        int confuseChance = 0, int flinchChance = 0,
        Fraction? drain = null, Fraction? recoil = null, bool recoilOnMiss = false, Fraction? heal = null,
        int multiHitMin = 0, int multiHitMax = 0,
        int? fixedDamage = null, bool fixedDamageLevel = false, bool ohko = false,
        int critBoost = 0, bool selfDestruct = false, bool leechSeed = false, bool setsSpikes = false,
        Weather setsWeather = Weather.None, bool setsStealthRock = false, bool binds = false,
        bool isProtect = false, bool forcesSwitch = false,
        DamageClass? counterCategory = null, bool bypassAccuracy = false, bool chargeTurn = false,
        bool multiTurnLock = false, bool makesContact = false, IReadOnlyList<StageEffect>? stageEffects = null,
        MoveTarget target = MoveTarget.Selected, StageAllEffect? stageAllEffect = null,
        IReadOnlyList<MoveEffect>? secondaryEffects = null,
        StatKind? offensiveStatOverride = null, StatKind? defensiveStatOverride = null,
        TargetHpThresholdPower? targetHpThresholdPower = null, HpRatioPower? hpRatioPower = null,
        HpBandPower? hpBandPower = null)
    {
        Move = move;
        Type = type;
        DamageClass = damageClass;
        Power = power;
        Accuracy = accuracy;
        MaxPp = Pp = pp;
        Priority = priority;
        CritStage = critStage;
        Ailment = ailment;
        AilmentChance = ailmentChance;
        StageEffects = stageEffects ?? (stageEffect is { } singleStage ? [singleStage] : []);
        StageEffect = StageEffects.FirstOrDefault();
        StageAllEffect = stageAllEffect;
        ConfuseChance = confuseChance;
        FlinchChance = flinchChance;
        Drain = drain;
        Recoil = recoil;
        RecoilOnMiss = recoilOnMiss;
        Heal = heal;
        MultiHitMin = multiHitMin;
        MultiHitMax = multiHitMax;
        FixedDamage = fixedDamage;
        FixedDamageLevel = fixedDamageLevel;
        Ohko = ohko;
        CritBoost = critBoost;
        SelfDestruct = selfDestruct;
        LeechSeed = leechSeed;
        SetsSpikes = setsSpikes;
        SetsWeather = setsWeather;
        SetsStealthRock = setsStealthRock;
        Binds = binds;
        IsProtect = isProtect;
        ForcesSwitch = forcesSwitch;
        CounterCategory = counterCategory;
        BypassAccuracy = bypassAccuracy;
        ChargeTurn = chargeTurn;
        MultiTurnLock = multiTurnLock;
        MakesContact = makesContact;
        Target = target;
        OffensiveStatOverride = offensiveStatOverride;
        DefensiveStatOverride = defensiveStatOverride;
        TargetHpThresholdPower = targetHpThresholdPower;
        HpRatioPower = hpRatioPower;
        HpBandPower = hpBandPower;
        SecondaryEffects = secondaryEffects ?? BuildSecondaryEffects();
    }

    private BattleMove(BattleMove source, int pp, int maxPp)
    {
        Move = source.Move;
        Type = source.Type;
        DamageClass = source.DamageClass;
        Power = source.Power;
        Accuracy = source.Accuracy;
        MaxPp = maxPp;
        Pp = Math.Clamp(pp, 0, maxPp);
        Priority = source.Priority;
        CritStage = source.CritStage;
        Ailment = source.Ailment;
        AilmentChance = source.AilmentChance;
        StageEffects = source.StageEffects;
        StageEffect = source.StageEffect;
        StageAllEffect = source.StageAllEffect;
        ConfuseChance = source.ConfuseChance;
        FlinchChance = source.FlinchChance;
        Drain = source.Drain;
        Recoil = source.Recoil;
        RecoilOnMiss = source.RecoilOnMiss;
        Heal = source.Heal;
        MultiHitMin = source.MultiHitMin;
        MultiHitMax = source.MultiHitMax;
        FixedDamage = source.FixedDamage;
        FixedDamageLevel = source.FixedDamageLevel;
        Ohko = source.Ohko;
        CritBoost = source.CritBoost;
        SelfDestruct = source.SelfDestruct;
        LeechSeed = source.LeechSeed;
        SetsSpikes = source.SetsSpikes;
        SetsWeather = source.SetsWeather;
        SetsStealthRock = source.SetsStealthRock;
        Binds = source.Binds;
        IsProtect = source.IsProtect;
        ForcesSwitch = source.ForcesSwitch;
        CounterCategory = source.CounterCategory;
        BypassAccuracy = source.BypassAccuracy;
        ChargeTurn = source.ChargeTurn;
        MultiTurnLock = source.MultiTurnLock;
        MakesContact = source.MakesContact;
        Target = source.Target;
        OffensiveStatOverride = source.OffensiveStatOverride;
        DefensiveStatOverride = source.DefensiveStatOverride;
        TargetHpThresholdPower = source.TargetHpThresholdPower;
        HpRatioPower = source.HpRatioPower;
        HpBandPower = source.HpBandPower;
        SecondaryEffects = source.SecondaryEffects;
    }

    /// <summary>The move's chance-gated secondary effects as an ordered primitive list (the resolver
    /// iterates and dispatches these). Built from the typed params in the historical resolution order —
    /// migration step toward fully data-defined effects (EFFECT_TYPES_CATALOG).</summary>
    private IReadOnlyList<MoveEffect> BuildSecondaryEffects()
    {
        var effects = new List<MoveEffect>();

        // Chance-gated target effects (historical order: ailment, stat stage, confusion, flinch).
        if (Ailment is { } ail && AilmentChance > 0)
            effects.Add(new AilmentEffect(ail) { Chance = AilmentChance });
        foreach (StageEffect st in StageEffects.Where(st => st.Chance > 0))
            effects.Add(new StatChangeEffect(st.Stat, st.Delta, st.OnSelf) { Chance = st.Chance });
        if (StageAllEffect is { Chance: > 0 } all)
            effects.Add(new StatChangeAllEffect(all.Delta, all.OnSelf) { Chance = all.Chance });
        if (ConfuseChance > 0)
            effects.Add(new ConfusionEffect { Chance = ConfuseChance });
        if (FlinchChance > 0)
            effects.Add(new FlinchEffect { Chance = FlinchChance });
        if (Binds)
            effects.Add(new BindEffect());
        if (IsProtect)
            effects.Add(new ProtectEffect());
        if (ForcesSwitch)
            effects.Add(new ForceSwitchEffect());

        // Post-damage, deterministic effects (historical order: hazard, leech, drain, heal, recoil, crit, faint).
        // On-miss crash recoil stays in the miss branch, not here.
        if (SetsSpikes)
            effects.Add(new EntryHazardEffect());
        if (SetsStealthRock)
            effects.Add(new StealthRockEffect());
        if (SetsWeather != Weather.None)
            effects.Add(new SetWeatherEffect(SetsWeather));
        if (LeechSeed)
            effects.Add(new LeechSeedEffect());
        if (Drain is { } dr)
            effects.Add(new DrainEffect(dr));
        if (Heal is { } hl)
            effects.Add(new HealEffect(hl));
        if (Recoil is { } rc && !RecoilOnMiss)
            effects.Add(new RecoilEffect(rc));
        if (CritBoost > 0)
            effects.Add(new CritBoostEffect(CritBoost));
        if (SelfDestruct)
            effects.Add(new SelfDestructEffect());

        return effects;
    }

    public EntityId Move { get; }
    public EntityId Type { get; }
    public DamageClass DamageClass { get; }
    public int? Power { get; }
    public int? Accuracy { get; }
    public int Pp { get; private set; }
    public int MaxPp { get; }
    public int Priority { get; }
    public int CritStage { get; }
    public PersistentStatus? Ailment { get; }
    public int AilmentChance { get; }
    public StageEffect? StageEffect { get; }
    public IReadOnlyList<StageEffect> StageEffects { get; }
    public StageAllEffect? StageAllEffect { get; }
    public int ConfuseChance { get; }
    public int FlinchChance { get; }

    /// <summary>Ordered secondary effects driving resolution (EFFECT_TYPES_CATALOG). The typed fields
    /// above remain for compilation/inspection; this list is what the resolver dispatches.</summary>
    public IReadOnlyList<MoveEffect> SecondaryEffects { get; }

    /// <summary>Battle v5 numeric ops: drain/heal fractions, recoil (on-hit from damage, or on-miss crash
    /// from max HP when <see cref="RecoilOnMiss"/>).</summary>
    public Fraction? Drain { get; }
    public Fraction? Recoil { get; }
    public bool RecoilOnMiss { get; }
    public Fraction? Heal { get; }

    /// <summary>Multi-hit range (≥2 max enables it); the resolver rolls the count per <see cref="EffectMath.HitCount"/>.</summary>
    public int MultiHitMin { get; }
    public int MultiHitMax { get; }

    /// <summary>Formula-bypassing damage: a flat amount, the user's level (<see cref="FixedDamageLevel"/>),
    /// or a level-scaled one-hit KO (<see cref="Ohko"/>). Type immunity still applies.</summary>
    public int? FixedDamage { get; }
    public bool FixedDamageLevel { get; }
    public bool Ohko { get; }

    /// <summary>v5 self-ops: raise the user's crit stage (Focus Energy), and faint the user after
    /// connecting (Explosion).</summary>
    public int CritBoost { get; }
    public bool SelfDestruct { get; }
    public bool LeechSeed { get; }

    /// <summary>Spikes-style entry hazard set on the target's side (catalog §7.3 side condition).</summary>
    public bool SetsSpikes { get; }

    /// <summary>Battlefield weather this move sets (catalog §7.6 field condition); None = not a weather move.</summary>
    public Weather SetsWeather { get; }

    /// <summary>Stealth-Rock-style type-scaled entry hazard set on the target's side (catalog §7.3).</summary>
    public bool SetsStealthRock { get; }

    /// <summary>Partial-trap move (Bind/Wrap/Fire Spin) — traps the target with a residual (catalog §7.2).</summary>
    public bool Binds { get; }

    /// <summary>Protect/Detect — shields the user this turn with success-chain decay (catalog §7.2).</summary>
    public bool IsProtect { get; }

    /// <summary>Roar/Whirlwind — forces the target out (random reserve, or ends a wild battle; catalog §9.6).</summary>
    public bool ForcesSwitch { get; }

    /// <summary>Counter/Mirror Coat — deals 2× the damage of this category taken this turn (catalog §9.2).</summary>
    public DamageClass? CounterCategory { get; }

    /// <summary>Sure-hit: skip the accuracy roll (Swift/Aerial Ace-style accuracyBypass).</summary>
    public bool BypassAccuracy { get; }

    /// <summary>Two-turn move (Solar Beam-style): charges turn 1, strikes turn 2 (catalog §7.2 charge).</summary>
    public bool ChargeTurn { get; }

    /// <summary>Thrash/Outrage — locks the user into this move for 2–3 turns, then self-confuses (catalog §9.3).</summary>
    public bool MultiTurnLock { get; }
    public bool MakesContact { get; }
    public MoveTarget Target { get; }
    public StatKind? OffensiveStatOverride { get; }
    public StatKind? DefensiveStatOverride { get; }
    public TargetHpThresholdPower? TargetHpThresholdPower { get; }
    public HpRatioPower? HpRatioPower { get; }
    public HpBandPower? HpBandPower { get; }

    public bool HasPp => Pp > 0;
    public void UsePp() => Pp = Math.Max(0, Pp - 1);
    public BattleMove WithPpPool(int pp, int maxPp) => new(this, pp, maxPp);
}

/// <summary>A creature as it exists in battle: computed stats, current HP, and its moves. Mutable
/// runtime state, distinct from the species definition and the saved instance.</summary>
public sealed class BattleCreature
{
    private sealed record BattleFormRuntime(
        string FormId,
        FormActivation Activation,
        FormCondition? Condition,
        EntityId? RequiredHeldItem,
        EntityId? RequiredTrainerItem,
        int? Turns,
        int? HpMultiplierPercent,
        Stats Stats,
        IReadOnlyList<EntityId> Types,
        IReadOnlyList<AbilityHook> AbilityHooks,
        IReadOnlyDictionary<EntityId, BattleMove> MoveRemap);

    public BattleCreature(EntityId species, string name, int level,
        IReadOnlyList<EntityId> types, Stats stats, IReadOnlyList<BattleMove> moves, int catchRate = 45,
        IReadOnlyList<AbilityHook>? abilityHooks = null, IReadOnlyList<Effect>? heldItemBattleEffects = null,
        EntityId? heldItem = null, int weightHectograms = 1, int heightDecimeters = 1, int friendship = 70)
    {
        if (weightHectograms <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightHectograms), "Battle weight must be positive.");
        if (heightDecimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(heightDecimeters), "Battle height must be positive.");
        if (friendship is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(friendship), "Battle friendship must be within 0..255.");
        Species = species;
        Name = name;
        Level = level;
        Types = types;
        Stats = stats;
        MaxHp = stats.Hp;
        CurrentHp = stats.Hp;
        Moves = moves;
        CatchRate = catchRate;
        AbilityHooks = abilityHooks ?? [];
        HeldItemBattleEffects = heldItemBattleEffects ?? [];
        HeldItem = heldItem;
        WeightHectograms = weightHectograms;
        HeightDecimeters = heightDecimeters;
        Friendship = friendship;
        _baseStats = stats;
        _baseTypes = types;
        _baseAbilityHooks = AbilityHooks;
        _baseMoves = moves;
    }

    public EntityId Species { get; }
    public string Name { get; }
    public int Level { get; }
    public IReadOnlyList<EntityId> Types { get; private set; }
    public Stats Stats { get; private set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int CatchRate { get; }
    public IReadOnlyList<BattleMove> Moves { get; private set; }
    public IReadOnlyList<AbilityHook> AbilityHooks { get; private set; }
    public IReadOnlyList<Effect> HeldItemBattleEffects { get; }
    public EntityId? HeldItem { get; }
    public int WeightHectograms { get; }
    public int HeightDecimeters { get; }
    public int Friendship { get; }

    public PersistentStatus? Status { get; private set; }
    public int StatusCounter { get; private set; }

    /// <summary>Volatile state (v4): cleared on switch/battle-end, not persisted.</summary>
    public int ConfusionCounter { get; private set; }
    public bool Flinched { get; private set; }
    public bool IsConfused => ConfusionCounter > 0;

    /// <summary>Persistent crit-stage bonus from Focus-Energy-style moves (v5). Volatile.</summary>
    public int CritStageBonus { get; private set; }

    /// <summary>Leech Seed volatile (v5): drained each end-of-turn to the opposing active.</summary>
    public bool Seeded { get; private set; }

    /// <summary>Partial-trap volatile (v5, catalog §7.2): residual each turn + can't switch while &gt; 0.</summary>
    public int TrapTurns { get; private set; }
    public bool IsTrapped => TrapTurns > 0;

    /// <summary>Protect volatile (v5, catalog §7.2 protect_family): shielded this turn, with a chain
    /// counter that halves success on consecutive uses. <see cref="Protected"/> clears each turn.</summary>
    public bool Protected { get; private set; }
    public int ProtectChain { get; private set; }

    /// <summary>Damage taken this turn by category, for Counter/Mirror Coat (catalog §9.2). Reset each turn.</summary>
    public int PhysicalDamageTaken { get; private set; }
    public int SpecialDamageTaken { get; private set; }

    /// <summary>Two-turn move mid-charge (catalog §7.2 charge): the move index locked in until it fires.</summary>
    public int? ChargingMoveIndex { get; private set; }
    public bool IsCharging => ChargingMoveIndex is not null;

    /// <summary>Thrash/Outrage rampage lock (catalog §9.3 outrage_family): forced move + turns left.</summary>
    public int LockedMoveIndex { get; private set; }
    public int LockTurns { get; private set; }
    public bool IsLocked => LockTurns > 0;
    public int? ChoiceLockedMoveIndex { get; private set; }
    public int ActionsSinceSwitch { get; private set; }
    public EntityId? LastMoveUsed { get; private set; }

    private readonly int[] _stages = new int[7]; // atk, def, spa, spd, spe, accuracy, evasion
    private readonly HashSet<string> _consumedHeldEffects = [];
    private readonly Stats _baseStats;
    private readonly IReadOnlyList<EntityId> _baseTypes;
    private readonly IReadOnlyList<AbilityHook> _baseAbilityHooks;
    private readonly IReadOnlyList<BattleMove> _baseMoves;
    private IReadOnlyList<BattleFormRuntime> _forms = [];
    private string? _activeConditionFormId;
    private string? _activeTemporaryFormId;
    private string? _activeTimedFormId;
    private int _timedFormTurns;

    public bool IsFainted => CurrentHp <= 0;

    public static BattleCreature FromInstance(CreatureInstance instance, GameDb db)
    {
        Species species = db.Find<Species>(instance.Species)
            ?? throw new InvalidOperationException($"Unknown species '{instance.Species}'.");
        Form? form = ActivePermanentForm(instance, species);
        var moves = instance.Moves.Select(slot =>
        {
            EntityId moveId = form?.MoveRemap?.TryGetValue(slot.Move, out EntityId remapped) == true ? remapped : slot.Move;
            Move move = db.Find<Move>(moveId)
                ?? throw new InvalidOperationException($"Unknown move '{moveId}'.");
            BattleMove battleMove = MoveCompiler.ToBattleMove(move);
            for (int i = battleMove.Pp; i > slot.Pp; i--)
                battleMove.UsePp();
            return battleMove;
        }).ToList();

        EntityId? abilityId = form?.AbilityOverride
            ?? (instance.Ability is { Length: > 0 } text ? EntityId.Parse(text)
            : species.Abilities.Count > 0 ? species.Abilities[0]
            : null);
        Ability? ability = abilityId is { } id ? db.Find<Ability>(id) : null;
        Item? held = instance.HeldItem is { } heldId ? db.Find<Item>(heldId) : null;

        Stats stats = StatCalc.Compute(form?.StatOverrides ?? species.BaseStats, instance.Ivs, instance.Evs, instance.Nature, instance.Level);
        var creature = new BattleCreature(instance.Species, instance.Nickname ?? species.Name, instance.Level,
            form?.TypeOverrides ?? species.Types, stats, moves, species.CatchRate, ability?.Hooks, held?.BattleEffects,
            instance.HeldItem, species.WeightHectograms, species.HeightDecimeters, instance.Happiness);
        creature._forms = BuildRuntimeForms(species, instance, db, ability?.Hooks ?? []);
        creature.TakeDamage(stats.Hp - Math.Clamp(instance.CurHp, 0, stats.Hp));
        if (instance.Status is { } status)
            creature.SetStatus(status, instance.StatusCounter);
        return creature;
    }

    public (bool Changed, string? FormId) ReevaluateConditionForm(Weather weather)
    {
        if (_activeTemporaryFormId is not null || _activeTimedFormId is not null)
            return (false, null);

        BattleFormRuntime? next = _forms.FirstOrDefault(f =>
            f.Activation == FormActivation.Condition && ConditionMatches(f.Condition, weather));
        if (next?.FormId == _activeConditionFormId)
            return (false, null);

        _activeConditionFormId = next?.FormId;
        ApplyForm(next);
        return (true, next?.FormId);
    }

    public bool CanActivateTemporaryForm(string formId, Func<EntityId, bool> hasTrainerItem)
    {
        BattleFormRuntime? form = _forms.FirstOrDefault(f => f.FormId == formId && f.Activation == FormActivation.BattleTemporary);
        return form is not null
            && _activeTemporaryFormId is null
            && _activeTimedFormId is null
            && HeldItem == form.RequiredHeldItem
            && form.RequiredTrainerItem is { } trainerItem
            && hasTrainerItem(trainerItem);
    }

    public void ActivateTemporaryForm(string formId)
    {
        BattleFormRuntime form = _forms.First(f => f.FormId == formId && f.Activation == FormActivation.BattleTemporary);
        _activeTemporaryFormId = form.FormId;
        _activeConditionFormId = null;
        ApplyForm(form);
    }

    public bool CanActivateTimedForm(string formId)
    {
        BattleFormRuntime? form = _forms.FirstOrDefault(f => f.FormId == formId && f.Activation == FormActivation.BattleTimed);
        return form is not null
            && _activeTemporaryFormId is null
            && _activeTimedFormId is null
            && form.Turns is > 0;
    }

    public void ActivateTimedForm(string formId)
    {
        BattleFormRuntime form = _forms.First(f => f.FormId == formId && f.Activation == FormActivation.BattleTimed);
        _activeTimedFormId = form.FormId;
        _timedFormTurns = form.Turns!.Value;
        _activeConditionFormId = null;
        ApplyForm(form);
    }

    public bool TickTimedForm()
    {
        if (_activeTimedFormId is null || --_timedFormTurns > 0)
            return false;

        _activeTimedFormId = null;
        _timedFormTurns = 0;
        ApplyForm(null);
        return true;
    }

    public bool RevertActiveBattleFormIfFainted()
    {
        return IsFainted && RevertActiveBattleForm();
    }

    public bool RevertActiveBattleForm()
    {
        if (_activeTemporaryFormId is null && _activeTimedFormId is null)
            return false;

        _activeTemporaryFormId = null;
        _activeTimedFormId = null;
        _timedFormTurns = 0;
        ApplyForm(null);
        return true;
    }

    private void ApplyForm(BattleFormRuntime? form)
    {
        int oldMax = MaxHp;
        int oldHp = CurrentHp;

        Stats stats = form?.Stats ?? _baseStats;
        if (form?.HpMultiplierPercent is { } hpMultiplier)
            stats = stats with { Hp = Math.Max(1, stats.Hp * hpMultiplier / 100) };

        Stats = stats;
        Types = form?.Types ?? _baseTypes;
        AbilityHooks = form?.AbilityHooks ?? _baseAbilityHooks;
        ApplyMoveRemap(form);
        MaxHp = Stats.Hp;

        CurrentHp = oldHp <= 0 ? 0 : Math.Max(1, oldHp * MaxHp / oldMax);
    }

    private void ApplyMoveRemap(BattleFormRuntime? form)
    {
        int[] pp = Moves.Select(m => m.Pp).ToArray();
        int[] maxPp = Moves.Select(m => m.MaxPp).ToArray();
        Moves = _baseMoves
            .Select((move, i) => (form?.MoveRemap.TryGetValue(move.Move, out BattleMove? remapped) == true ? remapped : move)
                .WithPpPool(pp[i], maxPp[i]))
            .ToList();
    }

    private bool ConditionMatches(FormCondition? condition, Weather weather)
    {
        if (condition is null)
            return false;

        bool weatherMatches = string.IsNullOrWhiteSpace(condition.Weather)
            || string.Equals(condition.Weather, weather.ToString(), StringComparison.OrdinalIgnoreCase);
        bool heldMatches = condition.HeldItem is null
            || (HeldItem == condition.HeldItem && _consumedHeldEffects.Count == 0);
        return weatherMatches && heldMatches;
    }

    private static Form? ActivePermanentForm(CreatureInstance instance, Species species)
    {
        if (string.IsNullOrWhiteSpace(instance.Form))
            return null;

        Form form = species.Forms.FirstOrDefault(f => f.FormId == instance.Form)
            ?? throw new InvalidOperationException($"Unknown form '{instance.Form}' for species '{species.Id}'.");
        if (form.Activation != FormActivation.Permanent)
            throw new NotSupportedException($"Form '{form.FormId}' activation '{form.Activation}' is not a permanent battle form.");
        return form;
    }

    private static IReadOnlyList<BattleFormRuntime> BuildRuntimeForms(
        Species species, CreatureInstance instance, GameDb db, IReadOnlyList<AbilityHook> baseHooks)
    {
        return species.Forms
            .Where(f => f.Activation is FormActivation.Condition or FormActivation.BattleTemporary or FormActivation.BattleTimed)
            .Select(f =>
            {
                Ability? ability = f.AbilityOverride is { } id ? db.Find<Ability>(id) : null;
                return new BattleFormRuntime(
                    f.FormId,
                    f.Activation,
                    f.Condition,
                    f.RequiredHeldItem,
                    f.RequiredTrainerItem,
                    f.Turns,
                    f.HpMultiplierPercent,
                    StatCalc.Compute(f.StatOverrides ?? species.BaseStats, instance.Ivs, instance.Evs, instance.Nature, instance.Level),
                    f.TypeOverrides ?? species.Types,
                    ability?.Hooks ?? baseHooks,
                    BuildMoveRemap(f.MoveRemap, db));
            })
            .ToList();
    }

    private static IReadOnlyDictionary<EntityId, BattleMove> BuildMoveRemap(IReadOnlyDictionary<EntityId, EntityId>? remap, GameDb db)
    {
        if (remap is null || remap.Count == 0)
            return new Dictionary<EntityId, BattleMove>();

        return remap.ToDictionary(
            kv => kv.Key,
            kv => MoveCompiler.ToBattleMove(db.Find<Move>(kv.Value)
                ?? throw new InvalidOperationException($"Unknown remapped move '{kv.Value}'.")));
    }

    public int Stage(StatKind stat) => _stages[StageIndex(stat)];

    public void SetStage(StatKind stat, int value) => _stages[StageIndex(stat)] = StatStages.Clamp(value);

    public void ChangeStage(StatKind stat, int delta)
    {
        int i = StageIndex(stat);
        _stages[i] = StatStages.Apply(_stages[i], delta);
    }

    /// <summary>Clears all stat stages (on switch-out or battle end).</summary>
    public void ResetStages() => Array.Clear(_stages);

    public void SetConfusion(int turns) => ConfusionCounter = Math.Max(0, turns);
    /// <summary>Counts down confusion (0 = snapped out).</summary>
    public void TickConfusion() { if (ConfusionCounter > 0) ConfusionCounter--; }
    public void SetFlinch() => Flinched = true;
    public void ClearFlinch() => Flinched = false;
    public void RaiseCrit(int stages) => CritStageBonus += Math.Max(0, stages);
    public void SetSeeded(bool seeded) => Seeded = seeded;
    public void SetTrap(int turns) => TrapTurns = Math.Max(0, turns);
    /// <summary>Counts down the partial trap (0 = released).</summary>
    public void TickTrap() { if (TrapTurns > 0) TrapTurns--; }

    public void SetProtected() { Protected = true; ProtectChain++; }
    public void ClearProtected() => Protected = false;
    /// <summary>Breaks the protect chain (used a different move, or protect failed).</summary>
    public void ResetProtectChain() => ProtectChain = 0;

    /// <summary>Records damage taken this turn for Counter/Mirror Coat (status hits contribute nothing).</summary>
    public void RecordDamageTaken(DamageClass damageClass, int amount)
    {
        if (damageClass == DamageClass.Physical) PhysicalDamageTaken += amount;
        else if (damageClass == DamageClass.Special) SpecialDamageTaken += amount;
    }

    public void ResetDamageTaken() { PhysicalDamageTaken = 0; SpecialDamageTaken = 0; }

    public bool HasConsumedHeldEffect(string op) => _consumedHeldEffects.Contains(op);
    public bool ConsumeHeldEffect(string op) => _consumedHeldEffects.Add(op);

    public void StartCharging(int moveIndex) => ChargingMoveIndex = moveIndex;
    public void StopCharging() => ChargingMoveIndex = null;

    public void StartLock(int moveIndex, int turns) { LockedMoveIndex = moveIndex; LockTurns = Math.Max(0, turns); }
    /// <summary>Counts down the rampage lock (0 = the rampage ends this turn → self-confusion).</summary>
    public void TickLock() { if (LockTurns > 0) LockTurns--; }
    public void SetChoiceLock(int moveIndex) => ChoiceLockedMoveIndex ??= moveIndex;
    public void RecordMoveUse(EntityId move) { ActionsSinceSwitch++; LastMoveUsed = move; }

    /// <summary>Clears volatile state on switch-out / battle end (stages handled separately).</summary>
    public void ClearVolatiles()
    {
        ConfusionCounter = 0;
        Flinched = false;
        CritStageBonus = 0;
        Seeded = false;
        TrapTurns = 0;
        Protected = false;
        ProtectChain = 0;
        ChargingMoveIndex = null;
        LockTurns = 0;
        ChoiceLockedMoveIndex = null;
        ActionsSinceSwitch = 0;
        LastMoveUsed = null;
    }

    private static int StageIndex(StatKind stat) => stat switch
    {
        StatKind.Atk => 0,
        StatKind.Def => 1,
        StatKind.Spa => 2,
        StatKind.Spd => 3,
        StatKind.Spe => 4,
        StatKind.Accuracy => 5,
        StatKind.Evasion => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), "HP has no stat stage."),
    };

    public void TakeDamage(int amount) => CurrentHp = Math.Clamp(CurrentHp - amount, 0, MaxHp);
    public void Heal(int amount) => CurrentHp = Math.Clamp(CurrentHp + amount, 0, MaxHp);

    public void SetStatus(PersistentStatus status, int counter = 0)
    {
        Status = status;
        StatusCounter = counter > 0 ? counter : (status == PersistentStatus.Toxic ? 1 : 0);
    }

    /// <summary>Counts down a sleep timer (0 = ready to wake next attempt).</summary>
    public void TickSleep()
    {
        if (StatusCounter > 0)
            StatusCounter--;
    }

    public void ClearStatus()
    {
        Status = null;
        StatusCounter = 0;
    }

    /// <summary>Ramps the toxic counter at end of turn (no effect for other statuses).</summary>
    public void AdvanceToxic()
    {
        if (Status == PersistentStatus.Toxic)
            StatusCounter++;
    }
}
