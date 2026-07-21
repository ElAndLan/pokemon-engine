using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16E party state: New Game builds the starter party through Core, the
/// party survives a save round trip, and overflow routes to storage by Core's rule.</summary>
public sealed class PartyStateTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapA = EntityId.Parse("map:town");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId SpeciesA = EntityId.Parse("species:pebbling");
    private static readonly EntityId SpeciesB = EntityId.Parse("species:sprig");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public PartyStateTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Species Species(EntityId id) => new()
    {
        Id = id,
        Name = id.Slug,
        Types = [TypeId],
        BaseStats = new Stats(45, 45, 45, 45, 45, 45),
        GrowthRate = "medium-fast",
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    private WorldSession Session(IEnumerable<EntityId>? starters = null, int boxCount = 2,
        int boxCapacity = 3, IRng? rng = null)
    {
        var map = new Map
        {
            Id = MapA, Name = "Town", Width = 8, Height = 8,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
        };
        var settings = new ProjectSettings
        {
            Name = "T",
            TileSize = Tile,
            StarterParty = starters?.ToList() ?? [SpeciesA],
            Boxes = new BoxConfig { Count = boxCount, Capacity = boxCapacity },
        };
        var db = new GameDb(settings,
        [
            map,
            Species(SpeciesA),
            Species(SpeciesB),
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move { Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35 },
        ]);
        return new WorldSession(db, _ui, Tile, 256, 192, rng ?? new Rng(1));
    }

    private static CreatureInstance Creature(int hp = 10) => new()
    {
        Species = SpeciesA, Level = 5, CurHp = hp, Moves = [new MoveSlot(MoveId, 35)],
    };

    // --- New Game -----------------------------------------------------------------

    [Fact]
    public void NewGame_BuildsThePartyFromTheStarterList()
    {
        WorldSession session = Session([SpeciesA, SpeciesB]);
        session.InitialiseNewGame();

        Assert.Equal(2, session.Party.Count);
        Assert.Equal(SpeciesA, session.Party[0].Species);
        Assert.Equal(SpeciesB, session.Party[1].Species);
    }

    [Fact]
    public void NewGame_GivesMovesFromTheLearnset()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();

        MoveSlot move = Assert.Single(session.Party[0].Moves);
        Assert.Equal(MoveId, move.Move);
        Assert.Equal(35, move.Pp);   // PP from the move definition, not invented
    }

    [Fact]
    public void NewGame_StartsMembersAtFullHealth()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();
        Assert.True(session.Party[0].CurHp > 0);
    }

    [Fact]
    public void NewGame_HonoursTheRequestedLevel()
    {
        WorldSession session = Session();
        session.InitialiseNewGame(starterLevel: 12);
        Assert.Equal(12, session.Party[0].Level);
    }

    /// <summary>Same seed, same starters: New Game is reproducible, which a seeded replay needs.</summary>
    [Fact]
    public void NewGame_IsDeterministicForASeed()
    {
        static (Stats Ivs, string Nature) Run()
        {
            WorldSession session = new PartyStateTests().Session(rng: new Rng(42));
            session.InitialiseNewGame();
            return (session.Party[0].Ivs, session.Party[0].Nature);
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void NewGame_NeverExceedsThePartyMaximum()
    {
        WorldSession session = Session(Enumerable.Repeat(SpeciesA, 10));
        session.InitialiseNewGame();
        Assert.Equal(PartyStorage.MaxParty, session.Party.Count);
    }

    /// <summary>A missing starter species is a validation error; New Game skips it rather than
    /// substituting a different creature.</summary>
    [Fact]
    public void NewGame_SkipsAMissingStarterRatherThanSubstituting()
    {
        WorldSession session = Session([EntityId.Parse("species:absent"), SpeciesB]);
        session.InitialiseNewGame();

        CreatureInstance only = Assert.Single(session.Party);
        Assert.Equal(SpeciesB, only.Species);
    }

    [Fact]
    public void NewGame_WithNoStarters_LeavesAnEmptyParty()
    {
        WorldSession session = Session([]);
        session.InitialiseNewGame();
        Assert.Empty(session.Party);
    }

    [Fact]
    public void NewGame_ClearsAnyPreviousPartyAndBoxes()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();
        session.Deposit(Creature());
        session.Boxes[0].Add(Creature());

        session.InitialiseNewGame();
        Assert.Single(session.Party);
        Assert.Empty(session.Boxes[0]);
    }

    // --- Deposit routing ----------------------------------------------------------

    [Fact]
    public void Deposit_FillsThePartyFirst()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();

        DepositResult result = session.Deposit(Creature())!.Value;
        Assert.Equal(DepositTarget.Party, result.Target);
        Assert.Equal(2, session.Party.Count);
    }

    [Fact]
    public void Deposit_OverflowsToTheFirstBoxWithRoom()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();
        while (session.Party.Count < PartyStorage.MaxParty)
            session.Deposit(Creature());

        DepositResult result = session.Deposit(Creature())!.Value;
        Assert.Equal(DepositTarget.Box, result.Target);
        Assert.Equal(0, result.BoxIndex);
        Assert.Single(session.Boxes[0]);
    }

    [Fact]
    public void Deposit_MovesToTheNextBoxWhenOneIsFull()
    {
        WorldSession session = Session(boxCount: 2, boxCapacity: 1);
        session.InitialiseNewGame();
        while (session.Party.Count < PartyStorage.MaxParty)
            session.Deposit(Creature());

        session.Deposit(Creature());                       // fills box 0
        DepositResult result = session.Deposit(Creature())!.Value;
        Assert.Equal(1, result.BoxIndex);
    }

    [Fact]
    public void Deposit_ReturnsNullWhenEverythingIsFull()
    {
        WorldSession session = Session(boxCount: 1, boxCapacity: 1);
        session.InitialiseNewGame();
        while (session.Party.Count < PartyStorage.MaxParty)
            session.Deposit(Creature());
        session.Deposit(Creature());                       // fills the only box

        Assert.Null(session.Deposit(Creature()));
    }

    [Fact]
    public void Deposit_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => Session().Deposit(null!));

    [Fact]
    public void BoxesAreSizedFromProjectSettings() =>
        Assert.Equal(4, Session(boxCount: 4).Boxes.Count);

    // --- Blackout condition -------------------------------------------------------

    [Fact]
    public void AHealthyParty_IsNotWhitedOut()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();
        Assert.False(session.PartyIsWhitedOut);
    }

    [Fact]
    public void AnAllFaintedParty_IsWhitedOut()
    {
        WorldSession session = Session();
        session.Party.Add(Creature(hp: 0));
        session.Party.Add(Creature(hp: 0));
        Assert.True(session.PartyIsWhitedOut);
    }

    [Fact]
    public void OneSurvivor_IsNotWhitedOut()
    {
        WorldSession session = Session();
        session.Party.Add(Creature(hp: 0));
        session.Party.Add(Creature(hp: 3));
        Assert.False(session.PartyIsWhitedOut);
    }

    /// <summary>An empty party is not a blackout: that is a New Game that has not started yet.</summary>
    [Fact]
    public void AnEmptyParty_IsNotWhitedOut() => Assert.False(Session().PartyIsWhitedOut);

    // --- Persistence --------------------------------------------------------------

    [Fact]
    public void ToSave_CapturesPartyAndBoxes()
    {
        WorldSession session = Session([SpeciesA, SpeciesB]);
        session.InitialiseNewGame();
        session.Boxes[0].Add(Creature());

        SaveFile save = session.ToSave("h");
        Assert.Equal(2, save.Party.Count);
        Assert.Single(save.Boxes[0]);
    }

    [Fact]
    public void SaveThenRestore_PreservesTheParty()
    {
        WorldSession first = Session([SpeciesA, SpeciesB]);
        first.InitialiseNewGame();
        first.Enter(MapA, new GridPos(1, 1), Facing.Down);
        SaveFile save = first.ToSave("h");

        WorldSession second = Session([SpeciesA, SpeciesB]);
        second.Restore(save);

        Assert.Equal(first.Party.Count, second.Party.Count);
        Assert.Equal(first.Party[0].Species, second.Party[0].Species);
        Assert.Equal(first.Party[0].Ivs, second.Party[0].Ivs);
        Assert.Equal(first.Party[0].Nature, second.Party[0].Nature);
    }

    [Fact]
    public void SaveThenRestore_PreservesBoxContents()
    {
        WorldSession first = Session();
        first.InitialiseNewGame();
        first.Boxes[1].Add(Creature(hp: 7));
        first.Enter(MapA, default, Facing.Down);

        WorldSession second = Session();
        second.Restore(first.ToSave("h"));

        Assert.Equal(7, second.Boxes[1][0].CurHp);
    }

    /// <summary>Restore replaces rather than merges, or a released creature would come back.</summary>
    [Fact]
    public void Restore_ReplacesAnExistingPartyRatherThanAppending()
    {
        WorldSession session = Session();
        session.InitialiseNewGame();
        session.Deposit(Creature());
        Assert.Equal(2, session.Party.Count);

        session.Restore(new SaveFile { Map = MapA, Party = [Creature()] });
        Assert.Single(session.Party);
    }

    [Fact]
    public void Restore_ClearsBoxesThatTheSaveDoesNotFill()
    {
        WorldSession session = Session();
        session.Boxes[0].Add(Creature());
        session.Restore(new SaveFile { Map = MapA });
        Assert.Empty(session.Boxes[0]);
    }

    /// <summary>A save holding more than six party members is clamped rather than trusted.</summary>
    [Fact]
    public void Restore_ClampsAnOversizedPartyFromAHandEditedSave()
    {
        WorldSession session = Session();
        session.Restore(new SaveFile
        {
            Map = MapA,
            Party = Enumerable.Repeat(Creature(), 10).ToList(),
        });

        Assert.Equal(PartyStorage.MaxParty, session.Party.Count);
    }

    /// <summary>More saved boxes than the project defines must not throw.</summary>
    [Fact]
    public void Restore_IgnoresExtraBoxesBeyondTheProjectConfiguration()
    {
        WorldSession session = Session(boxCount: 1);
        session.Restore(new SaveFile
        {
            Map = MapA,
            Boxes = [[Creature()], [Creature()], [Creature()]],
        });

        Assert.Single(session.Boxes);
        Assert.Single(session.Boxes[0]);
    }
}
