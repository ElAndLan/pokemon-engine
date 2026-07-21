using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16C reachability row: headless input scripts drive Title, New,
/// Continue, and Menu and back, through the real scene stack, UI kit, and input edges.</summary>
public sealed class SceneFlowTests : IDisposable
{
    private const int W = 256;
    private const int H = 192;

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly SceneStack _stack = new();

    public SceneFlowTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose()
    {
        _stack.Dispose();
        _renderer.Dispose();
    }

    private static TickInput Press(params GameAction[] pressed) =>
        new(pressed.ToHashSet(), pressed.ToHashSet(), new HashSet<GameAction>());

    private static TickInput Idle =>
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    /// <summary>Drives one frame end to end: tick the stack, then render through the real batch and
    /// renderer, so a scene that draws an invalid quad fails the test.</summary>
    private void Frame(TickInput input)
    {
        _stack.Tick(input);
        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        _stack.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
    }

    private TitleScene PushTitle(bool continueAvailable = true)
    {
        var title = new TitleScene(_ui, W, H, "CREATURE GAME", continueAvailable);
        _stack.Push(title);
        Frame(Idle);   // applies the queued push and enters the scene
        return title;
    }

    // --- Title -------------------------------------------------------------------

    [Fact]
    public void Title_EntersAndRendersWithoutInvalidQuads()
    {
        PushTitle();
        Assert.NotEmpty(_renderer.Drawn);
        Assert.Equal(1, _renderer.FramesEnded);
    }

    [Fact]
    public void Title_StartsOnNewGame()
    {
        TitleScene title = PushTitle();
        Assert.Equal(0, title.SelectedIndex);
        Assert.Equal(TitleChoice.None, title.Choice);
    }

    [Fact]
    public void Title_ConfirmOnNewGame_ChoosesNewGame()
    {
        TitleScene title = PushTitle();
        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.NewGame, title.Choice);
    }

    [Fact]
    public void Title_DownThenConfirm_ChoosesContinue()
    {
        TitleScene title = PushTitle();
        Frame(Press(GameAction.Down));
        Assert.Equal(1, title.SelectedIndex);
        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.Continue, title.Choice);
    }

    /// <summary>With no save, Continue stays visible but cannot be selected or confirmed.</summary>
    [Fact]
    public void Title_WithoutASave_CannotReachContinue()
    {
        TitleScene title = PushTitle(continueAvailable: false);
        Frame(Press(GameAction.Down));
        Assert.Equal(0, title.SelectedIndex);

        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.NewGame, title.Choice);
    }

    [Fact]
    public void Title_IgnoresFurtherInputOnceChosen()
    {
        TitleScene title = PushTitle();
        Frame(Press(GameAction.Confirm));
        Frame(Press(GameAction.Down));
        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.NewGame, title.Choice);   // the host owns the transition now
    }

    /// <summary>Held Confirm must not re-trigger: only the press edge counts.</summary>
    [Fact]
    public void Title_HeldConfirmWithoutAnEdge_DoesNothing()
    {
        TitleScene title = PushTitle();
        var held = new TickInput(new HashSet<GameAction> { GameAction.Confirm },
            new HashSet<GameAction>(), new HashSet<GameAction>());
        Frame(held);
        Assert.Equal(TitleChoice.None, title.Choice);
    }

    // --- Menu overlay ------------------------------------------------------------

    [Fact]
    public void Menu_PushesOverTitleAndBothRender()
    {
        PushTitle();
        _renderer.Drawn.Clear();

        _stack.Push(new MenuScene(_ui, W, H, ["PARTY", "BAG", "SAVE"]));
        Frame(Idle);

        Assert.Equal(2, _stack.Count);
        Assert.Equal(2, _stack.RenderOrder().Count);   // overlay does not hide the title
    }

    [Fact]
    public void Menu_NavigatesAndConfirms()
    {
        PushTitle();
        var menu = new MenuScene(_ui, W, H, ["PARTY", "BAG", "SAVE"]);
        _stack.Push(menu);
        Frame(Idle);

        Frame(Press(GameAction.Down));
        Assert.Equal(1, menu.SelectedIndex);
        Frame(Press(GameAction.Confirm));
        Assert.Equal(1, menu.Chosen);
    }

    [Fact]
    public void Menu_WrapsBecauseItOptsIn()
    {
        PushTitle();
        var menu = new MenuScene(_ui, W, H, ["A", "B", "C"]);
        _stack.Push(menu);
        Frame(Idle);

        Frame(Press(GameAction.Up));
        Assert.Equal(2, menu.SelectedIndex);
    }

    [Fact]
    public void Menu_SkipsDisabledEntriesButKeepsThemVisible()
    {
        PushTitle();
        var menu = new MenuScene(_ui, W, H, ["A", "B", "C"], [true, false, true]);
        _stack.Push(menu);
        Frame(Idle);

        Frame(Press(GameAction.Down));
        Assert.Equal(2, menu.SelectedIndex);
        Assert.NotEmpty(_renderer.Drawn);   // B still drew
    }

    /// <summary>Cancel closes the overlay and the title beneath is active again — the "and back"
    /// half of the reachability row.</summary>
    [Fact]
    public void Menu_CancelClosesAndReturnsFocusToTheSceneBelow()
    {
        TitleScene title = PushTitle();
        var menu = new MenuScene(_ui, W, H, ["PARTY"]);
        _stack.Push(menu);
        Frame(Idle);

        Frame(Press(GameAction.Cancel));
        Assert.True(menu.Closed);

        _stack.Pop();
        Frame(Idle);
        Assert.Same(title, _stack.Active);

        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.NewGame, title.Choice);   // the title takes input again
    }

    [Fact]
    public void Menu_EmptyControlStillAcceptsCancel()
    {
        PushTitle();
        var menu = new MenuScene(_ui, W, H, []);
        _stack.Push(menu);
        Frame(Idle);

        Assert.Null(menu.SelectedIndex);
        Frame(Press(GameAction.Cancel));
        Assert.True(menu.Closed);
    }

    [Fact]
    public void Menu_ClosedIgnoresFurtherInput()
    {
        PushTitle();
        var menu = new MenuScene(_ui, W, H, ["A", "B"]);
        _stack.Push(menu);
        Frame(Idle);

        Frame(Press(GameAction.Cancel));
        Frame(Press(GameAction.Confirm));
        Assert.Null(menu.Chosen);
    }

    // --- Full script -------------------------------------------------------------

    /// <summary>One uninterrupted script: Title → Menu → back → Continue.</summary>
    [Fact]
    public void ReachabilityScript_TitleToMenuAndBackToContinue()
    {
        TitleScene title = PushTitle();

        var menu = new MenuScene(_ui, W, H, ["PARTY", "SAVE"]);
        _stack.Push(menu);
        Frame(Idle);
        Frame(Press(GameAction.Down));
        Frame(Press(GameAction.Confirm));
        Assert.Equal(1, menu.Chosen);

        Frame(Press(GameAction.Cancel));
        _stack.Pop();
        Frame(Idle);

        Frame(Press(GameAction.Down));
        Frame(Press(GameAction.Confirm));
        Assert.Equal(TitleChoice.Continue, title.Choice);
        Assert.Equal(1, _stack.Count);
    }

    [Fact]
    public void FadedReplace_BlocksInputThenCompletes()
    {
        TitleScene title = PushTitle();
        _stack.Replace(new MenuScene(_ui, W, H, ["A"]));

        for (int i = 0; i < SceneStack.FadeTicks; i++)
            Frame(Press(GameAction.Confirm));       // input is blocked while fading
        Assert.Equal(TitleChoice.None, title.Choice);

        for (int i = 0; i < SceneStack.FadeTicks + 2; i++)
            Frame(Idle);
        Assert.False(_stack.IsTransitioning);
        Assert.IsType<MenuScene>(_stack.Active);
    }
}

/// <summary>The procedural glyph atlas backing every UI primitive.</summary>
public sealed class FontAtlasTests
{
    [Fact]
    public void Rgba_MatchesTheDeclaredDimensions() =>
        Assert.Equal(FontAtlas.Width * FontAtlas.Height * 4, FontAtlas.Rgba().Length);

    [Fact]
    public void Atlas_ValidatesAsATexture()
    {
        TextureInfo info = TextureInfo.Validate(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba().Length);
        Assert.Equal(FontAtlas.Width, info.Width);
    }

    [Fact]
    public void EveryGlyphSourceLiesInsideTheAtlas()
    {
        var info = new TextureInfo(FontAtlas.Width, FontAtlas.Height);
        Assert.True(info.Contains(FontAtlas.Solid));
        foreach (char value in FontAtlas.Order)
            Assert.True(info.Contains(FontAtlas.Source(value)), $"glyph '{value}' escapes the atlas");
    }

    [Fact]
    public void UnsupportedCharacters_FallBackToTheReplacementGlyph()
    {
        Assert.False(FontAtlas.Supports('☃'));
        Assert.Equal(FontAtlas.Source(BitmapFont.Replacement), FontAtlas.Source('☃'));
    }

    [Fact]
    public void LowercaseResolvesToItsUppercaseGlyph()
    {
        Assert.True(FontAtlas.Supports('a'));
        Assert.Equal(FontAtlas.Source('A'), FontAtlas.Source('a'));
    }

    /// <summary>The solid cell must be opaque, or every panel and fade would draw nothing.</summary>
    [Fact]
    public void SolidCell_IsOpaqueWhite()
    {
        byte[] pixels = FontAtlas.Rgba();
        Assert.Equal(255, pixels[0]);
        Assert.Equal(255, pixels[3]);
    }

    [Fact]
    public void GlyphPixels_AreWrittenForALetter()
    {
        byte[] pixels = FontAtlas.Rgba();
        RectI source = FontAtlas.Source('A');
        int lit = 0;
        for (int y = source.Y; y < source.Bottom; y++)
            for (int x = source.X; x < source.Right; x++)
                if (pixels[(y * FontAtlas.Width + x) * 4 + 3] != 0)
                    lit++;
        Assert.True(lit > 5, $"'A' only lit {lit} pixels");
    }
}
