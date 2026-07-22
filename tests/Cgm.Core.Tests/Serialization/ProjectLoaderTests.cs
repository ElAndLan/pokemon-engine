using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class ProjectLoaderTests
{
    private static Project LoadFixtureMin() => ProjectLoader.Load(TestPaths.Sample("fixture-min"));

    [Fact]
    public void Load_ReadsSettingsAndAllEntities()
    {
        Project p = LoadFixtureMin();
        Assert.Equal("Fixture Min", p.Settings.Name);
        Assert.Equal(2, p.All<TypeDef>().Count());
        Assert.Equal(2, p.All<Move>().Count());
        Assert.Single(p.All<Species>());
        Assert.Single(p.All<Item>());
    }

    [Fact]
    public void Load_ResolvesTypedEntities()
    {
        Project p = LoadFixtureMin();

        Species leafcub = p.Find<Species>(EntityId.Parse("species:leafcub"))!;
        Assert.NotNull(leafcub);
        Assert.Equal(45, leafcub.BaseStats.Hp);
        Assert.Equal(69, leafcub.WeightHectograms);
        Assert.Equal(7, leafcub.HeightDecimeters);
        Assert.Equal([EntityId.Parse("type:grass")], leafcub.Types);
        Assert.Equal(2, leafcub.Learnset.Count);
        Assert.Equal(7, leafcub.Learnset[1].Level);

        Move ember = p.Find<Move>(EntityId.Parse("move:ember"))!;
        Assert.Equal(40, ember.Power);
        Assert.Equal(DamageClass.Special, ember.DamageClass);
        Assert.Equal(2, ember.Effects.Count);
        Assert.Equal("ailment", ember.Effects[1].Op);
        Assert.Equal(10, ember.Effects[1].Chance);
    }

    [Fact]
    public void Load_ReadsWorldEntities()
    {
        Project p = LoadFixtureMin();
        Assert.Single(p.All<Tileset>());
        Assert.Single(p.All<EncounterTable>());

        Map map = p.Find<Map>(EntityId.Parse("map:test_room"))!;
        Assert.Equal(4, map.Width);
        Assert.Equal([EntityId.Parse("tileset:exterior")], map.Tilesets);
        Assert.Equal(12, map.Layers.Ground.Count);
        Assert.Equal(2, map.EncounterZones.Count);
        Assert.Equal(EntityId.Parse("encounter:test_room_grass"), map.EncounterZones[0].Table);
    }

    [Fact]
    public void Load_ResolvesPolymorphicMapEntities()
    {
        Map map = LoadFixtureMin().Find<Map>(EntityId.Parse("map:test_room"))!;
        Assert.Equal(3, map.Entities.Count);

        Assert.IsType<PlayerStartEntity>(map.Entities[0]);

        var npc = Assert.IsType<NpcEntity>(map.Entities[1]);
        Assert.Equal("Hi there!", npc.Dialogue);
        Assert.Equal(Facing.Left, npc.Facing);

        var warp = Assert.IsType<WarpEntity>(map.Entities[2]);
        Assert.Equal(EntityId.Parse("map:test_room"), warp.Target);
        Assert.Equal(WarpTransition.Door, warp.Transition);
    }

    [Fact]
    public void MapEntities_RoundTripThroughJson()
    {
        Map map = LoadFixtureMin().Find<Map>(EntityId.Parse("map:test_room"))!;
        // The polymorphic "kind" discriminator must survive a serialize→deserialize cycle.
        string json = Cgm.Core.Serialization.CgmJson.Serialize(map);
        Map back = Cgm.Core.Serialization.CgmJson.Deserialize<Map>(json);
        Assert.IsType<NpcEntity>(back.Entities[1]);
        Assert.IsType<WarpEntity>(back.Entities[2]);
    }

    /// <summary>The v11 <c>object</c> placement kind carries an <c>object:*</c> reference and survives
    /// the polymorphic round-trip like every other kind.</summary>
    [Fact]
    public void ObjectPlacementEntity_RoundTripsWithItsReference()
    {
        var map = new Map
        {
            Id = EntityId.Parse("map:m"), Width = 4, Height = 4,
            Entities = [new ObjectEntity { Key = "clinic", Pos = new GridPos(1, 2), Object = EntityId.Parse("object:clinic") }],
        };

        Map back = Cgm.Core.Serialization.CgmJson.Deserialize<Map>(Cgm.Core.Serialization.CgmJson.Serialize(map));
        var placed = Assert.IsType<ObjectEntity>(Assert.Single(back.Entities));
        Assert.Equal("clinic", placed.Key);
        Assert.Equal(new GridPos(1, 2), placed.Pos);
        Assert.Equal(EntityId.Parse("object:clinic"), placed.Object);

        // The reference is reachable by the reflection walk, so broken-reference validation covers it.
        Assert.Contains(EntityId.Parse("object:clinic"), EntityReferences.Collect(placed));
    }

    [Fact]
    public void Contains_And_Find_BehaveForMissing()
    {
        Project p = LoadFixtureMin();
        Assert.True(p.Contains(EntityId.Parse("item:potion")));
        Assert.False(p.Contains(EntityId.Parse("item:nonexistent")));
        Assert.Null(p.Find<Species>(EntityId.Parse("species:missing")));
    }

    [Fact]
    public void Load_Throws_WhenIdSlugDoesNotMatchFileName()
    {
        using var t = new TempProject();
        t.WriteData("type", "fire.json", """{ "schemaVersion":1, "id":"type:water", "name":"X" }""");
        Assert.Throws<InvalidDataException>(() => ProjectLoader.Load(t.Dir));
    }

    [Fact]
    public void Load_Throws_WhenEntityInWrongCategoryFolder()
    {
        using var t = new TempProject();
        // A move file placed under data/item/
        t.WriteData("item", "ember.json", """{ "schemaVersion":1, "id":"move:ember", "name":"X" }""");
        Assert.Throws<InvalidDataException>(() => ProjectLoader.Load(t.Dir));
    }

    [Fact]
    public void Load_ReadsAbilityEntities()
    {
        using var t = new TempProject();
        t.WriteData("ability", "sturdy_root.json",
            """{ "schemaVersion":2, "id":"ability:sturdy_root", "name":"Sturdy Root", "hooks":[{"hook":"onSwitchIn","effects":[{"op":"weatherSummon"}]}] }""");

        Ability ability = ProjectLoader.Load(t.Dir).Find<Ability>(EntityId.Parse("ability:sturdy_root"))!;
        Assert.Equal(AbilityHookPoint.OnSwitchIn, ability.Hooks.Single().Hook);
        Assert.Equal("weatherSummon", ability.Hooks.Single().Effects.Single().Op);
    }

    /// <summary>A throwaway project folder with a minimal valid settings file.</summary>
    private sealed class TempProject : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "cgm-load-" + Guid.NewGuid().ToString("N"));

        public TempProject()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, ProjectFile.FileName),
                """{ "schemaVersion":1, "id":"project:main", "name":"Temp" }""");
        }

        public void WriteData(string category, string fileName, string json)
        {
            string dir = Path.Combine(Dir, "data", category);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, fileName), json);
        }

        public void Dispose()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
        }
    }
}
