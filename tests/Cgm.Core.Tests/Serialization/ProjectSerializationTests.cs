using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class ProjectSerializationTests
{
    private static ProjectSettings Sample() => new()
    {
        Name = "Demo",
        EngineVersion = "0.1",
        TileSize = 16,
        StartMap = EntityId.Parse("map:route_001"),
        StartPos = new GridPos(5, 7),
        StartFacing = Facing.Left,
        StarterParty = [EntityId.Parse("species:leafcub")],
    };

    [Fact]
    public void Serialize_IsByteStableAcrossRoundTrip()
    {
        string first = CgmJson.Serialize(Sample());
        string second = CgmJson.Serialize(CgmJson.Deserialize<ProjectSettings>(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        ProjectSettings original = Sample();
        ProjectSettings back = CgmJson.Deserialize<ProjectSettings>(CgmJson.Serialize(original));
        // Compare serialized forms: record equality would compare list properties by reference.
        Assert.Equal(CgmJson.Serialize(original), CgmJson.Serialize(back));
    }

    [Fact]
    public void Enums_SerializeAsCamelCaseStrings()
    {
        string json = CgmJson.Serialize(Sample());
        Assert.Contains("\"startFacing\": \"left\"", json);
        Assert.Contains("\"mode\": \"ingame\"", json);
    }

    [Fact]
    public void NullOptionalReference_IsOmitted()
    {
        string json = CgmJson.Serialize(new ProjectSettings { Name = "NoStart" });
        Assert.DoesNotContain("startMap", json);
    }

    [Fact]
    public void EntityIds_SerializeAsStrings()
    {
        string json = CgmJson.Serialize(Sample());
        Assert.Contains("\"startMap\": \"map:route_001\"", json);
        Assert.Contains("\"species:leafcub\"", json);
    }

    [Fact]
    public void FileRoundTrip_SaveThenLoad()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cgm-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProjectFile.Save(dir, Sample());
            Assert.True(File.Exists(Path.Combine(dir, ProjectFile.FileName)));
            Assert.Equal(CgmJson.Serialize(Sample()), CgmJson.Serialize(ProjectFile.Load(dir)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FixtureMin_LoadsAndAppliesDefaults()
    {
        ProjectSettings p = ProjectFile.Load(TestPaths.Sample("fixture-min"));
        Assert.Equal("Fixture Min", p.Name);
        Assert.Equal(16, p.TileSize);
        Assert.Equal(EntityId.Parse("map:test_room"), p.StartMap);
        Assert.Equal(Facing.Down, p.StartFacing);
        Assert.Equal([EntityId.Parse("species:leafcub")], p.StarterParty);
        // Fields absent in the fixture fall back to schema defaults:
        Assert.Equal(["items", "medicine", "balls", "key"], p.Pockets);
        Assert.Equal(8, p.Boxes.Count);
    }
}
