using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using static Cgm.Core.Tests.Validation.TestEntities;

namespace Cgm.Core.Tests.Serialization;

public sealed class ExporterTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "cgm-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_out))
            Directory.Delete(_out, recursive: true);
    }

    private static Project FixtureMin() => ProjectLoader.Load(TestPaths.Sample("fixture-min"));

    [Fact]
    public void ExportData_WritesPackAndConfig()
    {
        ExportResult result = Exporter.ExportData(FixtureMin(), new ExportOptions(), _out);

        Assert.True(File.Exists(result.PackPath));
        Assert.True(File.Exists(result.ConfigPath));
        Assert.Equal(Path.Combine(_out, "game.cgmpack"), result.PackPath);
        Assert.Equal(Path.Combine(_out, "config.json"), result.ConfigPath);
        Assert.False(result.Validation.HasErrors);
    }

    [Fact]
    public void ExportedPack_RoundTripsToSameGameDb()
    {
        Project project = FixtureMin();
        Exporter.ExportData(project, new ExportOptions(), _out);

        using FileStream packStream = File.OpenRead(Path.Combine(_out, "game.cgmpack"));
        GameDb fromPack = CgmPack.Read(packStream);
        GameDb fromFolder = GameDb.FromProject(project);
        Assert.Equal(fromFolder.Entities.Count, fromPack.Entities.Count);
        Assert.NotNull(fromPack.Find<Species>(EntityId.Parse("species:leafcub")));
    }

    [Fact]
    public void GeneratedConfig_DefaultsFromProject()
    {
        Project project = FixtureMin();
        Exporter.ExportData(project, new ExportOptions(), _out);

        RuntimeConfig config = CgmJson.Deserialize<RuntimeConfig>(File.ReadAllText(Path.Combine(_out, "config.json")));
        Assert.Equal(project.Settings.Name, config.GameName);
        Assert.Equal(project.Settings.Name, config.WindowTitle); // defaults to game name
        Assert.Equal("game.cgmpack", config.PackPath);
        Assert.Equal(240, config.VirtualWidth);
        Assert.Equal(160, config.VirtualHeight);
        Assert.False(config.Debug);
    }

    [Fact]
    public void Options_OverrideProjectDefaults()
    {
        var options = new ExportOptions(GameName: "Cool Game", WindowTitle: "Play Cool Game",
            SaveDirName: "coolsave", Debug: true);
        Exporter.ExportData(FixtureMin(), options, _out);

        RuntimeConfig config = CgmJson.Deserialize<RuntimeConfig>(File.ReadAllText(Path.Combine(_out, "config.json")));
        Assert.Equal("Cool Game", config.GameName);
        Assert.Equal("Play Cool Game", config.WindowTitle);
        Assert.Equal("coolsave", config.SaveDirName);
        Assert.True(config.Debug);
    }

    [Fact]
    public void ValidationErrors_BlockExport()
    {
        // A trainer with an empty party is a validation error; nothing should be written.
        Project broken = Project(Trainer("t") with { Party = [] });
        Assert.Throws<InvalidOperationException>(() => Exporter.ExportData(broken, new ExportOptions(), _out));
        Assert.False(File.Exists(Path.Combine(_out, "game.cgmpack")));
    }

    [Fact]
    public void OverrideValidation_ExportsDespiteErrors()
    {
        Project broken = Project(Trainer("t") with { Party = [] });
        ExportResult result = Exporter.ExportData(broken, new ExportOptions(OverrideValidation: true), _out);

        Assert.True(result.Validation.HasErrors); // still reported…
        Assert.True(File.Exists(result.PackPath)); // …but written anyway
    }
}
