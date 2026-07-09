using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Model;

public sealed class SaveTests
{
    private static CreatureInstance Mon() => new()
    {
        Species = EntityId.Parse("species:leafcub"),
        Level = 12,
        Exp = 1200,
        Ivs = new Stats(31, 20, 15, 5, 0, 31),
        Evs = new Stats(0, 4, 0, 0, 0, 0),
        Nature = "adamant",
        CurHp = 30,
        Status = PersistentStatus.Poison,
        StatusCounter = 2,
        Moves = [new MoveSlot(EntityId.Parse("move:ember"), 25)],
        HeldItem = EntityId.Parse("item:potion"),
        Nickname = "Sprout",
        OtName = "Red",
        Ball = EntityId.Parse("item:poke_ball"),
    };

    private static SaveFile Sample() => new()
    {
        GameContentHash = "abc123",
        Map = EntityId.Parse("map:route_001"),
        Pos = new GridPos(5, 9),
        Facing = Facing.Left,
        Party = [Mon()],
        Boxes = [[Mon()], []],
        Bag = new Dictionary<string, IReadOnlyList<BagEntry>>
        {
            ["medicine"] = [new BagEntry(EntityId.Parse("item:potion"), 3)],
        },
        Money = 5000,
        Flags = new Dictionary<string, int> { ["story.badge_1"] = 1 },
        Respawn = new RespawnPoint(EntityId.Parse("map:town"), new GridPos(1, 1)),
        PlaytimeSeconds = 3600,
        Dex = new DexRecord { Seen = [EntityId.Parse("species:leafcub")], Caught = [EntityId.Parse("species:leafcub")] },
    };

    [Fact]
    public void SaveFile_RoundTripsByteStable()
    {
        string first = CgmJson.Serialize(Sample());
        string second = CgmJson.Serialize(CgmJson.Deserialize<SaveFile>(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public void SaveFile_PreservesKeyFieldsThroughRoundTrip()
    {
        SaveFile back = CgmJson.Deserialize<SaveFile>(CgmJson.Serialize(Sample()));
        Assert.Equal(EntityId.Parse("map:route_001"), back.Map);
        Assert.Single(back.Party);
        Assert.Equal(PersistentStatus.Poison, back.Party[0].Status);
        Assert.Equal("Sprout", back.Party[0].Nickname);
        Assert.Equal(2, back.Boxes.Count);
        Assert.Equal(3, back.Bag["medicine"][0].Count);
        Assert.Equal(1, back.Flags["story.badge_1"]);
    }

    [Fact]
    public void Status_SerializesAsCamelCaseString()
    {
        string json = CgmJson.Serialize(Mon());
        Assert.Contains("\"status\": \"poison\"", json);
    }

    [Fact]
    public void NullStatus_IsOmitted()
    {
        string json = CgmJson.Serialize(new CreatureInstance { Species = EntityId.Parse("species:x") });
        Assert.DoesNotContain("\"status\":", json); // distinct from the always-present "statusCounter"
    }
}

public sealed class InstanceGenTests
{
    private static readonly Stats Bases = new(45, 49, 49, 65, 65, 45);

    [Fact]
    public void RandomIvs_AllInRange()
    {
        var rng = new Rng(1);
        for (int i = 0; i < 200; i++)
        {
            Stats ivs = InstanceGen.RandomIvs(rng);
            foreach (int v in new[] { ivs.Hp, ivs.Atk, ivs.Def, ivs.Spa, ivs.Spd, ivs.Spe })
                Assert.InRange(v, 0, 31);
        }
    }

    [Fact]
    public void RandomNature_IsValid_AndSeedDeterministic()
    {
        Assert.True(Natures.IsValid(InstanceGen.RandomNature(new Rng(3))));
        Assert.Equal(InstanceGen.RandomNature(new Rng(3)), InstanceGen.RandomNature(new Rng(3)));
    }

    [Fact]
    public void Create_SetsExpAtCurve_FullHp_AndLevel()
    {
        var inst = InstanceGen.Create(EntityId.Parse("species:leafcub"), Bases, "medium-slow",
            level: 10, moves: [new MoveSlot(EntityId.Parse("move:ember"), 25)], rng: new Rng(5), otName: "Red");

        Assert.Equal(10, inst.Level);
        Assert.Equal(ExpCurve.TotalExp("medium-slow", 10), inst.Exp);
        int maxHp = StatCalc.Compute(Bases, inst.Ivs, inst.Evs, inst.Nature, 10).Hp;
        Assert.Equal(maxHp, inst.CurHp); // starts at full HP
        Assert.Equal("Red", inst.OtName);
        Assert.Equal(default, inst.Evs); // zero EVs
    }

    [Fact]
    public void Create_ChoosesNormalAbilitySlotWithInjectedRng()
    {
        var inst = InstanceGen.Create(EntityId.Parse("species:leafcub"), Bases, "medium-slow",
            level: 10, moves: [], rng: new SequenceRng(0, 0, 0, 0, 0, 0, 0, 1),
            normalAbilities: [EntityId.Parse("ability:first"), EntityId.Parse("ability:second")]);

        Assert.Equal("ability:second", inst.Ability);
    }

    [Fact]
    public void Create_LeavesAbilityEmpty_WhenNoNormalAbilities()
    {
        CreatureInstance inst = InstanceGen.Create(EntityId.Parse("species:x"), Bases, "fast", 5, [], new Rng(9));

        Assert.Null(inst.Ability);
    }

    [Fact]
    public void Create_IsSeedDeterministic()
    {
        CreatureInstance a = InstanceGen.Create(EntityId.Parse("species:x"), Bases, "fast", 5, [], new Rng(9));
        CreatureInstance b = InstanceGen.Create(EntityId.Parse("species:x"), Bases, "fast", 5, [], new Rng(9));
        Assert.Equal(a.Ivs, b.Ivs);
        Assert.Equal(a.Nature, b.Nature);
    }

    private sealed class SequenceRng(params int[] values) : IRng
    {
        private readonly Queue<int> _values = new(values);

        public int Next(int maxExclusive) => _values.Dequeue();
        public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
        public double NextDouble() => 0;
    }
}
