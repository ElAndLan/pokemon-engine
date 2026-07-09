using Cgm.Runtime.Engine;
using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Runtime.Tests;

public sealed class VirtualResolutionTests
{
    [Fact]
    public void Fit_IntegerScalesAndCenters()
    {
        // 960×640 window, 240×160 virtual → scale 4, fills exactly (no letterbox).
        Viewport v = VirtualResolution.Fit(960, 640, 240, 160);
        Assert.Equal(4, v.Scale);
        Assert.Equal(0, v.OffsetX);
        Assert.Equal(0, v.OffsetY);
        Assert.Equal(960, v.Width);
    }

    [Fact]
    public void Fit_LetterboxesWhenAspectDiffers()
    {
        // 1000×640 window, 240×160 virtual → limiting axis is height (640/160=4), width has slack.
        Viewport v = VirtualResolution.Fit(1000, 640, 240, 160);
        Assert.Equal(4, v.Scale);
        Assert.Equal((1000 - 960) / 2, v.OffsetX); // horizontal letterbox
        Assert.Equal(0, v.OffsetY);
    }

    [Fact]
    public void Fit_WindowSmallerThanVirtual_ClampsScaleToOne()
    {
        Viewport v = VirtualResolution.Fit(100, 100, 240, 160);
        Assert.Equal(1, v.Scale);
    }

    [Fact]
    public void Fit_RejectsNonPositiveVirtual()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VirtualResolution.Fit(960, 640, 0, 160));
    }
}

public sealed class CameraTests
{
    [Fact]
    public void Clamp_CentersOnTargetInMiddleOfLargeMap()
    {
        // view 240×160, map 1000×1000, target at (500,500) → cam (500-120, 500-80).
        var (x, y) = Camera.Clamp(500, 500, 240, 160, 1000, 1000);
        Assert.Equal(380, x);
        Assert.Equal(420, y);
    }

    [Fact]
    public void Clamp_StopsAtMapEdges()
    {
        // target at top-left corner → cam clamps to 0,0.
        Assert.Equal((0, 0), Camera.Clamp(0, 0, 240, 160, 1000, 1000));
        // target at bottom-right → cam clamps to map-view.
        Assert.Equal((1000 - 240, 1000 - 160), Camera.Clamp(1000, 1000, 240, 160, 1000, 1000));
    }

    [Fact]
    public void Clamp_CentersMapSmallerThanView()
    {
        // map 100 wide, view 240 → centered: -(240-100)/2 = -70.
        var (x, _) = Camera.Clamp(50, 50, 240, 160, 100, 100);
        Assert.Equal(-70, x);
    }
}

public sealed class InputStateTests
{
    [Fact]
    public void WasPressed_TrueOnlyOnRisingEdge()
    {
        var input = new InputState();
        input.Update([GameAction.Confirm]);
        Assert.True(input.WasPressed(GameAction.Confirm));
        Assert.True(input.IsDown(GameAction.Confirm));

        input.Update([GameAction.Confirm]); // still held
        Assert.False(input.WasPressed(GameAction.Confirm));
        Assert.True(input.IsDown(GameAction.Confirm));
    }

    [Fact]
    public void WasReleased_TrueOnlyOnFallingEdge()
    {
        var input = new InputState();
        input.Update([GameAction.Up]);
        input.Update([]); // released this frame
        Assert.True(input.WasReleased(GameAction.Up));
        Assert.False(input.IsDown(GameAction.Up));

        input.Update([]); // stays up
        Assert.False(input.WasReleased(GameAction.Up));
    }

    [Fact]
    public void UnrelatedActions_AreIndependent()
    {
        var input = new InputState();
        input.Update([GameAction.Left]);
        Assert.False(input.IsDown(GameAction.Right));
        Assert.True(input.WasPressed(GameAction.Left));
    }
}

public sealed class SceneStackTests
{
    [Fact]
    public void PushPopPeek_Order()
    {
        var stack = new SceneStack<string>();
        Assert.Null(stack.Active);
        Assert.Equal(0, stack.Count);

        stack.Push("overworld");
        stack.Push("battle");
        Assert.Equal("battle", stack.Active);
        Assert.Equal(2, stack.Count);

        Assert.Equal("battle", stack.Pop());
        Assert.Equal("overworld", stack.Active);
    }

    [Fact]
    public void Pop_EmptyReturnsNull()
    {
        Assert.Null(new SceneStack<string>().Pop());
    }

    [Fact]
    public void Replace_SwapsTop_OrPushesWhenEmpty()
    {
        var stack = new SceneStack<string>();
        stack.Replace("title");        // empty → push
        Assert.Equal("title", stack.Active);
        Assert.Equal(1, stack.Count);

        stack.Push("overworld");
        stack.Replace("menu");         // swaps overworld → menu, base unchanged
        Assert.Equal("menu", stack.Active);
        Assert.Equal(2, stack.Count);
    }
}

public sealed class BattleSceneTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId MoveA = EntityId.Parse("move:pulse");
    private static readonly EntityId MoveB = EntityId.Parse("move:giant_pulse");
    private static readonly EntityId SpeciesA = EntityId.Parse("species:flareling");
    private static readonly EntityId SpeciesB = EntityId.Parse("species:shadebud");

    [Fact]
    public void Menu_ListsOnlyLegalFormActivations()
    {
        var scene = new BattleScene(TimedFormBattle(), _ => new UseMove(0),
            [new BattleFormChoice("giant", 0), new BattleFormChoice("missing", 0)]);

        Assert.Contains(scene.Menu, i => i.Action is ActivateForm { FormId: "giant" });
        Assert.DoesNotContain(scene.Menu, i => i.Action is ActivateForm { FormId: "missing" });
    }

    [Fact]
    public void Confirm_SubmitsActivateFormAndPresentsFormChanged()
    {
        var scene = new BattleScene(TimedFormBattle(), _ => new UseMove(0),
            [new BattleFormChoice("giant", 0)]);
        var input = new InputState();

        input.Update([GameAction.Down]);
        scene.Update(input);
        input.Update([GameAction.Confirm]);
        scene.Update(input);

        Assert.Contains(scene.Presented, line => line == "Player changed form to giant");
    }

    private static BattleController TimedFormBattle()
    {
        GameDb db = new(new ProjectSettings { Name = "Runtime Battle Test" },
        [
            new TypeDef { Id = Normal, Name = "Normal" },
            new Move
            {
                Id = MoveA,
                Name = "Pulse",
                Type = Normal,
                DamageClass = DamageClass.Status,
                Pp = 10,
                Accuracy = 100,
            },
            new Move
            {
                Id = MoveB,
                Name = "Giant Pulse",
                Type = Normal,
                DamageClass = DamageClass.Status,
                Pp = 10,
                Accuracy = 100,
            },
            new Species
            {
                Id = SpeciesA,
                Name = "Flareling",
                Types = [Normal],
                BaseStats = new Stats(80, 40, 40, 40, 40, 40),
                Forms =
                [
                    new Form
                    {
                        FormId = "giant",
                        Activation = FormActivation.BattleTimed,
                        Turns = 2,
                        MoveRemap = new Dictionary<EntityId, EntityId> { [MoveA] = MoveB },
                    },
                ],
            },
            new Species
            {
                Id = SpeciesB,
                Name = "Shadebud",
                Types = [Normal],
                BaseStats = new Stats(80, 40, 40, 40, 40, 40),
            },
        ]);

        var player = BattleCreature.FromInstance(new CreatureInstance
        {
            Species = SpeciesA,
            Level = 50,
            CurHp = 120,
            Moves = [new MoveSlot(MoveA, 10)],
        }, db);
        var enemy = BattleCreature.FromInstance(new CreatureInstance
        {
            Species = SpeciesB,
            Level = 50,
            CurHp = 120,
            Moves = [new MoveSlot(MoveA, 10)],
        }, db);

        return new BattleController(player, enemy, new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));
    }
}

public sealed class ExportedGameBootTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "cgm-runtime-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_out))
            Directory.Delete(_out, recursive: true);
    }

    [Fact]
    public void Load_ReadsConfigPackStartMapAndShowcaseBattle()
    {
        Project project = ProjectLoader.Load(Sample("demo-game"));
        Exporter.ExportData(project, new ExportOptions(BuildTimestampUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), _out);

        ExportedGame game = ExportedGameBoot.Load(_out);

        Assert.Equal("Demo Game", game.Config.GameName);
        Assert.Equal(EntityId.Parse("map:showcase_room"), game.StartMap.Id);
        Assert.Contains(game.ShowcaseBattle.Menu, i => i.Action is ActivateForm);
    }

    [Fact]
    public void Smoke_LoadsPackAndResolvesOneShowcaseAction()
    {
        Project project = ProjectLoader.Load(Sample("demo-game"));
        Exporter.ExportData(project, new ExportOptions(), _out);

        ExportedGameBoot.Smoke(_out);
    }

    private static string Sample(string name) => Path.Combine(RepoRoot(), "samples", name);

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "samples")))
            dir = Directory.GetParent(dir)?.FullName ?? throw new DirectoryNotFoundException("Could not find repo root.");
        return dir;
    }
}