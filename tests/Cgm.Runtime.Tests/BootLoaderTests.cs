using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>16A steps 2-7: one GameDb from either data source, categorized failures, raw/pack parity.</summary>
public sealed class BootLoaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cgm-boot").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Dir(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    /// <summary>The smallest project that passes Core validation: one neutral type and species, one
    /// 2x2 map carrying the single required player-start, and a one-member starter party.</summary>
    private static Project Fixture(EntityId? startMap = null, GridPos start = default)
    {
        EntityId mapId = EntityId.Parse("map:field");
        EntityId typeId = EntityId.Parse("type:plain");
        EntityId speciesId = EntityId.Parse("species:pebbling");
        var map = new Map
        {
            Id = mapId,
            Name = "Field",
            Width = 2,
            Height = 2,
            Layers = new MapLayers { Ground = [0, 0, 0, 0] },
            Entities = [new PlayerStartEntity { Pos = new GridPos(0, 0) }],
        };
        var species = new Species
        {
            Id = speciesId,
            Name = "Pebbling",
            Types = [typeId],
            BaseStats = new Stats(40, 40, 40, 40, 40, 40),
            BaseExp = 50,
        };
        var type = new TypeDef { Id = typeId, Name = "Plain" };
        var settings = new ProjectSettings
        {
            Name = "Boot Fixture",
            StartMap = startMap ?? mapId,
            StartPos = start,
            StarterParty = [speciesId],
        };
        return new Project(settings, new Dictionary<EntityId, IEntity>
        {
            [mapId] = map,
            [speciesId] = species,
            [typeId] = type,
        });
    }

    /// <summary>Writes a pack directly from a GameDb, bypassing validation. Packed content is not
    /// re-validated at boot, so this is the only way to reach the step-7 start-state checks.</summary>
    private string WriteExportUnvalidated(ProjectSettings settings, IEnumerable<IEntity> entities)
    {
        string folder = Dir($"raw-{Guid.NewGuid():N}");
        using (FileStream pack = File.Create(Path.Combine(folder, "game.cgmpack")))
            CgmPack.Write(new GameDb(settings, entities.ToList()), pack);
        File.WriteAllText(Path.Combine(folder, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            VirtualWidth = 256,
            VirtualHeight = 192,
            PackPath = "game.cgmpack",
        }));
        return folder;
    }

    private string WriteProject(Project project)
    {
        string folder = Dir($"proj-{Guid.NewGuid():N}");
        ProjectFile.Save(folder, project.Settings);
        foreach (IEntity entity in project.Entities)
        {
            // ProjectLoader requires data/<category>/<slug>.json with filename == id slug.
            string dir = Directory.CreateDirectory(Path.Combine(folder, "data",
                entity.Id.Category.ToString().ToLowerInvariant())).FullName;
            File.WriteAllText(Path.Combine(dir, $"{entity.Id.Slug}.json"), CgmJson.SerializeEntity(entity));
        }
        return folder;
    }

    private string WriteExport(Project project, string packName = "game.cgmpack", string? packPathOverride = null)
    {
        string folder = Dir($"exp-{Guid.NewGuid():N}");
        using (FileStream pack = File.Create(Path.Combine(folder, packName)))
            CgmPack.Write(GameDb.FromProject(project), pack);
        File.WriteAllText(Path.Combine(folder, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            GameName = "Boot Fixture",
            VirtualWidth = 256,
            VirtualHeight = 192,
            PackPath = packPathOverride ?? packName,
        }));
        return folder;
    }

    private static string[] Canonical(GameDb db) => db.Entities
        .OrderBy(e => e.Id.ToString(), StringComparer.Ordinal)
        .Select(e => $"{e.Id}={CgmJson.SerializeEntity(e)}")
        .ToArray();

    private static BootArgs Project(string folder) => new(folder, false, false, null);
    private static BootArgs Exported() => new(null, false, false, null);

    private static RuntimeContent Ok(BootArgs args, string exeDir)
    {
        Assert.True(BootLoader.TryLoad(args, exeDir, out RuntimeContent? content, out BootDiagnostic? error),
            error?.Summary);
        Assert.Null(error);
        return content!;
    }

    private static BootDiagnostic Rejected(BootArgs args, string exeDir, RuntimeExit expected)
    {
        Assert.False(BootLoader.TryLoad(args, exeDir, out RuntimeContent? content, out BootDiagnostic? error));
        Assert.Null(content);
        BootDiagnostic diagnostic = Assert.IsType<BootDiagnostic>(error);
        Assert.Equal(expected, diagnostic.Exit);
        return diagnostic;
    }

    [Fact]
    public void RawProject_LoadsAndResolvesStartState()
    {
        RuntimeContent content = Ok(Project(WriteProject(Fixture())), _root);
        Assert.Equal(EntityId.Parse("map:field"), content.StartMap.Id);
        Assert.Equal("Boot Fixture", content.Config.GameName);
    }

    [Fact]
    public void ExportedPack_LoadsAndResolvesStartState()
    {
        string exe = WriteExport(Fixture());
        RuntimeContent content = Ok(Exported(), exe);
        Assert.Equal(EntityId.Parse("map:field"), content.StartMap.Id);
        Assert.Equal(256, content.Config.VirtualWidth);
    }

    /// <summary>The headline 16A acceptance item: both sources produce the same canonical database.</summary>
    [Fact]
    public void RawAndPacked_ProduceEqualDatabases()
    {
        Project fixture = Fixture();
        RuntimeContent raw = Ok(Project(WriteProject(fixture)), _root);
        RuntimeContent packed = Ok(Exported(), WriteExport(fixture));

        // Records holding IReadOnlyList compare by reference, so parity is by canonical serialized
        // value and ordinal ID order, exactly as ENGINE_RUNTIME_SPEC 16A defines it.
        Assert.Equal(CgmJson.Serialize(raw.Db.Settings), CgmJson.Serialize(packed.Db.Settings));
        Assert.Equal(CgmJson.SerializeEntity(raw.StartMap), CgmJson.SerializeEntity(packed.StartMap));
        Assert.Equal(Canonical(raw.Db), Canonical(packed.Db));
    }

    [Fact]
    public void MissingProjectFolder_IsAnArgumentError() =>
        Assert.Contains("does not exist",
            Rejected(Project(Path.Combine(_root, "absent")), _root, RuntimeExit.Arguments).Summary);

    [Fact]
    public void ProjectPathThatIsAFile_IsAnArgumentError()
    {
        string file = Path.Combine(_root, "not-a-folder.txt");
        File.WriteAllText(file, "x");
        Assert.Contains("not a folder", Rejected(Project(file), _root, RuntimeExit.Arguments).Summary);
    }

    [Fact]
    public void MissingConfig_IsAnArgumentError() =>
        Assert.Contains("config.json", Rejected(Exported(), Dir("empty"), RuntimeExit.Arguments).Summary);

    [Fact]
    public void MissingPackFile_IsAnArgumentError()
    {
        string exe = WriteExport(Fixture());
        File.Delete(Path.Combine(exe, "game.cgmpack"));
        Assert.Contains("does not exist", Rejected(Exported(), exe, RuntimeExit.Arguments).Summary);
    }

    [Theory]
    [InlineData("../outside.cgmpack")]
    [InlineData("sub/../../outside.cgmpack")]
    [InlineData("")]
    public void PackPathEscapingTheGameFolder_IsRejected(string packPath)
    {
        string exe = WriteExport(Fixture(), packPathOverride: packPath);
        Assert.Contains("escapes", Rejected(Exported(), exe, RuntimeExit.Arguments).Summary);
    }

    /// <summary>A sibling folder sharing a name prefix is outside the root, not inside it.</summary>
    [Fact]
    public void PackPathIntoPrefixSiblingFolder_IsRejected()
    {
        string exe = Dir("game");
        Dir("gameEvil");
        File.WriteAllText(Path.Combine(_root, "gameEvil", "evil.cgmpack"), "x");
        File.WriteAllText(Path.Combine(exe, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            VirtualWidth = 256,
            VirtualHeight = 192,
            PackPath = "../gameEvil/evil.cgmpack",
        }));
        Assert.Contains("escapes", Rejected(Exported(), exe, RuntimeExit.Arguments).Summary);
    }

    [Theory]
    [InlineData(0, 192)]
    [InlineData(256, 0)]
    [InlineData(-1, 192)]
    public void NonPositiveVirtualResolution_IsAnArgumentError(int width, int height)
    {
        string exe = Dir($"res-{width}-{height}");
        File.WriteAllText(Path.Combine(exe, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            VirtualWidth = width,
            VirtualHeight = height,
        }));
        Assert.Contains("positive", Rejected(Exported(), exe, RuntimeExit.Arguments).Summary);
    }

    [Fact]
    public void ConfigSchemaNewerThanRuntime_IsAContentError()
    {
        string exe = Dir("newer");
        File.WriteAllText(Path.Combine(exe, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            SchemaVersion = SchemaVersions.Current + 1,
            VirtualWidth = 256,
            VirtualHeight = 192,
        }));
        Assert.Contains("newer", Rejected(Exported(), exe, RuntimeExit.Content).Summary);
    }

    [Fact]
    public void CorruptPack_IsAContentErrorNotACrash()
    {
        string exe = WriteExport(Fixture());
        File.WriteAllBytes(Path.Combine(exe, "game.cgmpack"), [0, 1, 2, 3, 4, 5, 6, 7]);
        Rejected(Exported(), exe, RuntimeExit.Content);
    }

    [Fact]
    public void TamperedPackBody_FailsTheContentHash()
    {
        string exe = WriteExport(Fixture());
        string pack = Path.Combine(exe, "game.cgmpack");
        byte[] bytes = File.ReadAllBytes(pack);
        bytes[^1] ^= 0xFF;
        bytes[^2] ^= 0xFF;
        File.WriteAllBytes(pack, bytes);
        Rejected(Exported(), exe, RuntimeExit.Content);
    }

    /// <summary>Raw mode runs the Core validator, so an invalid project never reaches step 7.</summary>
    [Fact]
    public void RawProjectFailingValidation_IsAContentError()
    {
        Project fixture = Fixture();
        var noParty = new Project(fixture.Settings with { StarterParty = [] },
            fixture.Entities.ToDictionary(e => e.Id));
        Assert.Contains("validation",
            Rejected(Project(WriteProject(noParty)), _root, RuntimeExit.Content).Summary);
    }

    [Fact]
    public void PackedWithNoStartMap_IsAContentError()
    {
        Project fixture = Fixture();
        string exe = WriteExportUnvalidated(fixture.Settings with { StartMap = null }, fixture.Entities);
        Assert.Contains("no start map", Rejected(Exported(), exe, RuntimeExit.Content).Summary);
    }

    [Fact]
    public void PackedWithMissingStartMap_IsAContentErrorCarryingTheId()
    {
        Project fixture = Fixture();
        string exe = WriteExportUnvalidated(
            fixture.Settings with { StartMap = EntityId.Parse("map:absent") }, fixture.Entities);
        BootDiagnostic diagnostic = Rejected(Exported(), exe, RuntimeExit.Content);
        Assert.Equal("map:absent", diagnostic.Identifier);
        Assert.DoesNotContain(_root, diagnostic.Format());
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(-1, 0)]
    [InlineData(99, 99)]
    public void PackedStartPositionOutsideTheMap_IsAContentError(int x, int y)
    {
        Project fixture = Fixture();
        string exe = WriteExportUnvalidated(
            fixture.Settings with { StartPos = new GridPos(x, y) }, fixture.Entities);
        Assert.Contains("outside", Rejected(Exported(), exe, RuntimeExit.Content).Summary);
    }

    [Fact]
    public void StartPositionOnTheLastValidTile_IsAccepted()
    {
        Project fixture = Fixture();
        string exe = WriteExportUnvalidated(
            fixture.Settings with { StartPos = new GridPos(1, 1) }, fixture.Entities);
        Assert.Equal(new GridPos(1, 1), Ok(Exported(), exe).Db.Settings.StartPos);
    }

    /// <summary>No diagnostic may leak a host path: release output shows category and safe summary only.</summary>
    [Fact]
    public void Diagnostics_NeverContainHostPaths()
    {
        string missing = Path.Combine(_root, "absent");
        Assert.DoesNotContain(missing, Rejected(Project(missing), _root, RuntimeExit.Arguments).Format());
        Assert.DoesNotContain(_root, Rejected(Exported(), Dir("bare"), RuntimeExit.Arguments).Format());
    }

    [Fact]
    public void DebugFlag_PropagatesWithoutChangingContent()
    {
        string folder = WriteProject(Fixture());
        RuntimeContent plain = Ok(new BootArgs(folder, false, false, null), _root);
        RuntimeContent debug = Ok(new BootArgs(folder, true, false, null), _root);
        Assert.False(plain.Config.Debug);
        Assert.True(debug.Config.Debug);
        Assert.Equal(Canonical(plain.Db), Canonical(debug.Db));
        Assert.Equal(CgmJson.Serialize(plain.Db.Settings), CgmJson.Serialize(debug.Db.Settings));
    }
}
