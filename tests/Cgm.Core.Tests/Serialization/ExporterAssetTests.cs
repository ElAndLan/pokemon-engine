using System.Security.Cryptography;
using System.Text;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

/// <summary>Export embeds the images a project's sheets reference (EXPORT_PIPELINE_SPEC asset
/// sections), so an exported game carries its own art and needs no source folder.</summary>
public sealed class ExporterAssetTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cgm-export-assets").FullName;
    private readonly string _out = Directory.CreateTempSubdirectory("cgm-export-out").FullName;

    public void Dispose()
    {
        foreach (string dir in new[] { _root, _out })
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
    }

    private static readonly EntityId MapId = EntityId.Parse("map:field");

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Writes an asset into the project and returns its bytes.</summary>
    private byte[] WriteAsset(string relative, string content)
    {
        string full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(full, bytes);
        return bytes;
    }

    private static SpriteSheet Sheet(string slug, string asset, string? hash = null) => new()
    {
        Id = EntityId.Parse("sheet:" + slug), Name = slug, Asset = asset, ContentHash = hash,
        ImageW = 32, ImageH = 16, Mode = SliceMode.Grid, CellW = 16, CellH = 16,
        Cells = [new SheetCell { Index = 0, SpriteId = EntityId.Parse("sprite:" + slug + "_a") }],
    };

    private Project Project(params SpriteSheet[] sheets)
    {
        var entities = new Dictionary<EntityId, IEntity>
        {
            [MapId] = new Map
            {
                Id = MapId, Name = "Field", Width = 2, Height = 2,
                Layers = new MapLayers { Ground = [0, 0, 0, 0] },
            },
        };
        foreach (SpriteSheet sheet in sheets)
            entities[sheet.Id] = sheet;

        return new Project(new ProjectSettings { Name = "T", StartMap = MapId }, entities, _root);
    }

    private PackContent Export(Project project)
    {
        Exporter.ExportData(project, new ExportOptions(OverrideValidation: true), _out);
        using FileStream fs = File.OpenRead(Path.Combine(_out, Exporter.PackFileName));
        return CgmPack.Read(fs);
    }

    // --- Embedding ------------------------------------------------------------------

    [Fact]
    public void AReferencedAssetIsEmbeddedByteForByte()
    {
        byte[] bytes = WriteAsset("assets/a.png", "image-a");
        PackContent content = Export(Project(Sheet("a", "assets/a.png")));

        Assert.Equal(bytes, content.Assets["assets/a.png"]);
    }

    [Fact]
    public void TwoSheetsSlicingOneImageEmbedItOnce()
    {
        WriteAsset("assets/shared.png", "shared");
        PackContent content = Export(Project(
            Sheet("a", "assets/shared.png"), Sheet("b", "assets/shared.png")));

        Assert.Equal(["assets/shared.png"], content.Assets.Keys);
    }

    [Fact]
    public void HostSeparatorsInAuthoredPathsStillResolve()
    {
        byte[] bytes = WriteAsset("assets/sub/a.png", "nested");
        PackContent content = Export(Project(Sheet("a", @"assets\sub\a.png")));

        Assert.Equal(bytes, content.Assets["assets/sub/a.png"]);
    }

    [Fact]
    public void AProjectWithNoSheetsExportsNoAssets() =>
        Assert.Empty(Export(Project()).Assets);

    // --- Integrity gates ------------------------------------------------------------

    [Fact]
    public void AMatchingContentHashIsAccepted()
    {
        byte[] bytes = WriteAsset("assets/a.png", "image-a");
        PackContent content = Export(Project(Sheet("a", "assets/a.png", Hash(bytes))));

        Assert.Equal(bytes, content.Assets["assets/a.png"]);
    }

    /// <summary>An image edited after import would ship art the author never reviewed.</summary>
    [Fact]
    public void AChangedAssetAbortsTheExport()
    {
        byte[] original = WriteAsset("assets/a.png", "image-a");
        WriteAsset("assets/a.png", "image-a-edited");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Export(Project(Sheet("a", "assets/a.png", Hash(original)))));
        Assert.Contains("has changed", ex.Message);
    }

    [Fact]
    public void AMissingAssetAbortsTheExport() =>
        Assert.Throws<FileNotFoundException>(() => Export(Project(Sheet("a", "assets/gone.png"))));

    [Theory]
    [InlineData("")]
    [InlineData("../outside.png")]
    [InlineData("/etc/passwd")]
    public void AnUnsafeAssetPathAbortsTheExport(string path)
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Export(Project(Sheet("a", path))));
        Assert.Contains("unsafe", ex.Message);
    }

    /// <summary>A project built in memory has no folder, so its assets are unreadable. That must be
    /// an error rather than an export that silently ships no art.</summary>
    [Fact]
    public void ARootlessProjectCannotExportAssets()
    {
        var project = new Project(
            new ProjectSettings { Name = "T", StartMap = MapId },
            new Dictionary<EntityId, IEntity>
            {
                [MapId] = new Map
                {
                    Id = MapId, Name = "Field", Width = 2, Height = 2,
                    Layers = new MapLayers { Ground = [0, 0, 0, 0] },
                },
                [EntityId.Parse("sheet:a")] = Sheet("a", "assets/a.png"),
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Export(project));
        Assert.Contains("no folder", ex.Message);
    }
}
