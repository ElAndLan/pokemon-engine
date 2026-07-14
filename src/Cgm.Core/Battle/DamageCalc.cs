namespace Cgm.Core.Battle;

/// <summary>
/// The Gen III/IV damage pipeline (BATTLE_DAMAGE_CALC §2/§4). Pure: callers pass the resolved
/// stats and the already-decided crit/roll (drawn from <c>IRng</c> in fixed order by the resolver),
/// so the formula is table-testable to exact values. Modifiers apply in spec order, flooring after
/// each. Immunity short-circuits to 0; otherwise damage is at least 1.
/// </summary>
public static class DamageCalc
{
    /// <summary>Applies the standard spread modifier at the Targets stage after live targets are snapshotted.</summary>
    public static int ApplyTargetsModifier(int damage, int snapshottedLiveTargets) =>
        snapshottedLiveTargets >= 2 ? damage * 3 / 4 : damage;

    /// <summary>base = floor(floor(floor(2·L/5 + 2)·Power·A/D)/50) + 2.</summary>
    public static int BaseDamage(int level, int power, int a, int d)
    {
        int i1 = 2 * level / 5 + 2;
        long i2 = (long)i1 * power * a / d;
        return (int)(i2 / 50) + 2;
    }

    public static int Compute(int level, int power, int a, int d,
        double effectiveness, double stab, bool crit, int roll, bool burn, int snapshottedLiveTargets = 1)
    {
        if (effectiveness <= 0)
            return 0; // immune

        long dmg = ApplyTargetsModifier(BaseDamage(level, power, a, d), snapshottedLiveTargets);
        if (crit) dmg *= 2;                    // 3. critical
        dmg = dmg * roll / 100;                // 4. random (85–100), integer floor
        dmg = (long)(dmg * stab);              // 5. STAB
        dmg = (long)(dmg * effectiveness);     // 6. type effectiveness
        if (burn) dmg /= 2;                    // 7. burn (physical)

        return (int)Math.Max(1, dmg);
    }
}
