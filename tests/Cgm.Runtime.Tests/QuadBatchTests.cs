using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16B command-golden and batch-table rows: submission order, camera
/// resolution, scissor intersection, flush boundaries, and capacity growth.</summary>
public sealed class QuadBatchTests
{
    private static readonly TextureHandle TexA = new(1);
    private static readonly TextureHandle TexB = new(2);
    private static readonly RectI Src = new(0, 0, 16, 16);

    private static QuadBatch Open(int cameraX = 0, int cameraY = 0)
    {
        var batch = new QuadBatch();
        batch.Begin(cameraX, cameraY);
        return batch;
    }

    private static RectI At(int x, int y) => new(x, y, 16, 16);

    // --- Coordinates -------------------------------------------------------------

    [Fact]
    public void World_SubtractsTheCamera()
    {
        QuadBatch batch = Open(cameraX: 40, cameraY: 24);
        batch.World(TexA, Src, At(100, 80), layer: 0);

        Quad quad = batch.End().Quads.Single();
        Assert.Equal(60, quad.Dest.X);
        Assert.Equal(56, quad.Dest.Y);
        Assert.Equal(16, quad.Dest.Width);
    }

    [Fact]
    public void Ui_IgnoresTheCamera()
    {
        QuadBatch batch = Open(cameraX: 40, cameraY: 24);
        batch.Ui(TexA, Src, At(10, 10), layer: 0);

        Quad quad = batch.End().Quads.Single();
        Assert.Equal(10, quad.Dest.X);
        Assert.Equal(10, quad.Dest.Y);
    }

    [Fact]
    public void NegativeCamera_MovesWorldQuadsRight()
    {
        QuadBatch batch = Open(cameraX: -8, cameraY: -4);
        batch.World(TexA, Src, At(0, 0), layer: 0);

        Quad quad = batch.End().Quads.Single();
        Assert.Equal(8, quad.Dest.X);
        Assert.Equal(4, quad.Dest.Y);
    }

    // --- Ordering ----------------------------------------------------------------

    [Fact]
    public void Quads_SortByLayerThenSubmissionSequence()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), layer: 5);
        batch.Ui(TexA, Src, At(1, 0), layer: 0);
        batch.Ui(TexA, Src, At(2, 0), layer: 5);
        batch.Ui(TexA, Src, At(3, 0), layer: 0);

        Assert.Equal([1, 3, 0, 2], batch.End().Quads.Select(q => q.Dest.X).ToArray());
    }

    /// <summary>Equal layers keep exact call order; the renderer never guesses Y sorting.</summary>
    [Fact]
    public void EqualLayer_KeepsExactCallOrderEvenWhenYDescends()
    {
        QuadBatch batch = Open();
        foreach (int y in new[] { 90, 10, 50 })
            batch.Ui(TexA, Src, new RectI(0, y, 16, 16), layer: 1);

        Assert.Equal([90, 10, 50], batch.End().Quads.Select(q => q.Dest.Y).ToArray());
    }

    [Fact]
    public void NegativeLayers_SortBeforeZero()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.Ui(TexA, Src, At(1, 0), layer: -3);

        Assert.Equal([1, 0], batch.End().Quads.Select(q => q.Dest.X).ToArray());
    }

    // --- Flush boundaries --------------------------------------------------------

    [Fact]
    public void SameTextureLayerAndScissor_BatchIntoOneDrawCall()
    {
        QuadBatch batch = Open();
        for (int i = 0; i < 50; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);

        DrawCall call = batch.End().Calls.Single();
        Assert.Equal(50, call.Count);
        Assert.Equal(0, call.Start);
        Assert.Equal(FlushReason.FirstQuad, call.Reason);
    }

    [Fact]
    public void TextureChange_FlushesWithTextureReason()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.Ui(TexB, Src, At(1, 0), layer: 0);

        var (_, calls, stats) = batch.End();
        Assert.Equal(2, calls.Count);
        Assert.Equal(FlushReason.Texture, calls[1].Reason);
        Assert.Equal(1, stats.Flushes[FlushReason.Texture]);
    }

    [Fact]
    public void LayerChange_FlushesWithLayerReason()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.Ui(TexA, Src, At(1, 0), layer: 1);

        var (_, calls, stats) = batch.End();
        Assert.Equal(2, calls.Count);
        Assert.Equal(FlushReason.Layer, calls[1].Reason);
        Assert.Equal(1, stats.Flushes[FlushReason.Layer]);
    }

    [Fact]
    public void ScissorChange_FlushesWithScissorReason()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.PushScissor(new RectI(0, 0, 32, 32));
        batch.Ui(TexA, Src, At(1, 0), layer: 0);

        var (_, calls, stats) = batch.End();
        Assert.Equal(2, calls.Count);
        Assert.Equal(FlushReason.Scissor, calls[1].Reason);
        Assert.Equal(1, stats.Flushes[FlushReason.Scissor]);
    }

    /// <summary>A run longer than the batch capacity splits, and capacity grows to a power of two.</summary>
    [Fact]
    public void CapacityBoundary_FlushesAndGrowsToPowerOfTwo()
    {
        QuadBatch batch = Open();
        Assert.Equal(2048, batch.Capacity);
        for (int i = 0; i < QuadBatch.InitialCapacity + 1; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);

        var (quads, calls, stats) = batch.End();
        Assert.Equal(2049, quads.Count);
        Assert.Equal(2, calls.Count);
        Assert.Equal(2048, calls[0].Count);
        Assert.Equal(FlushReason.Capacity, calls[1].Reason);
        Assert.Equal(4096, stats.Capacity);
    }

    [Fact]
    public void CapacityGrowth_JumpsStraightToTheSmallestFittingPowerOfTwo()
    {
        QuadBatch batch = Open();
        for (int i = 0; i < 5000; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);

        Assert.Equal(8192, batch.End().Stats.Capacity);
    }

    /// <summary>After growth the larger buffer is real: a run that previously split now fits in one
    /// call. Batching against the constant instead of the live capacity would keep splitting at 2,048.</summary>
    [Fact]
    public void AfterGrowth_ARunLargerThanTheInitialCapacityIsOneDrawCall()
    {
        var batch = new QuadBatch();
        batch.Begin();
        for (int i = 0; i < 3000; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);
        Assert.Equal(2, batch.End().Calls.Count);   // split against the 2,048 buffer
        Assert.Equal(4096, batch.Capacity);

        batch.Begin();
        for (int i = 0; i < 3000; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);
        DrawCall call = Assert.Single(batch.End().Calls);
        Assert.Equal(3000, call.Count);
    }

    [Fact]
    public void Capacity_NeverShrinksOnALaterSmallerFrame()
    {
        var batch = new QuadBatch();
        batch.Begin();
        for (int i = 0; i < 3000; i++)
            batch.Ui(TexA, Src, At(i, 0), layer: 0);
        batch.End();

        batch.Begin();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        Assert.Equal(4096, batch.End().Stats.Capacity);
    }

    // --- Scissor -----------------------------------------------------------------

    [Fact]
    public void NestedScissor_IntersectsAndCanOnlyNarrow()
    {
        QuadBatch batch = Open();
        batch.PushScissor(new RectI(0, 0, 100, 100));
        batch.PushScissor(new RectI(50, 50, 100, 100));
        batch.Ui(TexA, Src, At(60, 60), layer: 0);

        Assert.Equal(new RectI(50, 50, 50, 50), batch.End().Quads.Single().Scissor);
    }

    [Fact]
    public void PopScissor_RestoresTheOuterRectangle()
    {
        QuadBatch batch = Open();
        batch.PushScissor(new RectI(0, 0, 100, 100));
        batch.PushScissor(new RectI(50, 50, 10, 10));
        batch.PopScissor();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);

        Assert.Equal(new RectI(0, 0, 100, 100), batch.End().Quads.Single().Scissor);
    }

    [Fact]
    public void PoppingEveryScissor_LeavesQuadsUnclipped()
    {
        QuadBatch batch = Open();
        batch.PushScissor(new RectI(0, 0, 10, 10));
        batch.PopScissor();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);

        Assert.Null(batch.End().Quads.Single().Scissor);
    }

    /// <summary>Disjoint nesting clips everything away, so the quad is dropped rather than drawn
    /// with a negative-size scissor the backend would reject.</summary>
    [Fact]
    public void DisjointScissors_CullSubmissionsEntirely()
    {
        QuadBatch batch = Open();
        batch.PushScissor(new RectI(0, 0, 10, 10));
        batch.PushScissor(new RectI(90, 90, 10, 10));
        batch.Ui(TexA, Src, At(0, 0), layer: 0);

        var (quads, calls, stats) = batch.End();
        Assert.Empty(quads);
        Assert.Empty(calls);
        Assert.Equal(0, stats.Quads);
    }

    [Fact]
    public void EdgeTouchingScissors_ProduceAnEmptyIntersection()
    {
        QuadBatch batch = Open();
        batch.PushScissor(new RectI(0, 0, 10, 10));
        batch.PushScissor(new RectI(10, 0, 10, 10));
        batch.Ui(TexA, Src, At(0, 0), layer: 0);

        Assert.Empty(batch.End().Quads);
    }

    [Fact]
    public void PopWithoutPush_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => Open().PopScissor());

    /// <summary>An unbalanced push must not leak into the next frame.</summary>
    [Fact]
    public void Begin_ResetsAnUnbalancedScissorStack()
    {
        var batch = new QuadBatch();
        batch.Begin();
        batch.PushScissor(new RectI(0, 0, 4, 4));
        batch.End();

        batch.Begin();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        Assert.Null(batch.End().Quads.Single().Scissor);
    }

    // --- Frame lifecycle and validation ------------------------------------------

    [Fact]
    public void Begin_ClearsThePreviousFrame()
    {
        var batch = new QuadBatch();
        batch.Begin();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.End();

        batch.Begin();
        Assert.Equal(0, batch.Count);
        Assert.Empty(batch.End().Quads);
    }

    [Fact]
    public void SequenceRestartsEachFrame_SoOrderingIsFrameLocal()
    {
        var batch = new QuadBatch();
        batch.Begin();
        batch.Ui(TexA, Src, At(0, 0), layer: 0);
        batch.End();

        batch.Begin();
        batch.Ui(TexA, Src, At(9, 0), layer: 0);
        Assert.Equal(0, batch.End().Quads.Single().Sequence);
    }

    [Fact]
    public void EmptyFrame_ProducesNoCallsAndDoesNotThrow()
    {
        var (quads, calls, stats) = Open().End();
        Assert.Empty(quads);
        Assert.Empty(calls);
        Assert.Equal(0, stats.DrawCalls);
    }

    [Fact]
    public void SubmittingBeforeBeginOrAfterEnd_IsRejected()
    {
        var batch = new QuadBatch();
        Assert.Throws<InvalidOperationException>(() => batch.Ui(TexA, Src, At(0, 0), 0));

        batch.Begin();
        batch.End();
        Assert.Throws<InvalidOperationException>(() => batch.Ui(TexA, Src, At(0, 0), 0));
        Assert.Throws<InvalidOperationException>(() => batch.PushScissor(new RectI(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => batch.End());
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    [InlineData(-1, 16)]
    public void EmptySourceOrDestination_IsRejected(int width, int height)
    {
        QuadBatch batch = Open();
        Assert.Throws<ArgumentException>(() => batch.Ui(TexA, new RectI(0, 0, width, height), At(0, 0), 0));
        Assert.Throws<ArgumentException>(() => batch.Ui(TexA, Src, new RectI(0, 0, width, height), 0));
    }

    [Fact]
    public void BothFlips_AreAccepted()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), 0, Flip.Horizontal | Flip.Vertical);
        Assert.Equal(Flip.Horizontal | Flip.Vertical, batch.End().Quads.Single().Flip);
    }

    [Fact]
    public void UnknownFlipFlags_AreRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Open().Ui(TexA, Src, At(0, 0), 0, (Flip)99));

    [Fact]
    public void Tint_DefaultsToOpaqueWhite()
    {
        QuadBatch batch = Open();
        batch.Ui(TexA, Src, At(0, 0), 0);
        Assert.Equal(Rgba.White, batch.End().Quads.Single().Tint);
    }

    // --- Rectangle helpers -------------------------------------------------------

    [Fact]
    public void Intersect_IsCommutativeAndClampsToOverlap()
    {
        var a = new RectI(0, 0, 10, 10);
        var b = new RectI(5, 5, 10, 10);
        Assert.Equal(new RectI(5, 5, 5, 5), a.Intersect(b));
        Assert.Equal(a.Intersect(b), b.Intersect(a));
    }

    [Fact]
    public void Contains_UsesHalfOpenBounds()
    {
        var outer = new RectI(0, 0, 16, 16);
        Assert.True(outer.Contains(new RectI(0, 0, 16, 16)));
        Assert.False(outer.Contains(new RectI(0, 0, 17, 16)));
        Assert.False(outer.Contains(new RectI(-1, 0, 4, 4)));
    }
}
