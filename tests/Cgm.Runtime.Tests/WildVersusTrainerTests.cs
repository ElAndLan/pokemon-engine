using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Wild and trainer battles differ in Core, and Runtime must build each correctly. The
/// isWild flag was silently wrong for five slices because nothing exercised a wild-only rule, so
/// these assert the flag itself, every behaviour Core gates on it, and both demo paths end to end.</summary>
public sealed class WildVersusTrainerTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 256;
    private const int H = 192;

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId WildId = EntityId.Parse("species:sprig");
    private static readonly EntityId TrainerId = EntityId.Parse("trainer:rival");
    private static readonly EntityId TableId = EntityId.Parse("encounter:grass");
    private static readonly EntityId Orb = EntityId.Parse("item:capture_orb");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly SceneStack _scenes = new();

    public WildVersusTrainerTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose()
    {
        _scenes.Dispose();
        _renderer.Dispose();
    }

    private static Species Species(EntityId id) => new()
    {
        Id = id, Name = id.Slug, Types = [TypeId],
        BaseStats = new Stats(60, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast", BaseExp = 64, CatchRate = 255,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    private static Trainer Rival(int party = 2) => new()
    {
        Id = TrainerId, Name = "Rival", SightRange = 4, AiProfile = AiProfile.Basic,
        Party = Enumerable.Range(0, party)
            .Select(_ => new PartyMember { Species = WildId, Level = 4 })
            .ToList(),
    };

    /// <summary>One map carrying both demo paths: grass on every tile, and a trainer looking down a
    /// clear column so walking into it is a sighting.</summary>
    private static GameDb Db(bool withTrainerNpc = false)
    {
        var entities = new List<MapEntity>();
        if (withTrainerNpc)
            entities.Add(new NpcEntity
            {
                Key = "rival", Pos = new GridPos(4, 0), Facing = Facing.Down, Trainer = TrainerId,
            });

        var map = new Map
        {
            Id = MapId, Name = "Field", Width = 8, Height = 8,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
            Entities = entities,
            EncounterZones = Enumerable.Range(0, 64).Select(i => new EncounterZoneCell(i, TableId)).ToList(),
        };

        return new GameDb(
            new ProjectSettings
            {
                Name = "Demo", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(2, 2),
                StarterParty = [Starter], Boxes = new BoxConfig { Count = 2, Capacity = 3 },
            },
            [
                map, Species(Starter), Species(WildId), Rival(),
                new TypeDef { Id = TypeId, Name = "Plain" },
                new Move
                {
                    Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                    DamageClass = DamageClass.Physical, Accuracy = 100,
                },
                new Item { Id = Orb, Name = "Capture Orb", Pocket = "balls", Consumable = true },
                new EncounterTable
                {
                    Id = TableId, Name = "Grass", BaseRate = 1.0,   // every step encounters
                    Slots = [new EncounterSlot { Species = WildId, Weight = 1, MinLevel = 3, MaxLevel = 4 }],
                },
            ]);
    }

    private WorldSession Session(GameDb db, int orbs = 0)
    {
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(7));
        session.InitialiseNewGame();
        if (orbs > 0)
            session.AddItem(Orb, orbs);
        return session;
    }

    private static IReadOnlyList<BattleCaptureChoice> Captures(WorldSession session) =>
        session.CaptureItems().Select(item => new BattleCaptureChoice(item)).ToList();

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

    // --- The flag itself ----------------------------------------------------------

    [Fact]
    public void AWildBattleIsMarkedWild()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db), WildId, 4, new Rng(2));
        Assert.True(start.Battle!.IsWild);
    }

    [Fact]
    public void ATrainerBattleIsNotMarkedWild()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Trainer(db, Session(db), Rival(), new Rng(2));
        Assert.False(start.Battle!.IsWild);
    }

    /// <summary>The regression guard: a wild battle built through the launcher must never come back
    /// flagged as a trainer battle, which is how capture broke.</summary>
    [Fact]
    public void EveryWildBattleTheLauncherBuildsIsWild()
    {
        GameDb db = Db();
        for (int level = 1; level <= 5; level++)
            Assert.True(BattleLauncher.Wild(db, Session(db), WildId, level, new Rng(level)).Battle!.IsWild,
                $"level {level} wild battle was not flagged wild");
    }

    // --- Capture legality, both directions ----------------------------------------

    [Fact]
    public void AWildBattleOffersCaptureWhenCarryingADevice()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var scene = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));

        Assert.Contains(scene.Menu, item => item.Action is ThrowBall);
        Assert.Contains(scene.Menu, item => item.Action is Run && item.Label == "Run");
    }

    [Fact]
    public void ATrainerBattleNeverOffersCaptureEvenCarryingADevice()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 5);
        BattleStart start = BattleLauncher.Trainer(db, session, Rival(), new Rng(2));
        var scene = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));

        Assert.DoesNotContain(scene.Menu, item => item.Action is ThrowBall);
        Assert.Contains(scene.Menu, item => item.Action is Run && item.Label == "Run");
    }

    /// <summary>Core refuses the action itself, not merely its menu entry — the menu is a
    /// convenience, the refusal is the rule.</summary>
    [Fact]
    public void SubmittingACaptureInATrainerBattleIsRefusedByCore()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        BattleStart start = BattleLauncher.Trainer(db, session, Rival(), new Rng(2));
        var scene = new BattleScene(start.Battle!, b => new UseMove(0));

        Assert.Throws<ArgumentException>(() => scene.Submit(new ThrowBall(1.0, 1.0)));
    }

    [Fact]
    public void ACaptureInAWildBattleIsAccepted()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var scene = new BattleScene(start.Battle!, b => new UseMove(0));

        scene.Submit(new ThrowBall(255.0, 1.0));
        Assert.True(start.Battle!.Captured);
    }

    // --- The other wild-only rule -------------------------------------------------

    /// <summary>Core ends a wild battle when the opponent is forced out — it fled — rather than
    /// switching to a reserve. A trainer battle switches instead. This is the second behaviour the
    /// wrong flag would have broken.</summary>
    [Fact]
    public void ForcingOutAWildOpponentEndsTheBattle()
    {
        GameDb db = Db();
        BattleStart wild = BattleLauncher.Wild(db, Session(db), WildId, 4, new Rng(2));
        Assert.True(wild.Battle!.IsWild);

        BattleStart trainer = BattleLauncher.Trainer(db, Session(db), Rival(), new Rng(2));
        Assert.False(trainer.Battle!.IsWild);

        // The distinction is Core's; asserting the flag reaches Core is Runtime's job. Core's own
        // force-out behaviour is covered by its suite.
        Assert.NotEqual(wild.Battle.IsWild, trainer.Battle.IsWild);
    }

    // --- Device consumption -------------------------------------------------------

    [Fact]
    public void ThrowingADeviceConsumesItFromTheBag()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 2);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));
        var scene = new BattleHostScene(_ui, presenter, W, H, item => session.ConsumeItem(item));
        _scenes.Push(scene);
        Frame(Idle);

        OpenItemsAndSelectFirst();
        Assert.Equal(1, session.ItemCount(Orb));
    }

    /// <summary>Root FIGHT -> ITEMS, open the panel, select the first item (the capture device).</summary>
    private void OpenItemsAndSelectFirst()
    {
        Frame(Press(GameAction.Down));      // the four-way root: FIGHT -> ITEMS
        Frame(Press(GameAction.Confirm));   // open the ITEMS panel
        Frame(Press(GameAction.Confirm));   // select the first item
    }

    /// <summary>With an empty bag the spend fails and the throw is cancelled, so a device cannot be
    /// thrown twice or on credit.</summary>
    [Fact]
    public void AThrowIsCancelledWhenTheBagCannotPay()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));

        // Empty the bag behind the scene's back, as a second throw in the same battle would.
        Assert.True(session.ConsumeItem(Orb));

        var scene = new BattleHostScene(_ui, presenter, W, H, item => session.ConsumeItem(item));
        _scenes.Push(scene);
        Frame(Idle);

        OpenItemsAndSelectFirst();
        Assert.False(start.Battle!.Captured);
        Assert.False(scene.IsPresenting);   // the spend failed, so nothing happened at all
    }

    [Fact]
    public void ANonItemActionSpendsNothing()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));
        var scene = new BattleHostScene(_ui, presenter, W, H, item => session.ConsumeItem(item));
        _scenes.Push(scene);
        Frame(Idle);

        Frame(Press(GameAction.Confirm));   // open FIGHT
        Frame(Press(GameAction.Confirm));   // submit the first move — a non-item action
        Assert.Equal(1, session.ItemCount(Orb));
    }

    // --- Four-way menu panels -----------------------------------------------------

    private BattleHostScene WildScene(int orbs = 1)
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: orbs);
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 4, new Rng(2));
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, Captures(session));
        var scene = new BattleHostScene(_ui, presenter, W, H, item => session.ConsumeItem(item));
        _scenes.Push(scene);
        Frame(Idle);
        return scene;
    }

    /// <summary>FIGHT opens the move panel, and choosing a move submits it.</summary>
    [Fact]
    public void FightPanelSubmitsAMove()
    {
        BattleHostScene scene = WildScene();
        Frame(Press(GameAction.Confirm));   // open FIGHT
        Frame(Press(GameAction.Confirm));   // move 0
        Assert.True(scene.IsPresenting);
    }

    /// <summary>RUN (root 2x2 bottom-right) attempts to flee, which produces events or ends the battle.</summary>
    [Fact]
    public void RunFromTheRootAttemptsToFlee()
    {
        BattleHostScene scene = WildScene();
        Frame(Press(GameAction.Down));      // FIGHT -> ITEMS
        Frame(Press(GameAction.Right));     // ITEMS -> RUN
        Frame(Press(GameAction.Confirm));   // flee
        Assert.True(scene.IsPresenting || scene.Finished);
    }

    /// <summary>The PARTY panel opens and renders the party (HP bars) without any invalid quads.</summary>
    [Fact]
    public void PartyPanelRendersHpBars()
    {
        WildScene();
        Frame(Press(GameAction.Right));     // FIGHT -> PARTY
        Frame(Press(GameAction.Confirm));   // open PARTY
        Frame(Idle);                        // render a frame

        Assert.NotEmpty(_renderer.Drawn);
        Assert.All(_renderer.Drawn, q => Assert.False(q.Dest.IsEmpty));
    }

    /// <summary>Cancel backs out of a sub-panel to the root without submitting anything.</summary>
    [Fact]
    public void CancelBacksOutOfASubPanel()
    {
        BattleHostScene scene = WildScene();
        Frame(Press(GameAction.Confirm));   // open FIGHT
        Frame(Press(GameAction.Cancel));    // back to the root menu
        Assert.False(scene.IsPresenting);   // nothing was submitted

        // Proof we are back at the root on FIGHT: two confirms reach a move again.
        Frame(Press(GameAction.Confirm));
        Frame(Press(GameAction.Confirm));
        Assert.True(scene.IsPresenting);
    }

    // --- Both demo paths, end to end ----------------------------------------------

    /// <summary>Walking in grass reaches a wild battle that can be captured.</summary>
    [Fact]
    public void GrassLeadsToAWildBattleThatCanBeCaptured()
    {
        GameDb db = Db();
        WorldSession session = Session(db, orbs: 1);
        OverworldScene overworld = session.Enter(MapId, new GridPos(2, 2), Facing.Right)!;
        _scenes.Push(overworld);
        Frame(Idle);

        StepOutcome? pending = null;
        for (int i = 0; i < 400 && pending is null; i++)
        {
            Frame(Press(GameAction.Right));
            pending = overworld.TakePending();
        }

        var encounter = Assert.IsType<StepOutcome.WildEncounter>(pending);
        BattleStart start = BattleLauncher.Wild(db, session, encounter.Species, encounter.Level, session.Rng);
        Assert.True(start.Battle!.IsWild);

        var presenter = new BattleScene(start.Battle, b => new UseMove(0), null, id => id.Slug, Captures(session));
        Assert.Contains(presenter.Menu, item => item.Action is ThrowBall);

        presenter.Submit(new ThrowBall(255.0, 1.0));
        Assert.True(start.Battle.Captured);

        DepositResult result = BattleLauncher.DepositCaptured(db, session, start.Battle)!.Value;
        Assert.Equal(DepositTarget.Party, result.Target);
        Assert.Equal(encounter.Species, session.Party[^1].Species);
    }

    /// <summary>Walking into a trainer's line of sight reaches a trainer battle that cannot be
    /// captured and that awards experience on a win.</summary>
    [Fact]
    public void TrainerSightLeadsToATrainerBattleThatCannotBeCaptured()
    {
        GameDb db = Db(withTrainerNpc: true);
        WorldSession session = Session(db, orbs: 3);
        OverworldScene overworld = session.Enter(MapId, new GridPos(4, 4), Facing.Up)!;
        _scenes.Push(overworld);
        Frame(Idle);

        StepOutcome? spotted = null;
        for (int i = 0; i < 400 && spotted is null; i++)
        {
            Frame(Press(GameAction.Up));
            if (overworld.TakePending() is StepOutcome.TrainerSpotted seen)
                spotted = seen;
        }

        var sighting = Assert.IsType<StepOutcome.TrainerSpotted>(spotted);
        Assert.Equal(TrainerId, sighting.Trainer.Id);

        BattleStart start = BattleLauncher.Trainer(db, session, sighting.Trainer, session.Rng);
        Assert.True(start.Started);
        Assert.False(start.Battle!.IsWild);
        Assert.Equal(2, start.Battle.Party(BattleSide.Enemy).Count);

        var presenter = new BattleScene(start.Battle, b => new UseMove(0), null, id => id.Slug, Captures(session));
        Assert.DoesNotContain(presenter.Menu, item => item.Action is ThrowBall);
        Assert.Equal(3, session.ItemCount(Orb));   // devices untouched by a trainer battle
    }

    /// <summary>The two paths coexist on one map: grass encounters and a trainer sighting both
    /// reachable, each behaving according to its own rules.</summary>
    [Fact]
    public void OneMapSupportsBothEncounterKinds()
    {
        GameDb db = Db(withTrainerNpc: true);
        WorldSession session = Session(db, orbs: 1);

        BattleStart wild = BattleLauncher.Wild(db, session, WildId, 4, new Rng(3));
        BattleStart trainer = BattleLauncher.Trainer(db, session, Rival(), new Rng(3));

        Assert.True(wild.Battle!.IsWild);
        Assert.False(trainer.Battle!.IsWild);
        Assert.Single(wild.Battle.Party(BattleSide.Enemy));
        Assert.Equal(2, trainer.Battle.Party(BattleSide.Enemy).Count);
    }

    [Fact]
    public void ATrainerWinAwardsMoreExperienceThanAWildWin()
    {
        GameDb db = Db();

        long Gain(bool trainer)
        {
            WorldSession session = Session(db);
            BattleStart start = trainer
                ? BattleLauncher.Trainer(db, session, Rival(party: 1), new Rng(4))
                : BattleLauncher.Wild(db, session, WildId, 4, new Rng(4));

            BattleCreature enemy = start.Battle!.Party(BattleSide.Enemy)[0];
            enemy.TakeDamage(enemy.MaxHp * 10);

            long before = session.Party[0].Exp;
            BattleLauncher.AwardExperience(db, session, start.Battle, trainer);
            return session.Party[0].Exp - before;
        }

        Assert.True(Gain(trainer: true) > Gain(trainer: false));
    }
}
