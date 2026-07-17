using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Type effectiveness from the <see cref="TypeDef"/> data (BATTLE_DAMAGE_CALC §5): per-type
/// multiplier (×0 immunity / ×0.5 / ×1 / ×2), the dual-type product, and STAB. Built once per
/// battle from the project's types.
/// </summary>
public sealed class TypeChart
{
    private readonly IReadOnlyDictionary<EntityId, TypeDef> _types;

    public TypeChart(IEnumerable<TypeDef> types) => _types = types.ToDictionary(t => t.Id);

    /// <summary>Multiplier of <paramref name="moveType"/> attacking one defender type.</summary>
    public double Single(EntityId moveType, EntityId defenderType)
        => ToDouble(SingleValue(moveType, defenderType));

    public BattleQueryValue SingleValue(EntityId moveType, EntityId defenderType)
    {
        if (!_types.TryGetValue(moveType, out TypeDef? t))
            return new BattleQueryValue(1);
        if (t.NoDamageTo.Contains(defenderType)) return new BattleQueryValue(0);
        if (t.DoubleDamageTo.Contains(defenderType)) return new BattleQueryValue(2);
        if (t.HalfDamageTo.Contains(defenderType)) return new BattleQueryValue(1, 2);
        return new BattleQueryValue(1);
    }

    /// <summary>Combined effectiveness against a defender's 1–2 types (their product).</summary>
    public double Effectiveness(EntityId moveType, IReadOnlyList<EntityId> defenderTypes)
        => ToDouble(EffectivenessValue(moveType, defenderTypes));

    public BattleQueryValue EffectivenessValue(EntityId moveType, IReadOnlyList<EntityId> defenderTypes) =>
        defenderTypes.Aggregate(new BattleQueryValue(1),
            (value, type) => Multiply(value, SingleValue(moveType, type)));

    /// <summary>Same-Type Attack Bonus: ×1.5 when the move's type matches an attacker type.</summary>
    public static double Stab(EntityId moveType, IReadOnlyList<EntityId> attackerTypes) =>
        attackerTypes.Contains(moveType) ? 1.5 : 1.0;

    internal static BattleQueryValue Multiply(BattleQueryValue left, BattleQueryValue right) =>
        new(checked(left.Numerator * right.Numerator), checked(left.Denominator * right.Denominator));

    internal static double ToDouble(BattleQueryValue value) => value.Numerator / (double)value.Denominator;
}
