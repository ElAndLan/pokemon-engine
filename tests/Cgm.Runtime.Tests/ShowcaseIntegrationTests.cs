using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Boots the exported demo-game pack through the real 16A loader and drives a battle with
/// Phase 15 mechanics, keeping end-to-end coverage that the retired showcase boot used to provide.</summary>
public sealed class ShowcaseIntegrationTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "cgm-showcase-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_out))
            Directory.Delete(_out, recursive: true);
    }

    private GameDb Export()
    {
        Project project = ProjectLoader.Load(TestRepo.Sample("demo-game"));
        Exporter.ExportData(project, new ExportOptions(
            BuildTimestampUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), _out);

        Assert.True(BootLoader.TryLoad(new BootArgs(null, false, false, null), _out,
            out RuntimeContent? content, out BootDiagnostic? error), error?.Summary);
        Assert.Equal(EntityId.Parse("map:showcase_room"), content!.StartMap.Id);
        Assert.Equal("Demo Game", content.Config.GameName);
        return content.Db;
    }

    [Fact]
    public void ExportedPack_BootsThroughTheRealLoader() => Assert.NotEmpty(Export().Entities);

    [Fact]
    public void ShowcaseScript_ProducesPhase15DemoEvents()
    {
        BattleScene scene = ShowcaseBattleFixture.Build(Export());

        void ResolvePending()
        {
            if (scene.PendingReplacementSlots.Count == 0)
                return;
            BattleSceneSnapshot snapshot = scene.Snapshot();
            scene.SubmitReplacements(scene.PendingReplacementSlots.Select(slot => new BattleReplacementSelection(slot,
                (slot.Side == BattleSide.Player ? snapshot.PlayerParty : snapshot.EnemyParty)
                    .Select((member, index) => (member, index))
                    .First(candidate => !candidate.member.IsActive && !candidate.member.IsFainted).index)).ToArray());
        }

        scene.Submit(new ActivateForm("bloom_guard", 0));
        ResolvePending();
        scene.Submit(new Switch(2));
        ResolvePending();
        for (int i = 0; i < 8 && scene.Snapshot().Outcome is null; i++)
        {
            if (scene.PendingReplacementSlots.Count > 0)
                ResolvePending();
            else
                scene.Submit(scene.Menu.First(item => item.Action is UseMove).Action);
        }

        Assert.Contains(scene.Events, e => e is WeatherChanged);
        Assert.Contains(scene.Events, e => e is FormChanged { FormId: "bloom_guard" });
        Assert.Contains(scene.Events, e => e is SwitchedIn { Side: BattleSide.Player });
        Assert.Contains(scene.Events, e => e is HeldItemConsumed { Op: "surviveFromFull" });
        Assert.Contains(scene.Events, e => e is BattleEnded);
    }
}
