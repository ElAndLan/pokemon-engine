using Cgm.Core.Timing;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16B host-loop contract: tick counts, edge delivery, and stall reset.</summary>
public sealed class HostLoopTests
{
    private const double Tick = 1000.0 / 60.0;

    private static List<TickInput> Frame(HostLoop loop, double elapsedMs, params GameAction[] held)
    {
        var seen = new List<TickInput>();
        loop.Frame(elapsedMs, held, seen.Add);
        return seen;
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(8.0, 0)]
    [InlineData(Tick, 1)]
    [InlineData(Tick * 3, 3)]
    [InlineData(Tick * 5, 5)]
    public void Frame_RunsTheDueTickCount(double elapsedMs, int expected) =>
        Assert.Equal(expected, Frame(new HostLoop(), elapsedMs).Count);

    /// <summary>A long stall never replays more than the catch-up cap, and the surplus is reported
    /// rather than silently swallowed.</summary>
    [Fact]
    public void Frame_ClampsBacklogToFiveTicksAndReportsTheDrop()
    {
        var loop = new HostLoop();
        Assert.Equal(5, Frame(loop, Tick * 20).Count);
        Assert.True(loop.DroppedTicks >= 14, $"dropped {loop.DroppedTicks}");
        Assert.InRange(loop.InterpolationAlpha, 0.0, 1.0);
    }

    [Fact]
    public void ZeroTickFrame_StillCountsAsARenderedFrame()
    {
        var loop = new HostLoop();
        Assert.Empty(Frame(loop, 1.0));
        Assert.Equal(0, loop.TotalTicks);
        Assert.Equal(1, loop.TotalFrames);
    }

    [Fact]
    public void Edges_BufferAcrossZeroTickFramesAndArriveOnceOnTheFirstDueTick()
    {
        var loop = new HostLoop();
        Assert.Empty(Frame(loop, 1.0, GameAction.Confirm));   // pressed, no tick due
        Assert.Empty(Frame(loop, 1.0, GameAction.Confirm));   // still held, still no tick

        List<TickInput> ticks = Frame(loop, Tick * 3, GameAction.Confirm);
        Assert.Equal(3, ticks.Count);
        Assert.True(ticks[0].WasPressed(GameAction.Confirm));
        Assert.False(ticks[1].WasPressed(GameAction.Confirm));
        Assert.False(ticks[2].WasPressed(GameAction.Confirm));
        Assert.All(ticks, t => Assert.True(t.IsDown(GameAction.Confirm)));
    }

    /// <summary>A press and release inside one zero-tick gap must both survive to the next tick;
    /// a tap that happens between renders is still a tap.</summary>
    [Fact]
    public void PressAndReleaseWithinOneGap_BothReachTheNextTick()
    {
        var loop = new HostLoop();
        Frame(loop, 1.0, GameAction.Confirm);
        Frame(loop, 1.0);

        TickInput tick = Assert.Single(Frame(loop, Tick));
        Assert.True(tick.WasPressed(GameAction.Confirm));
        Assert.True(tick.WasReleased(GameAction.Confirm));
        Assert.False(tick.IsDown(GameAction.Confirm));
    }

    [Fact]
    public void HeldState_ReflectsTheLatestPollNotTheEdgeFrame()
    {
        var loop = new HostLoop();
        Frame(loop, 1.0, GameAction.Up);
        TickInput tick = Assert.Single(Frame(loop, Tick, GameAction.Down));
        Assert.True(tick.IsDown(GameAction.Down));
        Assert.False(tick.IsDown(GameAction.Up));
        Assert.True(tick.WasPressed(GameAction.Down));
        Assert.True(tick.WasReleased(GameAction.Up));
    }

    [Fact]
    public void Release_IsReportedOnceThenForgotten()
    {
        var loop = new HostLoop();
        Frame(loop, Tick, GameAction.Menu);
        Assert.True(Frame(loop, Tick)[0].WasReleased(GameAction.Menu));
        Assert.False(Frame(loop, Tick)[0].WasReleased(GameAction.Menu));
    }

    [Fact]
    public void NoInput_ProducesEmptyEdgeSets()
    {
        TickInput tick = Assert.Single(Frame(new HostLoop(), Tick));
        Assert.Empty(tick.Held);
        Assert.Empty(tick.Pressed);
        Assert.Empty(tick.Released);
    }

    [Fact]
    public void SimultaneousActions_AllArriveOnTheSameTick()
    {
        TickInput tick = Assert.Single(Frame(new HostLoop(), Tick,
            GameAction.Up, GameAction.Confirm, GameAction.Run));
        Assert.Equal(3, tick.Pressed.Count);
        Assert.True(tick.WasPressed(GameAction.Up));
        Assert.True(tick.WasPressed(GameAction.Confirm));
        Assert.True(tick.WasPressed(GameAction.Run));
    }

    /// <summary>Duplicate held entries from merged devices must not double-report an edge.</summary>
    [Fact]
    public void DuplicateHeldActions_AreMergedNotDoubled()
    {
        var loop = new HostLoop();
        var seen = new List<TickInput>();
        loop.Frame(Tick, [GameAction.Confirm, GameAction.Confirm], seen.Add);
        Assert.Single(seen[0].Pressed);
        Assert.Single(seen[0].Held);
    }

    [Fact]
    public void ResetAfterStall_DropsTimeButKeepsBufferedEdges()
    {
        var loop = new HostLoop();
        Frame(loop, 8.0, GameAction.Confirm);   // sub-tick: time accumulates, press stays pending
        loop.ResetAfterStall();

        Assert.Equal(0.0, loop.InterpolationAlpha);
        List<TickInput> ticks = Frame(loop, Tick, GameAction.Confirm);
        Assert.Single(ticks);
        Assert.True(ticks[0].WasPressed(GameAction.Confirm));
    }

    [Fact]
    public void ResetAfterStall_PreventsCatchUpBurst()
    {
        var loop = new HostLoop();
        loop.ResetAfterStall();
        Assert.Empty(Frame(loop, 1.0));
    }

    [Fact]
    public void InterpolationAlpha_StaysInRangeAcrossUnevenFrames()
    {
        var loop = new HostLoop();
        foreach (double ms in new[] { 0.0, 3.7, Tick, Tick * 0.5, Tick * 2.3, 100.0, 0.1 })
        {
            Frame(loop, ms);
            Assert.InRange(loop.InterpolationAlpha, 0.0, 1.0);
        }
    }

    [Fact]
    public void TickCallback_ReceivesTicksInOrderAndCountsAccumulate()
    {
        var loop = new HostLoop();
        Frame(loop, Tick * 2);
        Frame(loop, Tick);
        Assert.Equal(3, loop.TotalTicks);
        Assert.Equal(2, loop.TotalFrames);
    }

    [Fact]
    public void CustomClock_IsHonoured()
    {
        var loop = new HostLoop(new FixedStepClock(tickRate: 30, maxTicksPerAdvance: 2));
        Assert.Equal(2, Frame(loop, 1000.0).Count);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonFiniteOrNegativeElapsed_IsRejected(double elapsedMs) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Frame(new HostLoop(), elapsedMs));

    [Fact]
    public void NullArguments_AreRejected()
    {
        var loop = new HostLoop();
        Assert.Throws<ArgumentNullException>(() => loop.Frame(Tick, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => loop.Frame(Tick, [], null!));
    }
}
