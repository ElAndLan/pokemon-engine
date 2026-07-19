namespace Cgm.Core.Battle;

public enum BattleDecoyFailure { None, AlreadyPresent, InsufficientHp }

public sealed record BattleDecoyCreation(BattleDecoyFailure Failure, int Cost, BattleDecoyState? Decoy)
{
    public bool Succeeded => Failure == BattleDecoyFailure.None;
}

public sealed record BattleDecoyInterception(int Absorbed, BattleDecoyState? Remaining, bool Broke);

/// <summary>Pure decoy (Substitute) arithmetic for Phase 15F-6. Creation costs a fraction of the
/// owner's max HP; interception absorbs incoming damage with no overflow to the owner. No RNG, no
/// mutation of the owner — the controller applies the HP cost and routes eligible hits here.</summary>
public static class BattleDecoy
{
    public static BattleDecoyCreation Create(int currentHp, int maxHp, Fraction costFraction, bool decoyPresent)
    {
        if (maxHp <= 0 || costFraction.Den == 0 || costFraction.Num <= 0)
            throw new ArgumentException("Decoy creation needs positive max HP and a positive cost fraction.");
        if (decoyPresent)
            return new BattleDecoyCreation(BattleDecoyFailure.AlreadyPresent, 0, null);
        int cost = Math.Max(1, maxHp * costFraction.Num / costFraction.Den);
        if (currentHp <= cost)
            return new BattleDecoyCreation(BattleDecoyFailure.InsufficientHp, cost, null);
        return new BattleDecoyCreation(BattleDecoyFailure.None, cost, new BattleDecoyState(cost, cost));
    }

    public static BattleDecoyInterception Intercept(BattleDecoyState decoy, int incomingDamage)
    {
        ArgumentNullException.ThrowIfNull(decoy);
        if (incomingDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(incomingDamage), "Intercepted damage cannot be negative.");
        int absorbed = Math.Min(incomingDamage, decoy.Hp);
        int remainingHp = decoy.Hp - absorbed;
        bool broke = remainingHp <= 0;
        return new BattleDecoyInterception(absorbed, broke ? null : decoy with { Hp = remainingHp }, broke);
    }
}
