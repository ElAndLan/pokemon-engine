using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16D movement: every collision, ledge, and camera decision comes from
/// Core, driven at fixed ticks through the real scene.</summary>
public sealed class OverworldSceneTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 256;
    private const int H = 192;

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public OverworldSceneTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    /// <summary>A map whose collision comes from explicit overrides, so tests state the terrain
    /// directly rather than depending on tileset lookup.</summary>
    private static Map Map(int width, int height, params (int X, int Y, CollisionValue Value)[] cells) =>
        new()
        {
            Id = EntityId.Parse("map:test"),
            Name = "Test",
            Width = width,
            Height = height,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, width * height).ToList() },
            CollisionOverrides = cells
                .Select(c => new CollisionOverride(c.Y * width + c.X, c.Value))
                .ToList(),
        };

    private OverworldScene Scene(Map map, GridPos start, Facing facing = Facing.Down)
    {
        var scene = new OverworldScene(_ui, map, [], start, facing, Tile, W, H);
        scene.Enter();
        return scene;
    }

    private static TickInput Hold(params GameAction[] held) =>
        new(held.ToHashSet(), held.ToHashSet(), new HashSet<GameAction>());

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    /// <summary>Runs enough ticks for a turn plus a step to complete.</summary>
    private static void Walk(OverworldScene scene, GameAction direction, int ticks = 24)
    {
        for (int i = 0; i < ticks; i++)
            scene.Update(Hold(direction));
    }

    // --- Movement through Core ----------------------------------------------------

    [Fact]
    public void StartsWhereItIsPlaced()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 4), Facing.Left);
        Assert.Equal(new GridPos(3, 4), scene.PlayerPos);
        Assert.Equal(Facing.Left, scene.PlayerFacing);
        Assert.Equal(MoverState.Idle, scene.PlayerState);
    }

    /// <summary>A tap turns without stepping; that is Core's rule, exercised through the scene.</summary>
    [Fact]
    public void TappingADirection_TurnsWithoutMoving()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3), Facing.Down);
        scene.Update(Hold(GameAction.Up));
        for (int i = 0; i < 6; i++)
            scene.Update(Idle);

        Assert.Equal(Facing.Up, scene.PlayerFacing);
        Assert.Equal(new GridPos(3, 3), scene.PlayerPos);
    }

    [Fact]
    public void HoldingADirection_TurnsThenSteps()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3), Facing.Down);
        Walk(scene, GameAction.Up);
        Assert.Equal(Facing.Up, scene.PlayerFacing);
        Assert.Equal(new GridPos(3, 2), scene.PlayerPos);
    }

    [Fact]
    public void WalkingAlreadyFacing_StepsWithoutATurnDelay()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3), Facing.Right);
        Walk(scene, GameAction.Right, ticks: 16);
        Assert.Equal(new GridPos(4, 3), scene.PlayerPos);
    }

    [Fact]
    public void SolidTiles_BlockTheStepButStillTurnThePlayer()
    {
        OverworldScene scene = Scene(Map(8, 8, (3, 2, CollisionValue.Solid)), new GridPos(3, 3));
        Walk(scene, GameAction.Up);

        Assert.Equal(Facing.Up, scene.PlayerFacing);
        Assert.Equal(new GridPos(3, 3), scene.PlayerPos);
    }

    [Fact]
    public void MapEdges_BlockMovement()
    {
        OverworldScene scene = Scene(Map(4, 4), new GridPos(0, 0), Facing.Up);
        Walk(scene, GameAction.Up);
        Assert.Equal(new GridPos(0, 0), scene.PlayerPos);
    }

    /// <summary>A ledge is one-way: hopping down clears two tiles, and the reverse is refused.</summary>
    [Fact]
    public void LedgeHop_ClearsTwoTilesInItsOwnDirection()
    {
        OverworldScene scene = Scene(Map(8, 8, (3, 4, CollisionValue.LedgeDown)), new GridPos(3, 3), Facing.Down);
        Walk(scene, GameAction.Down, ticks: 40);
        Assert.Equal(new GridPos(3, 5), scene.PlayerPos);
    }

    [Fact]
    public void LedgeFromTheWrongSide_IsBlocked()
    {
        OverworldScene scene = Scene(Map(8, 8, (3, 4, CollisionValue.LedgeDown)), new GridPos(3, 5), Facing.Up);
        Walk(scene, GameAction.Up, ticks: 40);
        Assert.Equal(new GridPos(3, 5), scene.PlayerPos);
    }

    [Fact]
    public void NpcsBlockTheTileTheyStandOn()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new NpcEntity { Key = "npc", Pos = new GridPos(3, 2) }],
        };
        OverworldScene scene = Scene(map, new GridPos(3, 3));
        Walk(scene, GameAction.Up);
        Assert.Equal(new GridPos(3, 3), scene.PlayerPos);
    }

    /// <summary>Opposite directions cancel in the resolver, so the player only turns to face.</summary>
    [Fact]
    public void OpposingDirections_ProduceNoMovement()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3));
        for (int i = 0; i < 24; i++)
            scene.Update(Hold(GameAction.Up, GameAction.Down));
        Assert.Equal(new GridPos(3, 3), scene.PlayerPos);
    }

    [Fact]
    public void NoInput_LeavesThePlayerIdle()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3));
        for (int i = 0; i < 30; i++)
            scene.Update(Idle);
        Assert.Equal(MoverState.Idle, scene.PlayerState);
        Assert.Equal(new GridPos(3, 3), scene.PlayerPos);
    }

    [Fact]
    public void ContinuedHolding_WalksSeveralTiles()
    {
        OverworldScene scene = Scene(Map(16, 16), new GridPos(1, 1), Facing.Right);
        Walk(scene, GameAction.Right, ticks: 16 * 3);
        Assert.Equal(4, scene.PlayerPos.X);
    }

    // --- Interaction --------------------------------------------------------------

    [Fact]
    public void FacingAnInteractable_ExposesItThroughCore()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new SignEntity { Key = "sign", Pos = new GridPos(3, 2), Text = "Hello" }],
        };
        OverworldScene scene = Scene(map, new GridPos(3, 3), Facing.Up);
        Assert.IsType<SignEntity>(scene.Facing);
    }

    [Fact]
    public void FacingNothing_ExposesNull() =>
        Assert.Null(Scene(Map(8, 8), new GridPos(3, 3), Facing.Up).Facing);

    /// <summary>Warps are step-on, not talk-to, so Core does not return them as interactables.</summary>
    [Fact]
    public void FacingAWarp_IsNotAnInteraction()
    {
        Map map = Map(8, 8) with
        {
            Entities =
            [
                new WarpEntity
                {
                    Key = "w", Pos = new GridPos(3, 2),
                    Target = EntityId.Parse("map:other"), TargetPos = new GridPos(0, 0),
                },
            ],
        };
        Assert.Null(Scene(map, new GridPos(3, 3), Facing.Up).Facing);
    }

    // --- Camera -------------------------------------------------------------------

    [Fact]
    public void Camera_CentresOnThePlayerInsideALargeMap()
    {
        OverworldScene scene = Scene(Map(64, 64), new GridPos(32, 32));
        Assert.Equal(32 * Tile + Tile / 2 - W / 2, scene.Camera.X);
        Assert.Equal(32 * Tile + Tile / 2 - H / 2, scene.Camera.Y);
    }

    [Fact]
    public void Camera_StopsAtMapEdges()
    {
        OverworldScene scene = Scene(Map(64, 64), new GridPos(0, 0));
        Assert.Equal(0, scene.Camera.X);
        Assert.Equal(0, scene.Camera.Y);
    }

    /// <summary>A map smaller than the viewport is centred, which means a negative camera.</summary>
    [Fact]
    public void Camera_CentresAMapSmallerThanTheView()
    {
        OverworldScene scene = Scene(Map(4, 4), new GridPos(2, 2));
        Assert.True(scene.Camera.X < 0);
        Assert.Equal(-(W - 4 * Tile) / 2, scene.Camera.X);
    }

    [Fact]
    public void Camera_FollowsThePlayerAsTheyWalk()
    {
        OverworldScene scene = Scene(Map(64, 64), new GridPos(32, 32), Facing.Right);
        int before = scene.Camera.X;
        Walk(scene, GameAction.Right, ticks: 16);
        Assert.True(scene.Camera.X > before);
    }

    // --- Rendering ----------------------------------------------------------------

    [Fact]
    public void Render_ProducesValidQuadsThroughTheRealBatch()
    {
        OverworldScene scene = Scene(Map(8, 8), new GridPos(3, 3));

        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();

        Assert.NotEmpty(_renderer.Drawn);
        Assert.All(_renderer.Drawn, q => Assert.False(q.Dest.IsEmpty));
    }

    /// <summary>Only the visible window is submitted, so a large map does not draw every tile.</summary>
    [Fact]
    public void Render_DrawsOnlyTilesNearTheCamera()
    {
        OverworldScene scene = Scene(Map(200, 200), new GridPos(100, 100));

        _batch.Begin();
        scene.Render();
        var (quads, _, _) = _batch.End();

        int maxTiles = (W / Tile + 2) * (H / Tile + 2) + 2;   // window plus edges, panel and player
        Assert.True(quads.Count < maxTiles, $"drew {quads.Count} quads for a 200x200 map");
    }

    // --- Construction -------------------------------------------------------------

    [Fact]
    public void NullArgumentsAndBadTileSize_AreRejected()
    {
        Map map = Map(4, 4);
        Assert.Throws<ArgumentNullException>(() => new OverworldScene(null!, map, [], default, Facing.Down, Tile, W, H));
        Assert.Throws<ArgumentNullException>(() => new OverworldScene(_ui, null!, [], default, Facing.Down, Tile, W, H));
        Assert.Throws<ArgumentNullException>(() => new OverworldScene(_ui, map, null!, default, Facing.Down, Tile, W, H));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OverworldScene(_ui, map, [], default, Facing.Down, 0, W, H));
    }

    [Fact]
    public void SceneIsOpaque_SoItHidesAnythingBeneath() =>
        Assert.False(Scene(Map(4, 4), default).IsOverlay);

    // --- Step outcomes and interaction --------------------------------------------

    private OverworldScene SceneWith(Map map, GridPos start, Facing facing = Facing.Down,
        FlagStore? flags = null)
    {
        var scene = new OverworldScene(_ui, map, [], start, facing, Tile, W, H, flags);
        scene.Enter();
        return scene;
    }

    [Fact]
    public void SteppingOntoAWarp_SurfacesItToTheHost()
    {
        Map map = Map(8, 8) with
        {
            Entities =
            [
                new WarpEntity
                {
                    Key = "w", Pos = new GridPos(3, 2),
                    Target = EntityId.Parse("map:other"), TargetPos = new GridPos(1, 1),
                },
            ],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3));
        Walk(scene, GameAction.Up);

        Assert.IsType<StepOutcome.Warp>(scene.Pending);
        Assert.IsType<StepOutcome.Warp>(scene.TakePending());
        Assert.Null(scene.Pending);   // taking it clears it
    }

    /// <summary>A trigger's flag actions run in the scene; nothing is surfaced to the host.</summary>
    [Fact]
    public void SteppingOnAFlagTrigger_SetsTheFlagWithoutInterruptingPlay()
    {
        Map map = Map(8, 8) with
        {
            Entities =
            [
                new TriggerEntity
                {
                    Key = "t", Pos = new GridPos(3, 2),
                    Actions = [new TriggerAction { Op = TriggerOp.SetFlag, Flag = "seen", Value = 3 }],
                },
            ],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3));
        Walk(scene, GameAction.Up);

        Assert.Equal(3, scene.Flags.GetInt("seen"));
        Assert.Null(scene.Pending);
    }

    [Fact]
    public void ClearFlagTrigger_ResetsTheFlag()
    {
        var flags = new FlagStore();
        flags.SetInt("seen", 5);
        Map map = Map(8, 8) with
        {
            Entities =
            [
                new TriggerEntity
                {
                    Key = "t", Pos = new GridPos(3, 2),
                    Actions = [new TriggerAction { Op = TriggerOp.ClearFlag, Flag = "seen" }],
                },
            ],
        };
        Walk(SceneWith(map, new GridPos(3, 3), flags: flags), GameAction.Up);
        Assert.Equal(0, flags.GetInt("seen"));
    }

    /// <summary>giveItem, heal, and startBattle need Core operations, so they reach the host.</summary>
    [Fact]
    public void TriggerActionsNeedingCoreOperations_ReachTheHost()
    {
        Map map = Map(8, 8) with
        {
            Entities =
            [
                new TriggerEntity
                {
                    Key = "t", Pos = new GridPos(3, 2),
                    Actions = [new TriggerAction { Op = TriggerOp.Heal }],
                },
            ],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3));
        Walk(scene, GameAction.Up);
        Assert.NotNull(scene.Pending);
    }

    [Fact]
    public void ConfirmFacingASign_OpensDialogueAndSuspendsMovement()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new SignEntity { Key = "s", Pos = new GridPos(3, 2), Text = "KEEP OUT" }],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3), Facing.Up);

        scene.Update(Hold(GameAction.Confirm));
        Assert.True(scene.InDialogue);

        GridPos frozen = scene.PlayerPos;
        Walk(scene, GameAction.Down);
        Assert.Equal(frozen, scene.PlayerPos);   // dialogue owns input
    }

    [Fact]
    public void ConfirmDismissesDialogueAndMovementResumes()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new SignEntity { Key = "s", Pos = new GridPos(3, 2), Text = "HI" }],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3), Facing.Up);

        scene.Update(Hold(GameAction.Confirm));
        Assert.True(scene.InDialogue);

        scene.Update(Hold(GameAction.Confirm));   // completes the page
        scene.Update(Hold(GameAction.Confirm));   // dismisses it
        Assert.False(scene.InDialogue);

        Walk(scene, GameAction.Down);
        Assert.Equal(new GridPos(3, 4), scene.PlayerPos);
    }

    [Fact]
    public void ConfirmFacingNothing_DoesNotOpenDialogue()
    {
        OverworldScene scene = SceneWith(Map(8, 8), new GridPos(3, 3), Facing.Up);
        scene.Update(Hold(GameAction.Confirm));
        Assert.False(scene.InDialogue);
    }

    [Fact]
    public void ConfirmFacingAnNpcWithDialogue_Speaks()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new NpcEntity { Key = "n", Pos = new GridPos(3, 2), Dialogue = "HELLO" }],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3), Facing.Up);
        scene.Update(Hold(GameAction.Confirm));
        Assert.True(scene.InDialogue);
    }

    /// <summary>An empty dialogue string must not open an empty box.</summary>
    [Fact]
    public void EmptySignText_OpensNoDialogue()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new SignEntity { Key = "s", Pos = new GridPos(3, 2), Text = "" }],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3), Facing.Up);
        scene.Update(Hold(GameAction.Confirm));
        Assert.False(scene.InDialogue);
    }

    [Fact]
    public void DialogueRendersWithoutInvalidQuads()
    {
        Map map = Map(8, 8) with
        {
            Entities = [new SignEntity { Key = "s", Pos = new GridPos(3, 2), Text = "A LONGER LINE OF TEXT" }],
        };
        OverworldScene scene = SceneWith(map, new GridPos(3, 3), Facing.Up);
        scene.Update(Hold(GameAction.Confirm));
        for (int i = 0; i < 10; i++)
            scene.Update(Idle);

        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);

        Assert.NotEmpty(_renderer.Drawn);
    }
}
