using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Walk-cycle playback: the right clip for the facing, advancing only while moving, resting
/// on the standing frame when idle, and deterministic because it advances in tick units.</summary>
public sealed class WalkAnimatorTests
{
    private static EntityId S(string s) => EntityId.Parse("sprite:" + s);

    /// <summary>A four-frame clip resting on frame 0: neutral, stepA, neutral, stepB at 100ms each.</summary>
    private static Animation Clip(string dir) => new()
    {
        Id = EntityId.Parse("anim:walk_" + dir), Loop = true,
        Frames =
        [
            new AnimFrame(S($"{dir}_n"), 100),
            new AnimFrame(S($"{dir}_a"), 100),
            new AnimFrame(S($"{dir}_n"), 100),
            new AnimFrame(S($"{dir}_b"), 100),
        ],
    };

    private static WalkAnimator Animator() =>
        new([Clip("down"), Clip("up"), Clip("left"), Clip("right")]);

    private const double Tick = 1000.0 / 60;   // ~16.67ms

    // --- Clip selection ---------------------------------------------------------------

    [Theory]
    [InlineData(Facing.Down, "down_n")]
    [InlineData(Facing.Up, "up_n")]
    [InlineData(Facing.Left, "left_n")]
    [InlineData(Facing.Right, "right_n")]
    public void StandingShowsTheFacingClipsRestFrame(Facing facing, string expected) =>
        Assert.Equal(S(expected), Animator().Advance(facing, moving: false, Tick));

    // --- Idle behaviour ---------------------------------------------------------------

    [Fact]
    public void StandingNeverLeavesTheRestFrameHoweverLongItStands()
    {
        WalkAnimator a = Animator();
        for (int i = 0; i < 100; i++)
            Assert.Equal(S("down_n"), a.Advance(Facing.Down, moving: false, Tick));
    }

    /// <summary>Stopping mid-cycle resets, so the next step starts from the standing pose rather than
    /// wherever the previous walk happened to end.</summary>
    [Fact]
    public void StoppingResetsSoTheNextStepStartsFromRest()
    {
        WalkAnimator a = Animator();
        for (int i = 0; i < 12; i++)                       // walk ~200ms into the cycle
            a.Advance(Facing.Down, moving: true, Tick);

        a.Advance(Facing.Down, moving: false, Tick);       // stop
        Assert.Equal(S("down_n"), a.Advance(Facing.Down, moving: true, Tick));  // first moving frame
    }

    // --- Advancing --------------------------------------------------------------------

    [Fact]
    public void MovingWalksThroughTheFramesInOrder()
    {
        WalkAnimator a = Animator();
        var seen = new List<EntityId?>();
        // 400ms total cycle; sample well past one loop at ~16.67ms/tick.
        for (int i = 0; i < 30; i++)
            seen.Add(a.Advance(Facing.Down, moving: true, Tick));

        // All four distinct poses appear, and the cycle returns to neutral.
        Assert.Contains(S("down_n"), seen);
        Assert.Contains(S("down_a"), seen);
        Assert.Contains(S("down_b"), seen);
    }

    [Fact]
    public void TheCycleLoops()
    {
        WalkAnimator a = Animator();
        EntityId? first = a.Advance(Facing.Down, moving: true, 0);      // t=0 -> frame 0
        for (int i = 0; i < 24; i++)                                    // advance exactly 400ms (one loop)
            a.Advance(Facing.Down, moving: true, 400.0 / 24);
        EntityId? afterLoop = a.Advance(Facing.Down, moving: true, 0);
        Assert.Equal(first, afterLoop);
    }

    /// <summary>Determinism: the same tick sequence yields the same frames, since nothing reads a
    /// wall clock.</summary>
    [Fact]
    public void SameTickSequenceIsReproducible()
    {
        WalkAnimator a = Animator(), b = Animator();
        for (int i = 0; i < 50; i++)
            Assert.Equal(a.Advance(Facing.Down, true, Tick), b.Advance(Facing.Down, true, Tick));
    }

    // --- Missing clips ----------------------------------------------------------------

    [Fact]
    public void AFacingWithNoClipYieldsNoSprite()
    {
        var a = new WalkAnimator([Clip("down"), null, null, null]);
        Assert.Null(a.Advance(Facing.Up, moving: false, Tick));
        Assert.NotNull(a.Advance(Facing.Down, moving: false, Tick));
    }

    [Fact]
    public void AnEmptyClipSetYieldsNoSprite() =>
        Assert.Null(new WalkAnimator([]).Advance(Facing.Down, moving: true, Tick));

    [Fact]
    public void NullClipListIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new WalkAnimator(null!));
}
