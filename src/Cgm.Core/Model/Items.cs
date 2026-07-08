namespace Cgm.Core.Model;

/// <summary>Medicine/ball effects applied to a creature instance (Phase 10). Instances are
/// immutable, so each returns an updated copy. Max HP/PP are passed in (computed from stats/moves).</summary>
public static class ItemEffects
{
    /// <summary>Restores HP, clamped to max. A potion has no effect on a fainted creature.</summary>
    public static CreatureInstance Heal(CreatureInstance c, int amount, int maxHp)
    {
        if (c.CurHp <= 0)
            return c; // fainted — needs a revive
        return c with { CurHp = Math.Min(maxHp, c.CurHp + Math.Max(0, amount)) };
    }

    /// <summary>Revives a fainted creature to a fraction of max HP (0.5 = revive, 1.0 = max revive)
    /// and clears status. No effect on a healthy creature.</summary>
    public static CreatureInstance Revive(CreatureInstance c, int maxHp, double fraction)
    {
        if (c.CurHp > 0)
            return c;
        return c with { CurHp = Math.Max(1, (int)(maxHp * fraction)), Status = null, StatusCounter = 0 };
    }

    public static CreatureInstance CureStatus(CreatureInstance c) =>
        c with { Status = null, StatusCounter = 0 };

    /// <summary>Restores a move's PP, clamped to its max.</summary>
    public static CreatureInstance RestorePp(CreatureInstance c, int moveIndex, int amount, int maxPp)
    {
        if (moveIndex < 0 || moveIndex >= c.Moves.Count)
            return c;

        var moves = c.Moves.ToList();
        MoveSlot slot = moves[moveIndex];
        moves[moveIndex] = slot with { Pp = Math.Min(maxPp, slot.Pp + Math.Max(0, amount)) };
        return c with { Moves = moves };
    }
}

/// <summary>Mart money arithmetic (Phase 10): sell at half price, buy if affordable.</summary>
public static class Mart
{
    public static int SellPrice(int price) => price / 2;

    public static int BuyCost(int price, int quantity) => price * Math.Max(0, quantity);

    public static bool CanAfford(int money, int price, int quantity) => money >= BuyCost(price, quantity);
}
