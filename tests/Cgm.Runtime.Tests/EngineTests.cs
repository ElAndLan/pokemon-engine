using Cgm.Runtime.Engine;

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
