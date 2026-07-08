using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class ItemEffectsTests
{
    private static CreatureInstance Mon(int curHp, PersistentStatus? status = null, params MoveSlot[] moves) => new()
    {
        Species = EntityId.Parse("species:x"),
        CurHp = curHp,
        Status = status,
        StatusCounter = status is null ? 0 : 3,
        Moves = moves,
    };

    [Fact]
    public void Heal_RestoresAndClampsAtMax()
    {
        Assert.Equal(50, ItemEffects.Heal(Mon(30), 20, maxHp: 100).CurHp);
        Assert.Equal(100, ItemEffects.Heal(Mon(90), 20, maxHp: 100).CurHp); // clamped
    }

    [Fact]
    public void Heal_NoEffectOnFainted()
    {
        Assert.Equal(0, ItemEffects.Heal(Mon(0), 50, maxHp: 100).CurHp);
    }

    [Fact]
    public void Revive_OnlyOnFainted_RestoresFraction_ClearsStatus()
    {
        CreatureInstance revived = ItemEffects.Revive(Mon(0, PersistentStatus.Poison), maxHp: 100, fraction: 0.5);
        Assert.Equal(50, revived.CurHp);
        Assert.Null(revived.Status);

        // No effect on a healthy creature.
        Assert.Equal(40, ItemEffects.Revive(Mon(40), 100, 1.0).CurHp);
    }

    [Fact]
    public void Revive_MinimumOneHp()
    {
        Assert.Equal(1, ItemEffects.Revive(Mon(0), maxHp: 1, fraction: 0.5).CurHp);
    }

    [Fact]
    public void CureStatus_ClearsStatusAndCounter()
    {
        CreatureInstance cured = ItemEffects.CureStatus(Mon(50, PersistentStatus.Sleep));
        Assert.Null(cured.Status);
        Assert.Equal(0, cured.StatusCounter);
    }

    [Fact]
    public void RestorePp_ClampsAtMax_AndIgnoresBadIndex()
    {
        var mon = Mon(50, null, new MoveSlot(EntityId.Parse("move:m"), 3));
        Assert.Equal(10, ItemEffects.RestorePp(mon, 0, 20, maxPp: 10).Moves[0].Pp); // clamped
        Assert.Equal(8, ItemEffects.RestorePp(mon, 0, 5, maxPp: 10).Moves[0].Pp);
        Assert.Equal(3, ItemEffects.RestorePp(mon, 9, 5, maxPp: 10).Moves[0].Pp); // bad index → unchanged
    }
}

public sealed class MartTests
{
    [Fact]
    public void SellPrice_IsHalf()
    {
        Assert.Equal(100, Mart.SellPrice(200));
        Assert.Equal(2, Mart.SellPrice(5)); // floors
    }

    [Theory]
    [InlineData(1000, 200, 5, true)]   // exactly affordable
    [InlineData(999, 200, 5, false)]
    [InlineData(0, 200, 0, true)]      // buying nothing is free
    public void CanAfford(int money, int price, int qty, bool expected)
    {
        Assert.Equal(expected, Mart.CanAfford(money, price, qty));
    }

    [Fact]
    public void BuyCost_IsPriceTimesQuantity()
    {
        Assert.Equal(600, Mart.BuyCost(200, 3));
    }
}
