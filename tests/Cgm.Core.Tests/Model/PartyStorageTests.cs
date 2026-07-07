using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class PartyStorageTests
{
    private static CreatureInstance Mon(string slug = "mon") => new() { Species = EntityId.Parse($"species:{slug}") };

    private static List<CreatureInstance> Full(int n) => Enumerable.Range(0, n).Select(i => Mon($"m{i}")).ToList();

    [Fact]
    public void EmptyParty_GoesToParty()
    {
        var party = new List<CreatureInstance>();
        var boxes = new List<List<CreatureInstance>> { new() };
        DepositResult? r = PartyStorage.Deposit(Mon(), party, boxes, boxCapacity: 30);
        Assert.Equal(DepositTarget.Party, r!.Value.Target);
        Assert.Single(party);
    }

    [Fact]
    public void PartyWithRoom_StillGoesToParty()
    {
        var party = Full(5);
        var boxes = new List<List<CreatureInstance>> { new() };
        DepositResult? r = PartyStorage.Deposit(Mon(), party, boxes, 30);
        Assert.Equal(DepositTarget.Party, r!.Value.Target);
        Assert.Equal(6, party.Count);
    }

    [Fact]
    public void FullParty_GoesToFirstBoxWithRoom()
    {
        var party = Full(6);
        var boxes = new List<List<CreatureInstance>> { new(), new() };
        DepositResult? r = PartyStorage.Deposit(Mon("caught"), party, boxes, 30);
        Assert.Equal(new DepositResult(DepositTarget.Box, 0), r);
        Assert.Single(boxes[0]);
        Assert.Equal(6, party.Count); // party untouched
    }

    [Fact]
    public void FullPartyAndFirstBox_SkipToNextBox()
    {
        var party = Full(6);
        var boxes = new List<List<CreatureInstance>> { Full(2), new() }; // box0 at capacity 2
        DepositResult? r = PartyStorage.Deposit(Mon("caught"), party, boxes, boxCapacity: 2);
        Assert.Equal(new DepositResult(DepositTarget.Box, 1), r);
        Assert.Single(boxes[1]);
    }

    [Fact]
    public void EverythingFull_ReturnsNull()
    {
        var party = Full(6);
        var boxes = new List<List<CreatureInstance>> { Full(1), Full(1) };
        DepositResult? r = PartyStorage.Deposit(Mon("caught"), party, boxes, boxCapacity: 1);
        Assert.Null(r);
        Assert.All(boxes, b => Assert.Single(b)); // nothing added
    }

    [Fact]
    public void ZeroCapacityBoxes_AreSkipped()
    {
        var party = Full(6);
        var boxes = new List<List<CreatureInstance>> { new(), new() };
        Assert.Null(PartyStorage.Deposit(Mon(), party, boxes, boxCapacity: 0));
    }
}
