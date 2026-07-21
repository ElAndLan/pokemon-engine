using System.Text;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>16A raw/pack parity for assets: booting the same content from a project folder and from
/// an exported pack must yield the same asset bytes, so art behaves identically in playtest and in
/// a shipped game (ADR-006).</summary>
public sealed class BootAssetParityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cgm-boot-assets").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string AssetPath = "assets/sheet.png";
    private static readonly byte[] AssetBytes = Encoding.UTF8.GetBytes("pretend-png-bytes");

    private string Dir(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    private static Project Fixture(string root)
    {
        EntityId mapId = EntityId.Parse("map:field");
        EntityId typeId = EntityId.Parse("type:plain");
        EntityId speciesId = EntityId.Parse("species:pebbling");
        EntityId sheetId = EntityId.Parse("sheet:world");

        return new Project(
            new ProjectSettings
            {
                Name = "Asset Fixture", StartMap = mapId, StarterParty = [speciesId],
            },
            new Dictionary<EntityId, IEntity>
            {
                [mapId] = new Map
                {
                    Id = mapId, Name = "Field", Width = 2, Height = 2,
                    Layers = new MapLayers { Ground = [0, 0, 0, 0] },
                    Entities = [new PlayerStartEntity { Key = "start", Pos = new GridPos(0, 0) }],
                },
                [speciesId] = new Species
                {
                    Id = speciesId, Name = "Pebbling", Types = [typeId],
                    BaseStats = new Stats(40, 40, 40, 40, 40, 40), BaseExp = 50,
                },
                [typeId] = new TypeDef { Id = typeId, Name = "Plain" },
                [sheetId] = new SpriteSheet
                {
                    Id = sheetId, Name = "World", Asset = AssetPath,
                    ImageW = 32, ImageH = 16, Mode = SliceMode.Grid, CellW = 16, CellH = 16,
                    Cells = [new SheetCell { Index = 0, SpriteId = EntityId.Parse("sprite:grass") }],
                },
            },
            root);
    }

    /// <summary>Writes the fixture as a loadable project folder, assets included.</summary>
    private string WriteProject()
    {
        string folder = Dir($"proj-{Guid.NewGuid():N}");
        Project project = Fixture(folder);
        ProjectFile.Save(folder, project.Settings);
        foreach (IEntity entity in project.Entities)
        {
            string dir = Directory.CreateDirectory(Path.Combine(folder, "data",
                entity.Id.Category.ToString().ToLowerInvariant())).FullName;
            File.WriteAllText(Path.Combine(dir, $"{entity.Id.Slug}.json"), CgmJson.SerializeEntity(entity));
        }

        string asset = Path.Combine(folder, "assets", "sheet.png");
        Directory.CreateDirectory(Path.GetDirectoryName(asset)!);
        File.WriteAllBytes(asset, AssetBytes);
        return folder;
    }

    private string ExportFrom(string projectFolder)
    {
        string outFolder = Dir($"exp-{Guid.NewGuid():N}");
        Exporter.ExportData(ProjectLoader.Load(projectFolder), new ExportOptions(), outFolder);
        return outFolder;
    }

    private static RuntimeContent Load(BootArgs args, string exeDir)
    {
        Assert.True(BootLoader.TryLoad(args, exeDir, out RuntimeContent? content, out BootDiagnostic? error),
            error?.Summary);
        return content!;
    }

    private static RuntimeContent LoadProject(string folder) =>
        Load(Parse(["--project", folder]), folder);

    private static RuntimeContent LoadExported(string folder) => Load(Parse([]), folder);

    private static BootArgs Parse(string[] argv)
    {
        Assert.True(BootArgs.TryParse(argv, out BootArgs? args, out BootDiagnostic? error), error?.Summary);
        return args!;
    }

    // --- Parity ----------------------------------------------------------------------

    [Fact]
    public void BothModesDeliverTheSameAssetBytes()
    {
        string projectFolder = WriteProject();
        string exportFolder = ExportFrom(projectFolder);

        Assert.True(LoadProject(projectFolder).Assets.TryRead(AssetPath, out byte[] raw));
        Assert.True(LoadExported(exportFolder).Assets.TryRead(AssetPath, out byte[] packed));

        Assert.Equal(AssetBytes, raw);
        Assert.Equal(AssetBytes, packed);
    }

    [Fact]
    public void BothModesAgreeThatAnAbsentAssetIsAbsent()
    {
        string projectFolder = WriteProject();
        string exportFolder = ExportFrom(projectFolder);

        Assert.False(LoadProject(projectFolder).Assets.TryRead("assets/nope.png", out _));
        Assert.False(LoadExported(exportFolder).Assets.TryRead("assets/nope.png", out _));
    }

    /// <summary>Content is untrusted in both modes; neither may read outside the game.</summary>
    [Fact]
    public void NeitherModeEscapesItsContent()
    {
        string projectFolder = WriteProject();
        string exportFolder = ExportFrom(projectFolder);

        Assert.False(LoadProject(projectFolder).Assets.TryRead("../../escape.png", out _));
        Assert.False(LoadExported(exportFolder).Assets.TryRead("../../escape.png", out _));
    }

    /// <summary>A source is always present, so scenes never null-check before asking for art.</summary>
    [Fact]
    public void ARuntimeContentAlwaysCarriesAnAssetSource()
    {
        var content = new RuntimeContent(
            new GameDb(new ProjectSettings { Name = "T" }, []),
            new Map { Id = EntityId.Parse("map:m"), Width = 1, Height = 1 },
            new RuntimeConfig());

        Assert.NotNull(content.Assets);
        Assert.False(content.Assets.TryRead(AssetPath, out _));
    }
}
