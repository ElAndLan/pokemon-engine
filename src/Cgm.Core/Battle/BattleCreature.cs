using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>A move as it exists in battle: static data + remaining PP (BATTLE_SYSTEM_SPEC).</summary>
public sealed class BattleMove
{
    public BattleMove(EntityId move, EntityId type, DamageClass damageClass,
        int? power, int? accuracy, int pp, int priority, int critStage)
    {
        Move = move;
        Type = type;
        DamageClass = damageClass;
        Power = power;
        Accuracy = accuracy;
        MaxPp = Pp = pp;
        Priority = priority;
        CritStage = critStage;
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

    public bool HasPp => Pp > 0;
    public void UsePp() => Pp = Math.Max(0, Pp - 1);
}

/// <summary>A creature as it exists in battle: computed stats, current HP, and its moves. Mutable
/// runtime state, distinct from the species definition and the saved instance.</summary>
public sealed class BattleCreature
{
    public BattleCreature(EntityId species, string name, int level,
        IReadOnlyList<EntityId> types, Stats stats, IReadOnlyList<BattleMove> moves)
    {
        Species = species;
        Name = name;
        Level = level;
        Types = types;
        Stats = stats;
        MaxHp = stats.Hp;
        CurrentHp = stats.Hp;
        Moves = moves;
    }

    public EntityId Species { get; }
    public string Name { get; }
    public int Level { get; }
    public IReadOnlyList<EntityId> Types { get; }
    public Stats Stats { get; }
    public int MaxHp { get; }
    public int CurrentHp { get; private set; }
    public IReadOnlyList<BattleMove> Moves { get; }

    public bool IsFainted => CurrentHp <= 0;

    public void TakeDamage(int amount) => CurrentHp = Math.Clamp(CurrentHp - amount, 0, MaxHp);
    public void Heal(int amount) => CurrentHp = Math.Clamp(CurrentHp + amount, 0, MaxHp);
}
