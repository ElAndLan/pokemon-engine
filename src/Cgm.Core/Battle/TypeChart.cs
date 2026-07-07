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
    {
        if (!_types.TryGetValue(moveType, out TypeDef? t))
            return 1.0;
        if (t.NoDamageTo.Contains(defenderType)) return 0.0;
        if (t.DoubleDamageTo.Contains(defenderType)) return 2.0;
        if (t.HalfDamageTo.Contains(defenderType)) return 0.5;
        return 1.0;
    }

    /// <summary>Combined effectiveness against a defender's 1–2 types (their product).</summary>
    public double Effectiveness(EntityId moveType, IReadOnlyList<EntityId> defenderTypes)
    {
        double m = 1.0;
        foreach (EntityId d in defenderTypes)
            m *= Single(moveType, d);
        return m;
    }

    /// <summary>Same-Type Attack Bonus: ×1.5 when the move's type matches an attacker type.</summary>
    public static double Stab(EntityId moveType, IReadOnlyList<EntityId> attackerTypes) =>
        attackerTypes.Contains(moveType) ? 1.5 : 1.0;
}
