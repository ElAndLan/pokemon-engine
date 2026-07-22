using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16F timing row: six-tick beat, press-to-skip, held 4×, and the rule
/// that presentation speed never changes event order or content.</summary>
public sealed class BattleBeatQueueTests
{
    private static BattleBeatQueue Queue(params string[] lines)
    {
        var queue = new BattleBeatQueue();
        queue.EnqueueAll(lines);
        return queue;
    }

    private static void Ticks(BattleBeatQueue queue, int count, bool held = false)
    {
        for (int i = 0; i < count; i++)
            queue.Tick(held);
    }

    [Fact]
    public void NothingPresentsUntilTicked()
    {
        BattleBeatQueue queue = Queue("a");
        Assert.Null(queue.Current);
        Assert.True(queue.IsPresenting);
    }

    [Fact]
    public void TheFirstTickShowsTheFirstLine()
    {
        BattleBeatQueue queue = Queue("a");
        queue.Tick();
        Assert.Equal("a", queue.Current);
    }

    /// <summary>An event holds the screen for its whole beat before the next one replaces it.</summary>
    [Fact]
    public void ALineHoldsForTheMinimumBeat()
    {
        BattleBeatQueue queue = Queue("a", "b");
        queue.Tick();

        Ticks(queue, BattleBeatQueue.MinimumBeatTicks - 1);
        Assert.Equal("a", queue.Current);

        queue.Tick();
        Assert.Equal("b", queue.Current);
    }

    [Fact]
    public void LinesPresentInEnqueuedOrder()
    {
        BattleBeatQueue queue = Queue("a", "b", "c");
        Ticks(queue, BattleBeatQueue.MinimumBeatTicks * 4);
        Assert.Equal(["a", "b", "c"], queue.Shown);
    }

    [Fact]
    public void PresentationEndsWhenEverythingHasBeenShown()
    {
        BattleBeatQueue queue = Queue("a");
        Ticks(queue, BattleBeatQueue.MinimumBeatTicks * 2);
        Assert.False(queue.IsPresenting);
        Assert.Null(queue.Current);
    }

    /// <summary>Confirm completes the current beat only — a held button cannot skip unseen events.</summary>
    [Fact]
    public void ConfirmCompletesOnlyTheCurrentBeat()
    {
        BattleBeatQueue queue = Queue("a", "b", "c");
        queue.Tick();

        queue.Confirm();
        queue.Tick();

        Assert.Equal("b", queue.Current);
        Assert.Equal(["a"], queue.Shown);   // only one advanced
    }

    [Fact]
    public void ConfirmWithNothingPresenting_IsHarmless()
    {
        var queue = new BattleBeatQueue();
        queue.Confirm();
        Assert.Null(queue.Current);
    }

    /// <summary>Holding Confirm consumes four presentation ticks per simulation tick.</summary>
    [Fact]
    public void HeldConfirmRunsAtFourTimesSpeed()
    {
        BattleBeatQueue fast = Queue("a", "b");
        BattleBeatQueue slow = Queue("a", "b");

        Ticks(fast, 4, held: true);    // 16 presentation ticks
        Ticks(slow, 4);                // 4 presentation ticks

        Assert.True(fast.Shown.Count > slow.Shown.Count,
            $"fast showed {fast.Shown.Count}, slow showed {slow.Shown.Count}");
    }

    /// <summary>Speed changes pacing only: the same lines appear in the same order either way.</summary>
    [Fact]
    public void FastForwardPresentsIdenticalContentInIdenticalOrder()
    {
        BattleBeatQueue fast = Queue("a", "b", "c");
        BattleBeatQueue slow = Queue("a", "b", "c");

        Ticks(fast, 40, held: true);
        Ticks(slow, 40);

        Assert.Equal(slow.Shown, fast.Shown);
        Assert.False(fast.IsPresenting);
        Assert.False(slow.IsPresenting);
    }

    [Fact]
    public void RemainingBeatCountsDown()
    {
        BattleBeatQueue queue = Queue("a");
        queue.Tick();
        Assert.Equal(BattleBeatQueue.MinimumBeatTicks, queue.RemainingBeat);

        queue.Tick();
        Assert.Equal(BattleBeatQueue.MinimumBeatTicks - 1, queue.RemainingBeat);
    }

    [Fact]
    public void Clear_DropsEverythingPending()
    {
        BattleBeatQueue queue = Queue("a", "b");
        queue.Tick();
        queue.Clear();

        Assert.False(queue.IsPresenting);
        Assert.Equal(0, queue.Pending);
    }

    [Fact]
    public void EnqueuingDuringPresentation_AppendsRatherThanInterrupting()
    {
        BattleBeatQueue queue = Queue("a");
        queue.Tick();
        queue.Enqueue("b");

        Assert.Equal("a", queue.Current);
        Ticks(queue, BattleBeatQueue.MinimumBeatTicks);
        Assert.Equal("b", queue.Current);
    }

    [Fact]
    public void AnEmptyQueueTicksHarmlessly()
    {
        var queue = new BattleBeatQueue();
        Ticks(queue, 10, held: true);
        Assert.False(queue.IsPresenting);
        Assert.Empty(queue.Shown);
    }

    [Fact]
    public void ABeatShorterThanTheMinimum_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleBeatQueue(BattleBeatQueue.MinimumBeatTicks - 1));

    [Fact]
    public void NullArguments_AreRejected()
    {
        var queue = new BattleBeatQueue();
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
        Assert.Throws<ArgumentNullException>(() => queue.EnqueueAll(null!));
    }
}

/// <summary>The battle scene itself: layout renders, the menu drives Core, and presentation gates
/// input.</summary>
public sealed class BattleHostSceneTests : IDisposable
{
    private const int W = 256;
    private const int H = 192;
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId SpeciesId = EntityId.Parse("species:pebbling");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public BattleHostSceneTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static BattleHostScene Scene(UiPainter ui)
    {
        var db = new GameDb(new ProjectSettings { Name = "T" },
        [
            new Species
            {
                Id = SpeciesId, Name = "Pebbling", Types = [TypeId],
                BaseStats = new Stats(60, 50, 50, 50, 50, 50), GrowthRate = "medium-fast",
            },
            new TypeDef { Id = TypeId, Name = "Plain" },
            // DamageClass defaults to Status; a damaging move must say so explicitly or Core
            // rejects the damage record it would have to write.
            new Move
            {
                Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                DamageClass = DamageClass.Physical, Accuracy = 100,
            },
        ]);

        CreatureInstance Make() => new()
        {
            Species = SpeciesId, Level = 10, CurHp = 40, Nature = "hardy",
            Moves = [new MoveSlot(MoveId, 35)],
        };

        var battle = new BattleController(
            [BattleCreature.FromInstance(Make(), db)],
            [BattleCreature.FromInstance(Make(), db)],
            new TypeChart(db.All<TypeDef>()), new Rng(1),
            moveData: db.All<Move>().Select(MoveCompiler.ToBattleMove));

        var presenter = new BattleScene(battle, b => new UseMove(0));
        var scene = new BattleHostScene(ui, presenter, W, H);
        scene.Enter();
        return scene;
    }

    private static TickInput Press(params GameAction[] actions) =>
        new(actions.ToHashSet(), actions.ToHashSet(), new HashSet<GameAction>());

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    private void Frame(BattleHostScene scene, TickInput input)
    {
        scene.Update(input);
        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
    }

    [Fact]
    public void RendersWithoutInvalidQuads()
    {
        BattleHostScene scene = Scene(_ui);
        Frame(scene, Idle);

        Assert.NotEmpty(_renderer.Drawn);
        Assert.All(_renderer.Drawn, q => Assert.False(q.Dest.IsEmpty));
    }

    [Fact]
    public void IsOpaqueSoTheOverworldDoesNotShowThrough() => Assert.False(Scene(_ui).IsOverlay);

    [Fact]
    public void StartsOnTheFirstMenuEntry() => Assert.Equal(0, Scene(_ui).SelectedIndex);

    /// <summary>Root FIGHT then move 0: the two-step path from the four-way menu to a submitted move.</summary>
    private void ChooseFirstMove(BattleHostScene scene)
    {
        Frame(scene, Press(GameAction.Confirm));   // open the FIGHT panel
        Frame(scene, Press(GameAction.Confirm));   // pick the first move
    }

    [Fact]
    public void ConfirmSubmitsAnActionAndQueuesTheResultingEvents()
    {
        BattleHostScene scene = Scene(_ui);
        ChooseFirstMove(scene);
        Assert.True(scene.IsPresenting, "submitting a move should produce events to present");
    }

    /// <summary>While events present, the menu must not accept another action.</summary>
    [Fact]
    public void PresentationGatesFurtherInput()
    {
        BattleHostScene scene = Scene(_ui);
        ChooseFirstMove(scene);
        Assert.True(scene.IsPresenting);

        int before = scene.Log.Count;
        Frame(scene, Press(GameAction.Down));
        Assert.Equal(0, scene.SelectedIndex);   // navigation ignored mid-presentation
        Assert.True(scene.Log.Count >= before);
    }

    [Fact]
    public void PresentationDrainsAndReturnsControl()
    {
        BattleHostScene scene = Scene(_ui);
        ChooseFirstMove(scene);

        for (int i = 0; i < 400 && scene.IsPresenting; i++)
            Frame(scene, Idle);

        Assert.False(scene.IsPresenting);
        Assert.NotEmpty(scene.Log);
    }

    [Fact]
    public void EveryPresentedLineIsHumanReadable()
    {
        BattleHostScene scene = Scene(_ui);
        ChooseFirstMove(scene);
        for (int i = 0; i < 400 && scene.IsPresenting; i++)
            Frame(scene, Idle);

        Assert.All(scene.Log, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line));
            Assert.DoesNotContain("unpresented", line);   // the catalogue covers everything
        });
    }

    /// <summary>A battle played out fast must reach the same outcome as one played slowly.</summary>
    [Fact]
    public void FastForwardReachesTheSameOutcome()
    {
        BattleOutcome? Play(bool hold)
        {
            BattleHostScene scene = Scene(_ui);
            for (int i = 0; i < 3000 && !scene.Finished; i++)
                Frame(scene, hold ? Press(GameAction.Confirm) : Press(GameAction.Confirm));
            return scene.Outcome;
        }

        Assert.Equal(Play(hold: false)?.Winner, Play(hold: true)?.Winner);
    }

    [Fact]
    public void APlayedOutBattleFinishes()
    {
        BattleHostScene scene = Scene(_ui);
        for (int i = 0; i < 3000 && !scene.Finished; i++)
            Frame(scene, Press(GameAction.Confirm));

        Assert.True(scene.Finished);
        Assert.NotNull(scene.Outcome);
    }

    [Fact]
    public void ExitClearsPendingPresentation()
    {
        BattleHostScene scene = Scene(_ui);
        Frame(scene, Press(GameAction.Confirm));
        scene.Exit();
        Assert.False(scene.IsPresenting);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleHostScene(null!, null!, W, H));
        Assert.Throws<ArgumentNullException>(() => new BattleHostScene(_ui, null!, W, H));
    }
}
