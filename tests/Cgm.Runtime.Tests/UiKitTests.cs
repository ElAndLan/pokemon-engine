using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16C UI kit contract: text measurement and wrapping, typewriter
/// cadence, list/grid navigation, and resource-bar presentation.</summary>
public sealed class TextLayoutTests
{
    private static readonly BitmapFont Font = new(glyphWidth: 5, glyphHeight: 7, spacing: 1, lineSpacing: 2);

    [Fact]
    public void Measure_CountsAdvancesWithoutTrailingSpacing()
    {
        Assert.Equal(0, Font.Measure(""));
        Assert.Equal(5, Font.Measure("A"));          // one glyph, no trailing gap
        Assert.Equal(11, Font.Measure("AB"));        // 5 + 1 + 5
        Assert.Equal(17, Font.Measure("ABC"));
    }

    [Fact]
    public void MeasureBlock_UsesWidestLineAndStacksHeights()
    {
        var (width, height) = Font.MeasureBlock(["A", "ABC"]);
        Assert.Equal(17, width);
        Assert.Equal(7 * 2 + 2, height);             // two lines, one gap between
        Assert.Equal((0, 0), Font.MeasureBlock([]));
    }

    [Fact]
    public void Measure_IsIndependentOfRenderScale()
    {
        // Nothing in measurement consults a framebuffer; the same string measures the same always.
        Assert.Equal(Font.Measure("HELLO"), new BitmapFont(5, 7, 1, 2).Measure("HELLO"));
    }

    [Fact]
    public void Wrap_BreaksOnTheLastWhitespace()
    {
        IReadOnlyList<string> lines = Font.Wrap("ABC DEF GHI", Font.Measure("ABC DEF"));
        Assert.Equal(["ABC DEF", "GHI"], lines);
    }

    [Fact]
    public void Wrap_TreatsNewlineAsAnExplicitBreak()
    {
        Assert.Equal(["AB", "CD"], Font.Wrap("AB\nCD", 200));
        Assert.Equal(["AB", "CD"], Font.Wrap("AB\r\nCD", 200));
    }

    [Fact]
    public void Wrap_KeepsBlankLinesAsDeliberateSpacing() =>
        Assert.Equal(["AB", "", "CD"], Font.Wrap("AB\n\nCD", 200));

    /// <summary>A token wider than the line must hard-break rather than overflow.</summary>
    [Fact]
    public void Wrap_HardBreaksAnOverlongToken()
    {
        IReadOnlyList<string> lines = Font.Wrap("ABCDEFGHIJ", Font.Measure("ABCD"));
        Assert.Equal(["ABCD", "EFGH", "IJ"], lines);
    }

    [Fact]
    public void Wrap_HardBreaksAnOverlongTokenAfterAShortWord()
    {
        IReadOnlyList<string> lines = Font.Wrap("AB CDEFGHIJKL", Font.Measure("ABCD"));
        Assert.All(lines, line => Assert.True(Font.Measure(line) <= Font.Measure("ABCD"), line));
        Assert.Equal("ABCDEFGHIJKL", string.Concat(lines).Replace(" ", ""));
    }

    [Fact]
    public void Wrap_NeverExceedsTheRequestedWidth()
    {
        const string text = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG";
        foreach (int width in new[] { 5, 11, 23, 47, 100 })
            Assert.All(Font.Wrap(text, width), line => Assert.True(Font.Measure(line) <= width,
                $"'{line}' exceeds {width}"));
    }

    [Fact]
    public void Wrap_PreservesEveryNonSpaceCharacter()
    {
        const string text = "ALPHA BETA GAMMA DELTA";
        string flattened = string.Concat(Font.Wrap(text, 30)).Replace(" ", "");
        Assert.Equal(text.Replace(" ", ""), flattened);
    }

    [Fact]
    public void Wrap_RejectsAWidthTooSmallForOneGlyph() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Font.Wrap("A", 4));

    [Fact]
    public void Wrap_EmptyStringYieldsOneEmptyLine() => Assert.Equal([""], Font.Wrap("", 100));

    [Fact]
    public void UnsupportedGlyphs_ResolveToTheReplacement()
    {
        Assert.True(Font.Supports('A'));
        Assert.False(Font.Supports('☃'));       // snowman
        Assert.Equal(BitmapFont.Replacement, Font.Resolve('☃'));
        Assert.Equal('A', Font.Resolve('A'));
    }

    [Fact]
    public void FontMetrics_RejectNonPositiveAndNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapFont(0, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapFont(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapFont(5, 7, -1));
    }
}

public sealed class TypewriterTests
{
    private static Typewriter Two(params string[] lines) => new([lines]);

    [Fact]
    public void RevealsOneGlyphEveryTwoTicks()
    {
        Typewriter writer = Two("ABC");
        Assert.Empty(writer.VisibleLines());

        writer.Tick();
        Assert.Empty(writer.VisibleLines());         // half a beat: nothing yet
        writer.Tick();
        Assert.Equal(["A"], writer.VisibleLines());
        writer.Tick();
        writer.Tick();
        Assert.Equal(["AB"], writer.VisibleLines());
    }

    [Fact]
    public void CustomCadenceIsHonoured()
    {
        var writer = new Typewriter([["ABC"]], ticksPerGlyph: 1);
        writer.Tick();
        Assert.Equal(["A"], writer.VisibleLines());
    }

    [Fact]
    public void NonPositiveCadence_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Typewriter([["A"]], 0));

    /// <summary>A line break is structure, not content: it costs no beat.</summary>
    [Fact]
    public void LineBreaksConsumeNoBeat()
    {
        Typewriter writer = Two("AB", "CD");
        for (int i = 0; i < 4; i++)
            writer.Tick();
        Assert.Equal(["AB"], writer.VisibleLines());

        writer.Tick();
        writer.Tick();
        Assert.Equal(["AB", "C"], writer.VisibleLines());
    }

    [Fact]
    public void PageCompletesWhenEveryGlyphIsVisible()
    {
        Typewriter writer = Two("AB");
        Assert.False(writer.PageComplete);
        for (int i = 0; i < 4; i++)
            writer.Tick();
        Assert.True(writer.PageComplete);
    }

    [Fact]
    public void Confirm_CompletesThePageThenAdvancesIt()
    {
        var writer = new Typewriter([["ABCDEF"], ["GH"]]);
        writer.Confirm();                             // completes page 1
        Assert.True(writer.PageComplete);
        Assert.Equal(0, writer.PageIndex);
        Assert.Equal(["ABCDEF"], writer.VisibleLines());

        writer.Confirm();                             // advances to page 2
        Assert.Equal(1, writer.PageIndex);
        Assert.False(writer.PageComplete);
        Assert.False(writer.Finished);
    }

    [Fact]
    public void Confirm_OnTheLastCompletePage_Finishes()
    {
        Typewriter writer = Two("AB");
        writer.Confirm();
        writer.Confirm();
        Assert.True(writer.Finished);

        writer.Confirm();                             // further confirms are inert
        Assert.True(writer.Finished);
        Assert.Equal(0, writer.PageIndex);
    }

    /// <summary>Confirm is edge-driven, so a held button cannot skip several pages at once.</summary>
    [Fact]
    public void TicksWhileHeld_DoNotAdvancePages()
    {
        var writer = new Typewriter([["A"], ["B"], ["C"]]);
        for (int i = 0; i < 50; i++)
            writer.Tick();
        Assert.Equal(0, writer.PageIndex);
        Assert.False(writer.Finished);
    }

    [Fact]
    public void FinishedWriter_StopsTicking()
    {
        Typewriter writer = Two("A");
        writer.Confirm();
        writer.Confirm();
        writer.Tick();
        Assert.True(writer.Finished);
    }

    [Fact]
    public void EmptyPageSet_IsImmediatelyComplete()
    {
        var writer = new Typewriter([]);
        Assert.True(writer.PageComplete);
        Assert.Empty(writer.VisibleLines());
        writer.Confirm();
        Assert.True(writer.Finished);
    }

    [Fact]
    public void PageCount_ReportsEveryPage() => Assert.Equal(3, new Typewriter([["A"], ["B"], ["C"]]).PageCount);
}

public sealed class SelectionListTests
{
    private static SelectionList List(int count, int columns = 1, bool wrap = false) =>
        new(Enumerable.Repeat(true, count), columns, wrap);

    [Fact]
    public void SelectsTheFirstEnabledEntryOnCreation()
    {
        Assert.Equal(0, List(3).Selected);
        Assert.Equal(1, new SelectionList([false, true, true]).Selected);
    }

    [Fact]
    public void EmptyControl_ExposesNoSelection()
    {
        var list = new SelectionList([]);
        Assert.Null(list.Selected);
        Assert.False(list.Move(GameAction.Down));
    }

    [Fact]
    public void AllDisabled_ExposesNoSelection() =>
        Assert.Null(new SelectionList([false, false]).Selected);

    [Fact]
    public void VerticalMovement_StepsAndStopsAtTheEnds()
    {
        SelectionList list = List(3);
        Assert.True(list.Move(GameAction.Down));
        Assert.Equal(1, list.Selected);
        Assert.True(list.Move(GameAction.Down));
        Assert.False(list.Move(GameAction.Down));    // no wrap by default
        Assert.Equal(2, list.Selected);
    }

    [Fact]
    public void DisabledEntries_StayVisibleButAreSkipped()
    {
        var list = new SelectionList([true, false, false, true]);
        Assert.True(list.Move(GameAction.Down));
        Assert.Equal(3, list.Selected);              // skipped 1 and 2
        Assert.Equal(4, list.Count);                 // still present
        Assert.False(list.IsEnabled(1));
    }

    [Fact]
    public void Wrapping_HappensOnlyWhenTheControlOptsIn()
    {
        SelectionList fixedList = List(3);
        Assert.False(fixedList.Move(GameAction.Up));
        Assert.Equal(0, fixedList.Selected);

        SelectionList wrapping = List(3, wrap: true);
        Assert.True(wrapping.Move(GameAction.Up));
        Assert.Equal(2, wrapping.Selected);
    }

    [Fact]
    public void GridMovement_UsesColumnsForVerticalSteps()
    {
        SelectionList grid = List(6, columns: 3);     // rows: [0 1 2] [3 4 5]
        Assert.True(grid.Move(GameAction.Right));
        Assert.Equal(1, grid.Selected);
        Assert.True(grid.Move(GameAction.Down));
        Assert.Equal(4, grid.Selected);
        Assert.True(grid.Move(GameAction.Up));
        Assert.Equal(1, grid.Selected);
    }

    /// <summary>Horizontal movement stays on its row unless the control wraps.</summary>
    [Fact]
    public void GridHorizontalMovement_DoesNotLeakToTheNextRow()
    {
        SelectionList grid = List(6, columns: 3);
        grid.Move(GameAction.Right);
        grid.Move(GameAction.Right);
        Assert.Equal(2, grid.Selected);
        Assert.False(grid.Move(GameAction.Right));    // would land on row 2
        Assert.Equal(2, grid.Selected);
    }

    [Fact]
    public void SingleEnabledEntry_HasNowhereToMove()
    {
        var list = new SelectionList([false, true, false], wrap: true);
        Assert.Equal(1, list.Selected);
        Assert.False(list.Move(GameAction.Down));
        Assert.Equal(1, list.Selected);
    }

    [Fact]
    public void NonDirectionalActions_DoNotMoveFocus()
    {
        SelectionList list = List(3);
        Assert.False(list.Move(GameAction.Confirm));
        Assert.Equal(0, list.Selected);
    }

    [Fact]
    public void DisablingTheSelectedEntry_MovesToTheNearestEnabled()
    {
        SelectionList list = List(3);
        list.Move(GameAction.Down);
        list.SetEnabled(1, false);
        Assert.Equal(2, list.Selected);              // prefers the next
    }

    [Fact]
    public void DisablingTheLastEnabledEntry_ClearsSelection()
    {
        var list = new SelectionList([true]);
        list.SetEnabled(0, false);
        Assert.Null(list.Selected);
    }

    [Fact]
    public void RemovingTheSelectedEntry_PrefersTheNextThenThePrevious()
    {
        SelectionList list = List(3);
        list.Move(GameAction.Down);
        list.Remove(1);
        Assert.Equal(1, list.Selected);              // the old index 2, now 1

        SelectionList tail = List(2);
        tail.Move(GameAction.Down);
        tail.Remove(1);                              // nothing after it
        Assert.Equal(0, tail.Selected);              // falls back to the previous
    }

    [Fact]
    public void RemovingAnEntryBeforeTheSelection_ShiftsTheIndex()
    {
        SelectionList list = List(3);
        list.Move(GameAction.Down);
        list.Move(GameAction.Down);
        Assert.Equal(2, list.Selected);
        list.Remove(0);
        Assert.Equal(1, list.Selected);              // same entry, new index
    }

    [Fact]
    public void RemovingTheOnlyEntry_ClearsSelection()
    {
        SelectionList list = List(1);
        list.Remove(0);
        Assert.Null(list.Selected);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void OutOfRangeIndices_AreRejected()
    {
        SelectionList list = List(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Remove(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.SetEnabled(-1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionList([true], columns: 0));
    }
}

public sealed class ResourceBarTests
{
    [Fact]
    public void ClampsPresentationIntoRange()
    {
        Assert.Equal(10, new ResourceBar(99, 10).Displayed);
        Assert.Equal(0, new ResourceBar(-5, 10).Displayed);
    }

    /// <summary>A zero maximum must read as empty rather than dividing by zero.</summary>
    [Fact]
    public void ZeroMax_IsEmptyWithoutDividing()
    {
        var bar = new ResourceBar(0, 0);
        Assert.Equal(0, bar.FillPixels(48));
        Assert.Equal(0, bar.Max);
    }

    [Fact]
    public void FillPixels_ScalesToTheTrack()
    {
        var bar = new ResourceBar(50, 100);
        Assert.Equal(24, bar.FillPixels(48));
        Assert.Equal(48, new ResourceBar(100, 100).FillPixels(48));
        Assert.Equal(0, new ResourceBar(0, 100).FillPixels(48));
    }

    /// <summary>Any remaining resource keeps at least one pixel lit, so "nearly dead" never reads
    /// as "dead".</summary>
    [Fact]
    public void ASingleRemainingPoint_StillLightsOnePixel() =>
        Assert.Equal(1, new ResourceBar(1, 999).FillPixels(48));

    [Fact]
    public void FillPixels_HandlesNonPositiveTracks()
    {
        var bar = new ResourceBar(50, 100);
        Assert.Equal(0, bar.FillPixels(0));
        Assert.Equal(0, bar.FillPixels(-8));
    }

    [Fact]
    public void FillPixels_DoesNotOverflowOnLargeValues() =>
        Assert.InRange(new ResourceBar(int.MaxValue, int.MaxValue).FillPixels(48), 0, 48);

    [Fact]
    public void AnimatesDisplayedValueTowardTheTarget()
    {
        var bar = new ResourceBar(10, 10);
        bar.Set(7);
        Assert.True(bar.IsAnimating);
        Assert.Equal(10, bar.Displayed);

        bar.Tick();
        Assert.Equal(9, bar.Displayed);
        bar.Tick();
        bar.Tick();
        Assert.Equal(7, bar.Displayed);
        Assert.False(bar.IsAnimating);

        bar.Tick();                                  // settled: no overshoot
        Assert.Equal(7, bar.Displayed);
    }

    [Fact]
    public void AnimatesUpwardToo()
    {
        var bar = new ResourceBar(2, 10);
        bar.Set(5);
        for (int i = 0; i < 3; i++)
            bar.Tick();
        Assert.Equal(5, bar.Displayed);
    }

    [Fact]
    public void CustomStep_MovesFasterWithoutOvershooting()
    {
        var bar = new ResourceBar(10, 10, step: 4);
        bar.Set(3);
        bar.Tick();
        Assert.Equal(6, bar.Displayed);
        bar.Tick();
        Assert.Equal(3, bar.Displayed);
    }

    [Fact]
    public void NonPositiveStep_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceBar(1, 1, step: 0));

    [Fact]
    public void SnapToTarget_SkipsTheAnimation()
    {
        var bar = new ResourceBar(10, 10);
        bar.Set(1);
        bar.SnapToTarget();
        Assert.Equal(1, bar.Displayed);
        Assert.False(bar.IsAnimating);
    }

    [Fact]
    public void LoweringMax_ReclampsBothValues()
    {
        var bar = new ResourceBar(10, 10);
        bar.Set(8, max: 5);
        Assert.Equal(5, bar.Max);
        Assert.Equal(5, bar.Target);
        Assert.Equal(5, bar.Displayed);
    }

    [Fact]
    public void SetNeverReportsAValueOutsideTheRange()
    {
        var bar = new ResourceBar(5, 10);
        bar.Set(-3);
        Assert.Equal(0, bar.Target);
        bar.Set(999);
        Assert.Equal(10, bar.Target);
    }
}
