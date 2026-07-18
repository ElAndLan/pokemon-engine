using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record BattleFallbackProfile(int Power, Fraction Recoil);

public static class BattleFallbackRules
{
    private static readonly IReadOnlyDictionary<string, BattleFallbackProfile> Rows =
        new Dictionary<string, BattleFallbackProfile>(StringComparer.Ordinal)
        {
            [BattleRulesets.Gen4Like] = new(50, new Fraction(1, 4)),
            [BattleRulesets.ModernReference] = new(50, new Fraction(1, 4)),
        };

    public static BattleFallbackProfile For(string ruleset) => Rows.TryGetValue(ruleset, out BattleFallbackProfile? row)
        ? row : throw new ArgumentException($"Unsupported battle ruleset '{ruleset}'.", nameof(ruleset));

    public static BattleMove Compile(string ruleset, EntityId type)
    {
        BattleFallbackProfile profile = For(ruleset);
        return new BattleMove(new EntityId(EntityCategory.Move, "fallback_action"), type,
            DamageClass.Physical, profile.Power, null, int.MaxValue, 0, 0,
            recoil: profile.Recoil, secondaryEffects:
            [
                new EffectivenessQueryEffect(EffectivenessQueryMode.Neutral, null, null, null,
                    StabQuerySource.None),
                new RecoilEffect(profile.Recoil),
            ], isFallback: true);
    }
}
