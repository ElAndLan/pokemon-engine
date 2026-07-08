using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class StorageTests
{
    private static CreatureInstance Mon(string slug, int hp = 100) => new()
    {
        Species = EntityId.Parse($"species:{slug}"),
        CurHp = hp,
    };

    [Fact]
    public void DepositToBox_MovesCreature_ConservesTotal()
    {
        var party = new List<CreatureInstance> { Mon("a"), Mon("b") };
        var box = new List<CreatureInstance>();
        int before = party.Count + box.Count;

        Assert.True(Storage.DepositToBox(party, box, partyIndex: 1, boxCapacity: 30));
        Assert.Single(party);
        Assert.Single(box);
        Assert.Equal(before, party.Count + box.Count); // conserved
        Assert.Equal(EntityId.Parse("species:b"), box[0].Species);
    }

    [Fact]
    public void DepositToBox_FailsWhenBoxFull()
    {
        var party = new List<CreatureInstance> { Mon("a"), Mon("b") };
        var box = new List<CreatureInstance> { Mon("x") };
        Assert.False(Storage.DepositToBox(party, box, 1, boxCapacity: 1));
        Assert.Equal(2, party.Count); // unchanged
    }

    [Fact]
    public void DepositToBox_CannotStrandLastHealthyMember()
    {
        // Party: one healthy (0), one fainted (1). Can deposit the fainted but not the healthy.
        var party = new List<CreatureInstance> { Mon("healthy", 100), Mon("fainted", 0) };
        var box = new List<CreatureInstance>();

        Assert.False(Storage.DepositToBox(party, box, partyIndex: 0, boxCapacity: 30)); // would strand
        Assert.True(Storage.DepositToBox(party, box, partyIndex: 1, boxCapacity: 30));  // fainted ok
        Assert.Single(party);
    }

    [Fact]
    public void WithdrawToParty_MovesCreature_UnlessPartyFull()
    {
        var party = new List<CreatureInstance>();
        var box = new List<CreatureInstance> { Mon("a") };
        Assert.True(Storage.WithdrawToParty(party, box, 0));
        Assert.Single(party);
        Assert.Empty(box);

        var fullParty = Enumerable.Range(0, 6).Select(i => Mon($"m{i}")).ToList();
        var box2 = new List<CreatureInstance> { Mon("z") };
        Assert.False(Storage.WithdrawToParty(fullParty, box2, 0));
        Assert.Equal(6, fullParty.Count);
    }

    [Fact]
    public void ReleaseFromBox_AlwaysAllowedForValidSlot()
    {
        var box = new List<CreatureInstance> { Mon("a"), Mon("b") };
        Assert.True(Storage.ReleaseFromBox(box, 0));
        Assert.Single(box);
        Assert.False(Storage.ReleaseFromBox(box, 5)); // bad slot
    }

    [Fact]
    public void ReleaseFromParty_BlockedIfStrands()
    {
        var soloHealthy = new List<CreatureInstance> { Mon("only", 100) };
        Assert.False(Storage.ReleaseFromParty(soloHealthy, 0)); // last healthy → blocked

        var pair = new List<CreatureInstance> { Mon("a", 100), Mon("b", 100) };
        Assert.True(Storage.ReleaseFromParty(pair, 0)); // another healthy remains
        Assert.Single(pair);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void BadIndices_FailGracefully(int index)
    {
        var party = new List<CreatureInstance> { Mon("a") };
        var box = new List<CreatureInstance> { Mon("b") };
        Assert.False(Storage.DepositToBox(party, box, index, 30));
        Assert.False(Storage.WithdrawToParty(party, box, index));
    }
}
