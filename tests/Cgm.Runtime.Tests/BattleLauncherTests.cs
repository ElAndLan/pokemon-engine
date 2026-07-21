using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16D battle entry and return: Runtime composes participants from live
/// session state, Core resolves the battle, and the result is written back through Core values.</summary>
public sealed class BattleLauncherTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapA = EntityId.Parse("map:town");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId Wild = EntityId.Parse("species:sprig");
    private static readonly EntityId TrainerId = EntityId.Parse("trainer:rival");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public BattleLauncherTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Species Species(EntityId id, int baseExp = 60) => new()
    {
        Id = id,
        Name = id.Slug,
        Types = [TypeId],
        BaseStats = new Stats(50, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast",
        BaseExp = baseExp,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    private GameDb Db(Trainer? trainer = null)
    {
        var settings = new ProjectSettings
        {
            Name = "T", TileSize = Tile, StarterParty = [Starter],
            Boxes = new BoxConfig { Count = 1, Capacity = 5 },
        };
        var entities = new List<IEntity>
        {
            new Map
            {
                Id = MapA, Name = "Town", Width = 8, Height = 8,
                Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
            },
            Species(Starter),
            Species(Wild),
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move { Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40 },
        };
        if (trainer is not null)
            entities.Add(trainer);
        return new GameDb(settings, entities);
    }

    private WorldSession Session(GameDb db, bool withParty = true)
    {
        var session = new WorldSession(db, _ui, Tile, 256, 192, new Rng(1));
        if (withParty)
            session.InitialiseNewGame();
        return session;
    }

    private static Trainer Rival(int partyCount = 1) => new()
    {
        Id = TrainerId,
        Name = "Rival",
        SightRange = 3,
        Party = Enumerable.Range(0, partyCount)
            .Select(_ => new PartyMember { Species = Wild, Level = 5 })
            .ToList(),
    };

    // --- Wild battles -------------------------------------------------------------

    [Fact]
    public void Wild_BuildsABattleFromThePartyAndRolledSpecies()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db), Wild, 5, new Rng(2));

        Assert.True(start.Started);
        Assert.Equal(BattleStartRefusal.None, start.Refusal);
        Assert.Single(start.Battle!.Party(BattleSide.Player));
        Assert.Single(start.Battle.Party(BattleSide.Enemy));
        Assert.Equal(Wild, start.Battle.Party(BattleSide.Enemy)[0].Species);
    }

    [Fact]
    public void Wild_UsesTheRequestedLevel()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db), Wild, 9, new Rng(2));
        Assert.Equal(9, start.Battle!.Party(BattleSide.Enemy)[0].Level);
    }

    [Fact]
    public void Wild_GivesTheOpponentLearnsetMoves()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db), Wild, 5, new Rng(2));
        Assert.NotEmpty(start.Battle!.Party(BattleSide.Enemy)[0].Moves);
    }

    /// <summary>Same seed, same wild creature: encounters must replay identically.</summary>
    [Fact]
    public void Wild_IsDeterministicForASeed()
    {
        GameDb db = Db();
        static (int MaxHp, int Level, int Moves) Run(GameDb db, WorldSession session)
        {
            BattleCreature wild = BattleLauncher.Wild(db, session, Wild, 5, new Rng(77))
                .Battle!.Party(BattleSide.Enemy)[0];
            // MaxHp derives from the generated IVs and nature, so equal stats mean equal generation.
            return (wild.MaxHp, wild.Level, wild.Moves.Count);
        }

        Assert.Equal(Run(db, Session(db)), Run(db, Session(db)));
    }

    [Fact]
    public void Wild_RefusesAnUnknownSpeciesRatherThanSubstituting()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db), EntityId.Parse("species:absent"), 5, new Rng(2));

        Assert.False(start.Started);
        Assert.Equal(BattleStartRefusal.UnknownSpecies, start.Refusal);
    }

    [Fact]
    public void Wild_RefusesWithAnEmptyParty()
    {
        GameDb db = Db();
        BattleStart start = BattleLauncher.Wild(db, Session(db, withParty: false), Wild, 5, new Rng(2));

        Assert.False(start.Started);
        Assert.Equal(BattleStartRefusal.EmptyParty, start.Refusal);
    }

    /// <summary>A whited-out party cannot fight; that path belongs to blackout, not a new battle.</summary>
    [Fact]
    public void Wild_RefusesWhenEveryPartyMemberHasFainted()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        session.Party[0] = session.Party[0] with { CurHp = 0 };

        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));
        Assert.Equal(BattleStartRefusal.PartyFainted, start.Refusal);
    }

    [Fact]
    public void Wild_RejectsNullArguments()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.Wild(null!, session, Wild, 5, new Rng(1)));
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.Wild(db, null!, Wild, 5, new Rng(1)));
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.Wild(db, session, Wild, 5, null!));
    }

    // --- Trainer battles ----------------------------------------------------------

    [Fact]
    public void Trainer_BuildsTheAuthoredParty()
    {
        Trainer rival = Rival(partyCount: 3);
        GameDb db = Db(rival);
        BattleStart start = BattleLauncher.Trainer(db, Session(db), rival, new Rng(2));

        Assert.True(start.Started);
        Assert.Equal(3, start.Battle!.Party(BattleSide.Enemy).Count);
    }

    [Fact]
    public void Trainer_RefusesAnEmptyOpponentParty()
    {
        Trainer rival = Rival(partyCount: 0);
        GameDb db = Db(rival);
        BattleStart start = BattleLauncher.Trainer(db, Session(db), rival, new Rng(2));

        Assert.False(start.Started);
        Assert.Equal(BattleStartRefusal.EmptyOpponent, start.Refusal);
    }

    [Fact]
    public void Trainer_RefusesAnUnknownSpeciesInItsParty()
    {
        var rival = Rival() with
        {
            Party = [new PartyMember { Species = EntityId.Parse("species:absent"), Level = 5 }],
        };
        GameDb db = Db(rival);

        Assert.Equal(BattleStartRefusal.UnknownSpecies,
            BattleLauncher.Trainer(db, Session(db), rival, new Rng(2)).Refusal);
    }

    [Fact]
    public void Trainer_HonoursAuthoredMovesOverTheLearnset()
    {
        var rival = Rival() with
        {
            Party = [new PartyMember { Species = Wild, Level = 5, Moves = [MoveId] }],
        };
        GameDb db = Db(rival);
        BattleStart start = BattleLauncher.Trainer(db, Session(db), rival, new Rng(2));

        Assert.Single(start.Battle!.Party(BattleSide.Enemy)[0].Moves);
    }

    [Fact]
    public void Trainer_RejectsNullArguments()
    {
        Trainer rival = Rival();
        GameDb db = Db(rival);
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.Trainer(db, Session(db), null!, new Rng(1)));
    }

    // --- Returning to the overworld ------------------------------------------------

    [Fact]
    public void ApplyResult_WritesHpBackToTheParty()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));

        int before = session.Party[0].CurHp;
        BattleLauncher.ApplyResult(session, start.Battle!);
        Assert.Equal(start.Battle!.Party(BattleSide.Player)[0].CurrentHp, session.Party[0].CurHp);
        Assert.True(before > 0);
    }

    [Fact]
    public void ApplyResult_WritesPpBackToTheParty()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));

        BattleLauncher.ApplyResult(session, start.Battle!);
        Assert.Equal(start.Battle!.Party(BattleSide.Player)[0].Moves[0].Pp, session.Party[0].Moves[0].Pp);
    }

    [Fact]
    public void ApplyResult_RejectsNullArguments()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.ApplyResult(null!, null!));
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.ApplyResult(session, null!));
    }

    // --- Experience ---------------------------------------------------------------

    [Fact]
    public void AwardExperience_GivesNothingWhenNoOpponentFainted()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));

        long before = session.Party[0].Exp;
        BattleLauncher.AwardExperience(db, session, start.Battle!, trainer: false);
        Assert.Equal(before, session.Party[0].Exp);
    }

    [Fact]
    public void AwardExperience_GivesExpForADefeatedOpponent()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));
        Faint(start.Battle!.Party(BattleSide.Enemy)[0]);

        long before = session.Party[0].Exp;
        BattleLauncher.AwardExperience(db, session, start.Battle, trainer: false);
        Assert.True(session.Party[0].Exp > before, "the survivor should have gained exp");
    }

    /// <summary>A trainer battle yields more than a wild one at the same level, per Core's formula.</summary>
    [Fact]
    public void AwardExperience_TrainerBattlesYieldMore()
    {
        GameDb db = Db();

        long Gain(bool trainer)
        {
            WorldSession session = Session(db);
            BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));
            Faint(start.Battle!.Party(BattleSide.Enemy)[0]);
            long before = session.Party[0].Exp;
            BattleLauncher.AwardExperience(db, session, start.Battle, trainer);
            return session.Party[0].Exp - before;
        }

        Assert.True(Gain(trainer: true) > Gain(trainer: false));
    }

    /// <summary>A fainted party member earns nothing, matching the Core rule.</summary>
    [Fact]
    public void AwardExperience_SkipsFaintedPartyMembers()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));
        Faint(start.Battle!.Party(BattleSide.Enemy)[0]);

        session.Party[0] = session.Party[0] with { CurHp = 0 };
        long before = session.Party[0].Exp;
        BattleLauncher.AwardExperience(db, session, start.Battle, trainer: false);

        Assert.Equal(before, session.Party[0].Exp);
    }

    [Fact]
    public void AwardExperience_CanRaiseTheLevel()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        BattleStart start = BattleLauncher.Wild(db, session, Wild, 5, new Rng(2));
        Faint(start.Battle!.Party(BattleSide.Enemy)[0]);

        // Enough repeated awards must eventually cross a level threshold.
        int before = session.Party[0].Level;
        for (int i = 0; i < 50; i++)
            BattleLauncher.AwardExperience(db, session, start.Battle, trainer: true);

        Assert.True(session.Party[0].Level > before,
            $"level stayed at {before} after 50 awards");
    }

    [Fact]
    public void AwardExperience_RejectsNullArguments()
    {
        GameDb db = Db();
        WorldSession session = Session(db);
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.AwardExperience(null!, session, null!, false));
        Assert.Throws<ArgumentNullException>(() => BattleLauncher.AwardExperience(db, session, null!, false));
    }

    /// <summary>Drives a creature's HP to zero through Core's own damage path.</summary>
    private static void Faint(BattleCreature creature) => creature.TakeDamage(creature.MaxHp * 10);
}
