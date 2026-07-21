using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16G budget instrumentation. Percentile maths and resource growth are
/// deterministic and asserted; wall-clock durations are machine-dependent and reported rather than
/// asserted, because a timing threshold in this suite would be a flake, not a gate.</summary>
public sealed class FrameMetricsTests
{
    // --- Percentiles --------------------------------------------------------------

    [Fact]
    public void AnEmptySeriesIsAllZero()
    {
        DurationStats stats = DurationStats.From([]);
        Assert.Equal(0, stats.Samples);
        Assert.Equal(0, stats.P95);
    }

    [Fact]
    public void ASingleSampleIsEveryStatistic()
    {
        DurationStats stats = DurationStats.From([4.5]);
        Assert.Equal(1, stats.Samples);
        Assert.Equal(4.5, stats.Min);
        Assert.Equal(4.5, stats.Median);
        Assert.Equal(4.5, stats.P95);
        Assert.Equal(4.5, stats.Max);
    }

    /// <summary>Nearest-rank on 1..100 puts p50 at 50 and p95 at 95, with no interpolation.</summary>
    [Fact]
    public void PercentilesUseNearestRank()
    {
        DurationStats stats = DurationStats.From([.. Enumerable.Range(1, 100).Select(i => (double)i)]);
        Assert.Equal(1, stats.Min);
        Assert.Equal(50, stats.Median);
        Assert.Equal(95, stats.P95);
        Assert.Equal(100, stats.Max);
    }

    [Fact]
    public void UnsortedInputIsSortedFirst()
    {
        DurationStats stats = DurationStats.From([9, 1, 5, 3, 7]);
        Assert.Equal(1, stats.Min);
        Assert.Equal(5, stats.Median);
        Assert.Equal(9, stats.Max);
    }

    /// <summary>One slow frame must move the maximum without dragging the median with it, which is
    /// the point of reporting both.</summary>
    [Fact]
    public void AnOutlierRaisesTheMaximumButNotTheMedian()
    {
        var samples = Enumerable.Repeat(1.0, 99).Append(500.0).ToList();
        DurationStats stats = DurationStats.From(samples);

        Assert.Equal(1.0, stats.Median);
        Assert.Equal(500.0, stats.Max);
    }

    [Fact]
    public void NullSamples_AreRejected() =>
        Assert.Throws<ArgumentNullException>(() => DurationStats.From(null!));

    // --- Collection ---------------------------------------------------------------

    [Fact]
    public void TheMeasuredActionRunsExactlyOnce()
    {
        var metrics = new FrameMetrics();
        int updates = 0, renders = 0;

        metrics.Begin();
        metrics.Update(() => updates++);
        metrics.Render(() => renders++);
        metrics.End();

        Assert.Equal(1, updates);
        Assert.Equal(1, renders);
    }

    [Fact]
    public void FramesCountRendersNotUpdates()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        for (int i = 0; i < 5; i++)
            metrics.Update(() => { });
        for (int i = 0; i < 3; i++)
            metrics.Render(() => { });

        Assert.Equal(3, metrics.End().Frames);
    }

    [Fact]
    public void BothSeriesAreCollected()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        for (int i = 0; i < 10; i++)
        {
            metrics.Update(() => { });
            metrics.Render(() => { });
        }

        FrameReport report = metrics.End();
        Assert.Equal(10, report.Update.Samples);
        Assert.Equal(10, report.Render.Samples);
    }

    /// <summary>Begin discards a previous run, so a report never mixes two measurements.</summary>
    [Fact]
    public void BeginResetsAPreviousRun()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        for (int i = 0; i < 50; i++)
            metrics.Render(() => { });

        metrics.Begin();
        metrics.Render(() => { });

        Assert.Equal(1, metrics.End().Frames);
    }

    [Fact]
    public void AnUnmeasuredRunReportsZeroFrames()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        FrameReport report = metrics.End();

        Assert.Equal(0, report.Frames);
        Assert.Equal(0, report.AllocatedBytesPerFrame);
    }

    [Fact]
    public void NullActions_AreRejected()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        Assert.Throws<ArgumentNullException>(() => metrics.Update(null!));
        Assert.Throws<ArgumentNullException>(() => metrics.Render(null!));
    }

    [Fact]
    public void TheReportFormatsEveryBudgetFigure()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        metrics.Update(() => { });
        metrics.Render(() => { });

        string line = metrics.End().Format();
        Assert.Contains("frames=", line);
        Assert.Contains("p95", line);
        Assert.Contains("alloc/frame", line);
    }

    // --- Allocation ---------------------------------------------------------------

    /// <summary>Allocation is measured, so a frame that allocates shows up. This is the signal the
    /// steady-state budget depends on; if it read zero regardless, the budget would be unmeasurable.</summary>
    [Fact]
    public void AllocationIsActuallyObserved()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        for (int i = 0; i < 100; i++)
            metrics.Render(() => GC.KeepAlive(new byte[4096]));

        FrameReport report = metrics.End();
        Assert.True(report.AllocatedBytesPerFrame > 1000,
            $"expected measurable allocation, saw {report.AllocatedBytesPerFrame:F0}B/frame");
    }

    /// <summary>A do-nothing frame must not allocate meaningfully, or the instrumentation itself is
    /// the thing blowing the budget.</summary>
    [Fact]
    public void TheInstrumentationItselfIsCheap()
    {
        var metrics = new FrameMetrics();
        metrics.Begin();
        for (int i = 0; i < 1000; i++)
        {
            metrics.Update(() => { });
            metrics.Render(() => { });
        }

        FrameReport report = metrics.End();
        Assert.True(report.AllocatedBytesPerFrame < 1024,
            $"instrumentation allocated {report.AllocatedBytesPerFrame:F0}B/frame against a 1KB budget");
    }
}

/// <summary>The 16G resource-growth row: repeated scene and battle cycles must not leak leases or
/// managed memory. Lease counts are exact and asserted; managed bytes are checked for monotonic
/// growth after a collection rather than an absolute figure.</summary>
public sealed class ResourceGrowthTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 256;
    private const int H = 192;
    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public ResourceGrowthTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static GameDb Db() => new(
        new ProjectSettings
        {
            Name = "T", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(2, 2),
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 1, Capacity = 4 },
        },
        [
            new Map
            {
                Id = MapId, Name = "Field", Width = 8, Height = 8,
                Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
            },
            new Species
            {
                Id = Starter, Name = "Pebbling", Types = [TypeId], GrowthRate = "medium-fast",
                BaseStats = new Stats(60, 50, 50, 50, 50, 50),
                Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
            },
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move
            {
                Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                DamageClass = DamageClass.Physical, Accuracy = 100,
            },
        ]);

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    /// <summary>A hundred enter/exit cycles must leave the renderer holding exactly what it started
    /// with — the atlas, and nothing else.</summary>
    [Fact]
    public void ARepeatedSceneCycleLeavesNoLeaseGrowth()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(1));
        session.InitialiseNewGame();

        int baseline = _renderer.TextureCount;

        for (int cycle = 0; cycle < 100; cycle++)
        {
            using var scenes = new SceneStack();
            scenes.Push(session.Enter(MapId, new GridPos(2, 2), Facing.Down)!);
            scenes.Tick(Idle);

            _batch.Begin();
            scenes.Render();
            _batch.End();

            scenes.Shutdown();
        }

        Assert.Equal(baseline, _renderer.TextureCount);
    }

    /// <summary>UI resources own their atlas and must release it, or every scene push would leak one.</summary>
    [Fact]
    public void RepeatedUiResourceCyclesReleaseTheirAtlas()
    {
        int baseline = _renderer.TextureCount;

        for (int cycle = 0; cycle < 100; cycle++)
        {
            using var resources = new UiResources(_renderer, _batch);
            Assert.Equal(baseline + 1, _renderer.TextureCount);
        }

        Assert.Equal(baseline, _renderer.TextureCount);
    }

    /// <summary>Managed memory must not grow monotonically across cycles once collected.</summary>
    [Fact]
    public void ARepeatedCycleDoesNotGrowManagedMemoryMonotonically()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(1));
        session.InitialiseNewGame();

        long Cycle(int count)
        {
            for (int i = 0; i < count; i++)
            {
                using var scenes = new SceneStack();
                scenes.Push(session.Enter(MapId, new GridPos(2, 2), Facing.Down)!);
                for (int t = 0; t < 10; t++)
                    scenes.Tick(Idle);
                scenes.Shutdown();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(forceFullCollection: true);
        }

        Cycle(20);                     // warm up allocation pools
        long after20 = Cycle(20);
        long after40 = Cycle(20);

        // Allow generous headroom: this asserts the absence of a leak, not an exact figure.
        Assert.True(after40 <= after20 * 1.5,
            $"managed memory grew from {after20} to {after40} across identical cycles");
    }

    /// <summary>The batch reuses its buffer rather than growing without bound across frames.</summary>
    [Fact]
    public void TheQuadBatchCapacityStabilises()
    {
        var batch = new QuadBatch();
        TextureHandle atlas = _renderer.CreateTexture(4, 4, new byte[64]);

        for (int frame = 0; frame < 200; frame++)
        {
            batch.Begin();
            for (int i = 0; i < 50; i++)
                batch.Ui(atlas, new RectI(0, 0, 4, 4), new RectI(i, 0, 4, 4), layer: 0);
            batch.End();
        }

        Assert.Equal(QuadBatch.InitialCapacity, batch.Capacity);
    }
}
