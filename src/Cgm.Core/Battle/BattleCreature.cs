using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>A stat-stage change a move applies — to the user or the target, at a given chance.</summary>
public sealed record StageEffect(StatKind Stat, int Delta, bool OnSelf, int Chance);

/// <summary>A rational fraction (num/den) for drain/recoil/heal effect amounts.</summary>
public readonly record struct Fraction(int Num, int Den);

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
        int critBoost = 0, bool selfDestruct = false, bool leechSeed = false)
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
        StageEffect = stageEffect;
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
    public int ConfuseChance { get; }
    public int FlinchChance { get; }

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

    public bool HasPp => Pp > 0;
    public void UsePp() => Pp = Math.Max(0, Pp - 1);
}

/// <summary>A creature as it exists in battle: computed stats, current HP, and its moves. Mutable
/// runtime state, distinct from the species definition and the saved instance.</summary>
public sealed class BattleCreature
{
    public BattleCreature(EntityId species, string name, int level,
        IReadOnlyList<EntityId> types, Stats stats, IReadOnlyList<BattleMove> moves, int catchRate = 45)
    {
        Species = species;
        Name = name;
        Level = level;
        Types = types;
        Stats = stats;
        MaxHp = stats.Hp;
        CurrentHp = stats.Hp;
        Moves = moves;
        CatchRate = catchRate;
    }

    public EntityId Species { get; }
    public string Name { get; }
    public int Level { get; }
    public IReadOnlyList<EntityId> Types { get; }
    public Stats Stats { get; }
    public int MaxHp { get; }
    public int CurrentHp { get; private set; }
    public int CatchRate { get; }
    public IReadOnlyList<BattleMove> Moves { get; }

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

    private readonly int[] _stages = new int[5]; // atk, def, spa, spd, spe

    public bool IsFainted => CurrentHp <= 0;

    public int Stage(StatKind stat) => _stages[StageIndex(stat)];

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

    /// <summary>Clears volatile state on switch-out / battle end (stages handled separately).</summary>
    public void ClearVolatiles()
    {
        ConfusionCounter = 0;
        Flinched = false;
        CritStageBonus = 0;
        Seeded = false;
    }

    private static int StageIndex(StatKind stat) => stat switch
    {
        StatKind.Atk => 0,
        StatKind.Def => 1,
        StatKind.Spa => 2,
        StatKind.Spd => 3,
        StatKind.Spe => 4,
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
