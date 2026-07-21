using Cgm.Runtime.Engine;
using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Runtime.Tests;

public sealed class VirtualResolutionTests
{
    // The shipped default is 256x192 (Gen 4 DS-era single screen); 4x fills a 1024x768 client.
    private const int W = 256;
    private const int H = 192;

    [Fact]
    public void Fit_IntegerScalesAndCenters()
    {
        Viewport v = VirtualResolution.Fit(W * 4, H * 4, W, H);
        Assert.Equal(4, v.Scale);
        Assert.Equal(0, v.OffsetX);
        Assert.Equal(0, v.OffsetY);
        Assert.Equal(W * 4, v.Width);
        Assert.Equal(H * 4, v.Height);
    }

    [Fact]
    public void Fit_LetterboxesWhenAspectDiffers()
    {
        // Height is the limiting axis at 4x; the extra width becomes symmetric letterbox.
        Viewport v = VirtualResolution.Fit(W * 4 + 40, H * 4, W, H);
        Assert.Equal(4, v.Scale);
        Assert.Equal(20, v.OffsetX);
        Assert.Equal(0, v.OffsetY);
    }

    /// <summary>An odd remainder puts the extra pixel on the right/bottom (spec 16B viewport rule).</summary>
    [Fact]
    public void Fit_OddRemainder_LeavesTheExtraPixelOnTheRight()
    {
        const int windowW = W * 2 + 5;
        Viewport v = VirtualResolution.Fit(windowW, H * 2, W, H);
        int rightMargin = windowW - v.OffsetX - v.Width;

        Assert.Equal(2, v.Scale);
        Assert.Equal(2, v.OffsetX);
        Assert.Equal(3, rightMargin);
        Assert.True(rightMargin > v.OffsetX, "The odd pixel belongs on the right.");
    }

    [Fact]
    public void Fit_WindowSmallerThanVirtual_ClampsScaleToOne()
    {
        Viewport v = VirtualResolution.Fit(100, 100, W, H);
        Assert.Equal(1, v.Scale);
    }

    [Theory]
    [InlineData(0, H)]
    [InlineData(W, 0)]
    [InlineData(-1, H)]
    public void Fit_RejectsNonPositiveVirtual(int virtualW, int virtualH) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => VirtualResolution.Fit(960, 640, virtualW, virtualH));
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
    public void Menu_ListsLegalSwitchesAfterMoveAndFormActions()
    {
        var scene = new BattleScene(TimedFormBattle(), _ => new UseMove(0),
            [new BattleFormChoice("giant", 0)], id => id.Slug);

        Assert.IsType<UseMove>(scene.Menu[0].Action);
        Assert.IsType<ActivateForm>(scene.Menu[1].Action);
        Assert.Contains(scene.Menu, i => i.Action is Switch { PartyIndex: 1 });
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

    [Fact]
    public void SelectingSwitchSubmitsSwitchAndPresentsSwitchedIn()
    {
        var scene = new BattleScene(TimedFormBattle(), _ => new UseMove(0),
            [new BattleFormChoice("giant", 0)]);

        BattleMenuItem switchItem = scene.Menu.Single(i => i.Action is Switch { PartyIndex: 1 });
        scene.Submit(switchItem.Action);

        Assert.Contains(scene.Events, e => e is SwitchedIn { Side: BattleSide.Player, PartyIndex: 1 });
        Assert.Contains(scene.Presented, line => line == "Player switched in");
    }

    [Fact]
    public void Snapshot_ExposesHpMenuLogPartyAndOutcome()
    {
        var scene = new BattleScene(TimedFormBattle(), _ => new UseMove(0),
            [new BattleFormChoice("giant", 0)], id => id.Slug);

        BattleSceneSnapshot before = scene.Snapshot();
        Assert.Equal("flareling", before.PlayerName);
        Assert.True(before.PlayerHp > 0);
        Assert.Equal(2, before.PlayerParty.Count);
        Assert.Contains(before.Menu, i => i.Action is ActivateForm);
        Assert.Null(before.Outcome);

        scene.Submit(new ActivateForm("giant", 0));
        BattleSceneSnapshot after = scene.Snapshot();
        Assert.Contains(after.RecentLog, line => line.Contains("changed form"));
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
        var reserve = BattleCreature.FromInstance(new CreatureInstance
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

        return new BattleController([player, reserve], [enemy], new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));
    }
}
