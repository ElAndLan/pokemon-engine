using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class GridMoverTests
{
    // Every step succeeds, moving one cell in the facing direction.
    private static Func<GridPos, Facing, MoveOutcome> AlwaysStep =>
        (p, d) => new MoveOutcome(MoveResult.Step, MovementRules.Step(p, d));

    private static Func<GridPos, Facing, MoveOutcome> AlwaysBlocked => (_, _) => MoveOutcome.Blocked;

    private static GridMover Mover(Func<GridPos, Facing, MoveOutcome> resolve) =>
        new(new GridPos(2, 2), Facing.Down, resolve, turnTicks: 2, stepTicks: 3);

    private static void Tick(GridMover m, Facing? dir, int times)
    {
        for (int i = 0; i < times; i++) m.Tick(dir);
    }

    [Fact]
    public void PressNewDirection_TurnsWithoutMoving()
    {
        GridMover m = Mover(AlwaysStep);
        m.Tick(Facing.Right);
        Assert.Equal(Facing.Right, m.Facing);
        Assert.Equal(MoverState.Turning, m.State);
        Assert.Equal(new GridPos(2, 2), m.Position); // did not move
    }

    [Fact]
    public void TapToFace_ReleaseDuringTurn_DoesNotStep()
    {
        GridMover m = Mover(AlwaysStep);
        m.Tick(Facing.Right);  // start turning (timer set to turnTicks=2)
        m.Tick(null);          // turn tick 1 (released)
        m.Tick(null);          // turn tick 2 → completes; not held → tap-to-face only
        Assert.Equal(MoverState.Idle, m.State);
        Assert.Equal(Facing.Right, m.Facing);
        Assert.Equal(new GridPos(2, 2), m.Position);
    }

    [Fact]
    public void HoldNewDirection_TurnsThenSteps()
    {
        GridMover m = Mover(AlwaysStep);
        Tick(m, Facing.Right, 3); // press + 2 turn ticks → turn completes and stepping begins
        Assert.Equal(MoverState.Moving, m.State);
        Tick(m, Facing.Right, 3); // 3-tick step completes → arrives one cell right
        Assert.Equal(new GridPos(3, 2), m.Position);
    }

    [Fact]
    public void PressFacingDirection_StepsImmediately_NoTurnDelay()
    {
        GridMover m = Mover(AlwaysStep); // already facing Down
        m.Tick(Facing.Down);
        Assert.Equal(MoverState.Moving, m.State); // no turn phase
        Tick(m, null, 3);
        Assert.Equal(new GridPos(2, 3), m.Position);
    }

    [Fact]
    public void Held_WalksContinuously()
    {
        GridMover m = Mover(AlwaysStep); // already facing Down → no turn
        // step 1: begin + 3 ticks (arrive at tick 4); step 2: 3 more ticks (arrive at tick 7).
        Tick(m, Facing.Down, 7);
        Assert.Equal(new GridPos(2, 4), m.Position);
        Assert.Equal(MoverState.Moving, m.State); // still walking (held)
    }

    [Fact]
    public void BufferedTurn_AppliedAfterArrival()
    {
        GridMover m = Mover(AlwaysStep);
        m.Tick(Facing.Down);        // step 1 begins (facing Down), timer=3
        m.Tick(Facing.Right);       // request Right mid-step → buffered (timer 2)
        m.Tick(Facing.Right);       // timer 1
        m.Tick(Facing.Right);       // timer 0 → arrive at (2,3); buffered Right → turn
        Assert.Equal(new GridPos(2, 3), m.Position);
        Assert.Equal(Facing.Right, m.Facing);
        Assert.Equal(MoverState.Turning, m.State);
    }

    [Fact]
    public void Blocked_FacesButStaysPut()
    {
        GridMover m = Mover(AlwaysBlocked);
        m.Tick(Facing.Down);      // already facing Down → try step → blocked
        Assert.Equal(MoverState.Idle, m.State);
        Assert.Equal(new GridPos(2, 2), m.Position);
    }

    [Fact]
    public void LedgeHop_TakesLongerAndLandsAtOutcomeDestination()
    {
        Func<GridPos, Facing, MoveOutcome> ledge =
            (_, _) => new MoveOutcome(MoveResult.LedgeHop, new GridPos(2, 4));
        GridMover m = Mover(ledge); // stepTicks=3 → hop = 6 ticks
        m.Tick(Facing.Down);
        Tick(m, null, 5);
        Assert.Equal(MoverState.Moving, m.State); // still hopping at tick 5 of 6
        m.Tick(null);
        Assert.Equal(new GridPos(2, 4), m.Position);
    }

    [Fact]
    public void Progress_AdvancesFromZeroToNearOne()
    {
        GridMover m = Mover(AlwaysStep);
        m.Tick(Facing.Down); // begin step (duration 3)
        float p0 = m.Progress;
        m.Tick(null);
        float p1 = m.Progress;
        Assert.True(p1 > p0);
        Assert.InRange(m.Progress, 0f, 1f);
    }
}
