using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>PHASE_16_DEMO_PLAN §3.3 item 3 and §3.4: capture through Core's maths, with a genuine
/// break-out probability, and the caught creature routed into party or storage.</summary>
public sealed class CaptureTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId WildId = EntityId.Parse("species:sprig");
    private static readonly EntityId Orb = EntityId.Parse("item:capture_orb");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public CaptureTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Species Species(EntityId id, int catchRate = 255) => new()
    {
        Id = id, Name = id.Slug, Types = [TypeId],
        BaseStats = new Stats(60, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast", BaseExp = 64, CatchRate = catchRate,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    private static GameDb Db(int catchRate = 255) => new(
        new ProjectSettings
        {
            Name = "Demo", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(2, 2),
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 2, Capacity = 3 },
        },
        [
            new Map
            {
                Id = MapId, Name = "Field", Width = 8, Height = 8,
                Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
            },
            Species(Starter), Species(WildId, catchRate),
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move
            {
                Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                DamageClass = DamageClass.Physical, Accuracy = 100,
            },
            new Item { Id = Orb, Name = "Capture Orb", Pocket = "balls", Consumable = true },
            new Item { Id = Potion, Name = "Potion", Pocket = "medicine", Consumable = true },
        ]);

    private WorldSession Session(GameDb db, IRng? rng = null)
    {
        var session = new WorldSession(db, _ui, Tile, 256, 192, rng ?? new Rng(1));
        session.InitialiseNewGame();
        return session;
    }

    // --- Bag ----------------------------------------------------------------------

    [Fact]
    public void AddingItems_Accumulates()
    {
        WorldSession session = Session(Db());
        session.AddItem(Orb, 3);
        session.AddItem(Orb, 2);
        Assert.Equal(5, session.ItemCount(Orb));
    }

    [Fact]
    public void ConsumingReducesTheCountAndRemovesTheLastOne()
    {
        WorldSession session = Session(Db());
        session.AddItem(Orb, 2);

        Assert.True(session.ConsumeItem(Orb));
        Assert.Equal(1, session.ItemCount(Orb));

        Assert.True(session.ConsumeItem(Orb));
        Assert.Equal(0, session.ItemCount(Orb));
        Assert.DoesNotContain(Orb, session.Bag.Keys);
    }

    /// <summary>A failed consume must leave the bag untouched rather than going negative.</summary>
    [Fact]
    public void ConsumingMoreThanCarried_FailsWithoutChangingTheBag()
    {
        WorldSession session = Session(Db());
        session.AddItem(Orb, 1);

        Assert.False(session.ConsumeItem(Orb, 2));
        Assert.Equal(1, session.ItemCount(Orb));
    }

    [Fact]
    public void ConsumingSomethingNotCarried_Fails() =>
        Assert.False(Session(Db()).ConsumeItem(Orb));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCounts_AreRejected(int count)
    {
        WorldSession session = Session(Db());
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AddItem(Orb, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ConsumeItem(Orb, count));
    }

    /// <summary>Capture devices are identified by their authored pocket, not by a hardcoded ID.</summary>
    [Fact]
    public void CaptureItems_AreThoseInTheBallsPocket()
    {
        WorldSession session = Session(Db());
        session.AddItem(Orb);
        session.AddItem(Potion);

        Assert.Equal([Orb], session.CaptureItems());
    }

    [Fact]
    public void CaptureItems_IsEmptyWithoutADevice()
    {
        WorldSession session = Session(Db());
        session.AddItem(Potion);
        Assert.Empty(session.CaptureItems());
    }

    [Fact]
    public void TheBagSurvivesASaveRoundTrip()
    {
        GameDb db = Db();
        WorldSession first = Session(db);
        first.Enter(MapId, new GridPos(1, 1), Facing.Down);
        first.AddItem(Orb, 4);
        first.AddItem(Potion, 2);

        WorldSession second = Session(db);
        second.Restore(first.ToSave("h"));

        Assert.Equal(4, second.ItemCount(Orb));
        Assert.Equal(2, second.ItemCount(Potion));
    }

    [Fact]
    public void RestoringReplacesTheBagRatherThanMerging()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.AddItem(Potion, 9);
        session.Enter(MapId, default, Facing.Down);

        session.Restore(new SaveFile
        {
            Map = MapId,
            Bag = new Dictionary<string, IReadOnlyList<BagEntry>> { ["balls"] = [new BagEntry(Orb, 1)] },
        });

        Assert.Equal(0, session.ItemCount(Potion));
        Assert.Equal(1, session.ItemCount(Orb));
    }

    // --- Capture in battle --------------------------------------------------------

    private (BattleController Battle, BattleScene Scene) WildBattle(GameDb db, WorldSession session,
        IRng rng, bool withOrb = true)
    {
        BattleStart start = BattleLauncher.Wild(db, session, WildId, 3, rng);
        IReadOnlyList<BattleCaptureChoice> captures = withOrb && session.CaptureItems().Count > 0
            ? [new BattleCaptureChoice(Orb)]
            : [];
        var scene = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug, captures);
        return (start.Battle!, scene);
    }

    [Fact]
    public void CarryingADevice_OffersACaptureAction()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.AddItem(Orb);

        var (_, scene) = WildBattle(db, session, new Rng(2));
        Assert.Contains(scene.Menu, item => item.Action is ThrowBall);
    }

    [Fact]
    public void CarryingNoDevice_OffersNoCaptureAction()
    {
        GameDb db = Db();
        WorldSession session = Session(db);

        var (_, scene) = WildBattle(db, session, new Rng(2), withOrb: false);
        Assert.DoesNotContain(scene.Menu, item => item.Action is ThrowBall);
    }

    /// <summary>Core refuses a throw in a trainer battle, so the option never appears.</summary>
    [Fact]
    public void ATrainerBattle_OffersNoCaptureAction()
    {
        var trainer = new Trainer
        {
            Id = EntityId.Parse("trainer:rival"), Name = "Rival", SightRange = 3,
            Party = [new PartyMember { Species = WildId, Level = 3 }],
        };
        var db = new GameDb(Db().Settings, [.. Db().Entities, trainer]);

        WorldSession session = Session(db);
        session.AddItem(Orb);

        BattleStart start = BattleLauncher.Trainer(db, session, trainer, new Rng(2));
        var scene = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug,
            [new BattleCaptureChoice(Orb)]);

        Assert.DoesNotContain(scene.Menu, item => item.Action is ThrowBall);
    }

    [Fact]
    public void ThrowingADevice_ProducesCaptureEvents()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.AddItem(Orb);

        var (_, scene) = WildBattle(db, session, new Rng(2));
        scene.Submit(new ThrowBall(1.0, 1.0));

        Assert.Contains(scene.Events, e => e is BallThrown);
        Assert.Contains(scene.Events, e => e is Captured or BrokeFree);
    }

    /// <summary>A high catch rate against a full-health target still succeeds, and Core reports it.</summary>
    [Fact]
    public void AGuaranteedCapture_Succeeds()
    {
        GameDb db = Db(catchRate: 255);
        WorldSession session = Session(db);
        session.AddItem(Orb);

        var (battle, scene) = WildBattle(db, session, new Rng(2));
        scene.Submit(new ThrowBall(255.0, 1.0));   // overwhelming bonus

        Assert.True(battle.Captured);
        Assert.Contains(scene.Events, e => e is Captured);
    }

    // --- Break-out probability (demo plan §3.4) -----------------------------------

    /// <summary>Break-out must be genuinely random: across many seeded attempts at a low catch rate,
    /// both outcomes occur. A capture that always succeeded or always failed would pass a single
    /// smoke test and still be broken.</summary>
    [Fact]
    public void CaptureIsProbabilisticAcrossManySeeds()
    {
        int captured = 0;
        const int attempts = 1000;

        for (int seed = 0; seed < attempts; seed++)
        {
            CaptureResult result = CaptureCalc.Attempt(
                maxHp: 100, curHp: 100, catchRate: 45, ballBonus: 1.0, statusBonus: 1.0, new Rng(seed));
            if (result.Caught)
                captured++;
        }

        Assert.InRange(captured, 1, attempts - 1);
    }

    /// <summary>Weakening the target must raise the capture rate, or the HP term is being ignored.</summary>
    [Fact]
    public void LowerHealthRaisesTheCaptureRate()
    {
        int Rate(int curHp)
        {
            int captured = 0;
            for (int seed = 0; seed < 1000; seed++)
                if (CaptureCalc.Attempt(100, curHp, 45, 1.0, 1.0, new Rng(seed)).Caught)
                    captured++;
            return captured;
        }

        Assert.True(Rate(10) > Rate(100),
            "a weakened target should be easier to catch than a healthy one");
    }

    [Fact]
    public void TheSameSeedGivesTheSameCaptureOutcome()
    {
        CaptureResult First() => CaptureCalc.Attempt(100, 50, 45, 1.0, 1.0, new Rng(77));
        Assert.Equal(First(), First());
    }

    // --- Deposit ------------------------------------------------------------------

    [Fact]
    public void ACapturedCreatureJoinsTheParty()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.AddItem(Orb);

        var (battle, scene) = WildBattle(db, session, new Rng(2));
        scene.Submit(new ThrowBall(255.0, 1.0));
        Assert.True(battle.Captured);

        DepositResult result = BattleLauncher.DepositCaptured(db, session, battle)!.Value;
        Assert.Equal(DepositTarget.Party, result.Target);
        Assert.Equal(2, session.Party.Count);
        Assert.Equal(WildId, session.Party[1].Species);
    }

    /// <summary>With a full party the capture goes to storage rather than being lost.</summary>
    [Fact]
    public void ACapturedCreatureOverflowsToStorage()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        while (session.Party.Count < PartyStorage.MaxParty)
            session.Deposit(session.Party[0]);
        session.AddItem(Orb);

        var (battle, scene) = WildBattle(db, session, new Rng(2));
        scene.Submit(new ThrowBall(255.0, 1.0));

        DepositResult result = BattleLauncher.DepositCaptured(db, session, battle)!.Value;
        Assert.Equal(DepositTarget.Box, result.Target);
        Assert.Single(session.Boxes[0]);
    }

    [Fact]
    public void ACapturedCreatureKeepsItsBattleHealth()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.AddItem(Orb);

        var (battle, scene) = WildBattle(db, session, new Rng(2));
        battle.Party(BattleSide.Enemy)[0].TakeDamage(5);
        scene.Submit(new ThrowBall(255.0, 1.0));

        BattleLauncher.DepositCaptured(db, session, battle);
        Assert.Equal(battle.Party(BattleSide.Enemy)[0].CurrentHp, session.Party[1].CurHp);
    }

    [Fact]
    public void AFullPartyAndFullStorage_ReportsRatherThanSilentlyLosingIt()
    {
        var db = new GameDb(
            Db().Settings with { Boxes = new BoxConfig { Count = 1, Capacity = 1 } },
            Db().Entities.ToList());

        WorldSession session = Session(db);
        while (session.Party.Count < PartyStorage.MaxParty)
            session.Deposit(session.Party[0]);
        session.Deposit(session.Party[0]);   // fills the only box
        session.AddItem(Orb);

        var (battle, scene) = WildBattle(db, session, new Rng(2));
        scene.Submit(new ThrowBall(255.0, 1.0));

        var reported = new List<string>();
        Assert.Null(BattleLauncher.DepositCaptured(db, session, battle, reported.Add));
        Assert.Contains(reported, line => line.Contains("full"));
    }

    [Fact]
    public void DepositCaptured_RejectsNullArguments()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.DepositCaptured(null!, session, null!));
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.DepositCaptured(db, session, null!));
    }
}
