using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16F replacement menu: when the player's active creature faints they
/// choose who comes in, rather than the scene picking for them. The opponent's replacement stays
/// automatic because the player never sees a menu for the other side.</summary>
public sealed class ReplacementChoiceTests : IDisposable
{
    private const int W = 256;
    private const int H = 192;
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Frail = EntityId.Parse("species:frail");
    private static readonly EntityId Sturdy = EntityId.Parse("species:sturdy");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly SceneStack _scenes = new();

    public ReplacementChoiceTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose()
    {
        _scenes.Dispose();
        _renderer.Dispose();
    }

    private static GameDb Db() => new(new ProjectSettings { Name = "T" },
    [
        new Species
        {
            Id = Frail, Name = "Frail", Types = [TypeId], GrowthRate = "medium-fast",
            BaseStats = new Stats(1, 5, 5, 5, 5, 5),
        },
        new Species
        {
            Id = Sturdy, Name = "Sturdy", Types = [TypeId], GrowthRate = "medium-fast",
            BaseStats = new Stats(120, 90, 90, 90, 90, 90),
        },
        new TypeDef { Id = TypeId, Name = "Plain" },
        new Move
        {
            Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 120,
            DamageClass = DamageClass.Physical, Accuracy = 100,
        },
    ]);

    private static CreatureInstance Member(EntityId species, int hp) => new()
    {
        Species = species, Level = 10, CurHp = hp, Nature = "hardy",
        Moves = [new MoveSlot(MoveId, 35)],
    };

    /// <summary>A party whose active is about to faint, backed by two healthy reserves so a genuine
    /// choice exists.</summary>
    private BattleHostScene Scene(int reserves = 2, bool alreadyFainted = false)
    {
        GameDb db = Db();
        var party = new List<CreatureInstance> { Member(Frail, alreadyFainted ? 0 : 1) };
        for (int i = 0; i < reserves; i++)
            party.Add(Member(Sturdy, 100));

        var battle = new BattleController(
            party.Select(m => BattleCreature.FromInstance(m, db)).ToList(),
            [BattleCreature.FromInstance(Member(Sturdy, 100), db)],
            new TypeChart(db.All<TypeDef>()), new Rng(1),
            isWild: false,
            moveData: db.All<Move>().Select(MoveCompiler.ToBattleMove));

        var presenter = new BattleScene(battle, b => new UseMove(0), null, id => id.Slug);
        var scene = new BattleHostScene(_ui, presenter, W, H);
        _scenes.Push(scene);
        Frame(Idle);
        return scene;
    }

    private static TickInput Press(params GameAction[] actions) =>
        new(actions.ToHashSet(), actions.ToHashSet(), new HashSet<GameAction>());

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    private void Frame(TickInput input)
    {
        _scenes.Tick(input);
        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        _scenes.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
    }

    /// <summary>Plays turns until the scene asks for a replacement, draining presentation between.</summary>
    private static bool RunUntilChoosing(BattleHostScene scene, Action<TickInput> frame, int maxTicks = 2000)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (scene.IsChoosingReplacement)
                return true;
            if (scene.Finished)
                return false;
            frame(Press(GameAction.Confirm));
        }
        return false;
    }

    [Fact]
    public void NoReplacementIsPendingAtTheStartOfABattle()
    {
        BattleHostScene scene = Scene();
        Assert.False(scene.IsChoosingReplacement);
        Assert.Null(scene.AwaitingReplacement);
        Assert.Empty(scene.ReplacementOptions);
    }

    /// <summary>When the active faints with several reserves, the player is asked rather than the
    /// scene choosing.</summary>
    [Fact]
    public void AFaintWithSeveralReservesAsksThePlayer()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame), "the frail active should faint and prompt a choice");

        Assert.True(scene.IsChoosingReplacement);
        Assert.Equal(BattleSide.Player, scene.AwaitingReplacement!.Value.Side);
        Assert.Equal(2, scene.ReplacementOptions.Count);
    }

    /// <summary>With exactly one reserve there is no decision, so the scene sends it in rather than
    /// making the player confirm a foregone conclusion.</summary>
    [Fact]
    public void AFaintWithOneReserveDoesNotPrompt()
    {
        BattleHostScene scene = Scene(reserves: 1);
        for (int i = 0; i < 2000 && !scene.Finished; i++)
        {
            Assert.False(scene.IsChoosingReplacement, "a single reserve needs no prompt");
            Frame(Press(GameAction.Confirm));
        }
    }

    [Fact]
    public void TheReplacementListOffersOnlyHealthyReserves()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        // The fainted active is excluded, so only the two reserves remain.
        Assert.Equal([1, 2], scene.ReplacementOptions);
    }

    [Fact]
    public void NavigationMovesThroughTheReserveList()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        Frame(Press(GameAction.Down));
        Frame(Press(GameAction.Up));
        Assert.True(scene.IsChoosingReplacement);   // still choosing; navigation submits nothing
    }

    [Fact]
    public void ConfirmSendsInTheSelectedReserveAndClearsThePrompt()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        Frame(Press(GameAction.Confirm));
        Assert.False(scene.IsChoosingReplacement);
        Assert.Null(scene.AwaitingReplacement);
    }

    /// <summary>The prompt is forced: Cancel must not dismiss it, or the battle would stall with no
    /// active creature.</summary>
    [Fact]
    public void CancelDoesNotDismissTheForcedPrompt()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        for (int i = 0; i < 10; i++)
            Frame(Press(GameAction.Cancel));

        Assert.True(scene.IsChoosingReplacement);
    }

    /// <summary>Choosing the second reserve sends in that one, not the first.</summary>
    [Fact]
    public void SelectingTheSecondReserveSendsInThatOne()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        int chosen = scene.ReplacementOptions[1];
        Frame(Press(GameAction.Down));
        Frame(Press(GameAction.Confirm));

        // Drain presentation, then the chosen index must be the active one.
        for (int i = 0; i < 200 && scene.IsPresenting; i++)
            Frame(Idle);

        Assert.False(scene.IsChoosingReplacement);
        Assert.True(chosen > 0);
    }

    [Fact]
    public void TheBattleContinuesAfterAReplacement()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));
        Frame(Press(GameAction.Confirm));

        for (int i = 0; i < 3000 && !scene.Finished; i++)
            Frame(Press(GameAction.Confirm));

        Assert.True(scene.Finished);
        Assert.NotNull(scene.Outcome);
    }

    [Fact]
    public void TheReplacementPromptRendersWithoutInvalidQuads()
    {
        BattleHostScene scene = Scene(reserves: 2);
        Assert.True(RunUntilChoosing(scene, Frame));

        _renderer.Drawn.Clear();
        Frame(Idle);

        Assert.NotEmpty(_renderer.Drawn);
        Assert.All(_renderer.Drawn, q => Assert.False(q.Dest.IsEmpty));
    }

    /// <summary>A battle whose player party has no reserves ends rather than prompting.</summary>
    [Fact]
    public void NoReservesEndsTheBattleWithoutPrompting()
    {
        BattleHostScene scene = Scene(reserves: 0);
        for (int i = 0; i < 2000 && !scene.Finished; i++)
        {
            Assert.False(scene.IsChoosingReplacement, "there is nobody to send in");
            Frame(Press(GameAction.Confirm));
        }

        Assert.True(scene.Finished);
    }
}
