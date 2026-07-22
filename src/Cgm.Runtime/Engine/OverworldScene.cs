using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>The walkable map scene (ENGINE_RUNTIME_SPEC 16D). It owns presentation state only:
/// every movement, collision, and ledge decision comes from Core's <see cref="GridMover"/> and
/// <see cref="MovementRules"/>, and the scene never recomputes a rule. One movement intent is queued
/// at a time, which is the resolved direction from the merged input devices.</summary>
public sealed class OverworldScene : IScene
{
    private readonly UiPainter _ui;
    private readonly Map _map;
    private readonly CollisionValue[] _collision;
    private readonly GridMover _mover;
    private readonly InputMerger _merger = new();
    private readonly FlagStore _flags;
    private readonly IRng _rng;
    private readonly IReadOnlyDictionary<EntityId, Trainer> _trainers;
    private readonly IReadOnlyDictionary<EntityId, EncounterTable> _tables;
    private readonly List<NpcActor> _npcs;
    private readonly TilePalette _palette;
    private readonly SpriteAtlas? _sprites;
    private readonly int _tileSize;
    private readonly int _width;
    private readonly int _height;
    private readonly WalkAnimator? _playerWalk;
    private readonly double _tickMs;
    private EntityId? _playerSprite;
    private Typewriter? _dialogue;

    public OverworldScene(UiPainter ui, Map map, IReadOnlyList<Tileset> tilesets, GridPos start,
        Facing facing, int tileSize, int virtualWidth, int virtualHeight,
        FlagStore? flags = null, IRng? rng = null,
        IReadOnlyDictionary<EntityId, Trainer>? trainers = null,
        IReadOnlyDictionary<EntityId, EncounterTable>? tables = null,
        SpriteAtlas? sprites = null, WalkAnimator? playerWalk = null,
        double tickMs = 1000.0 / Cgm.Core.Timing.FixedStepClock.DefaultTickRate)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tilesets);
        if (tileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Tile size must be positive.");

        _ui = ui;
        _map = map;
        _flags = flags ?? new FlagStore();
        _rng = rng ?? new Rng(1);
        _trainers = trainers ?? new Dictionary<EntityId, Trainer>();
        _tables = tables ?? new Dictionary<EntityId, EncounterTable>();
        _tileSize = tileSize;
        _width = virtualWidth;
        _height = virtualHeight;
        _sprites = sprites;
        _playerWalk = playerWalk;
        _tickMs = tickMs;
        // The same flattening MapCollision uses, so the tile a player walks through is the tile drawn.
        _palette = new TilePalette(tilesets);
        _collision = MapCollision.Derive(map, tilesets);
        _mover = new GridMover(start, facing, (from, dir) =>
            MovementRules.Resolve(from, dir, _collision, map.Width, map.Height, Occupied()));

        // Ordinal key order, not authored list order: NPCs must tick in the same sequence on every
        // run and after any map-file reordering, or a seeded replay diverges. This is what the
        // schema v8 stable keys are for.
        _npcs = map.Entities.OfType<NpcEntity>()
            .OrderBy(npc => npc.Key, StringComparer.Ordinal)
            .Select(npc => new NpcActor(npc, (from, dir) =>
                MovementRules.Resolve(from, dir, _collision, map.Width, map.Height, OccupiedExcept(npc))))
            .ToList();
    }

    public bool IsOverlay => false;

    public GridPos PlayerPos => _mover.Position;

    public Facing PlayerFacing => _mover.Facing;

    public MoverState PlayerState => _mover.State;

    /// <summary>Camera position in world pixels, clamped to the map by Core's rule.</summary>
    public (int X, int Y) Camera { get; private set; }

    /// <summary>What Confirm would interact with: the interactable entity one tile ahead, or null.
    /// Resolved by Core so the scene applies no priority rule of its own.</summary>
    public MapEntity? Facing => Interaction.InFront(_mover.Position, _mover.Facing, _map.Entities);

    /// <summary>An outcome the host must act on — warp, trainer battle, wild encounter, or an action
    /// needing a Core operation. The scene never performs these itself.</summary>
    public StepOutcome? Pending { get; private set; }

    /// <summary>True while a dialogue page is showing; movement is suspended.</summary>
    public bool InDialogue => _dialogue is { Finished: false };

    /// <summary>Set when the player pressed Menu; the host pushes the overlay and clears it.</summary>
    public bool MenuRequested { get; private set; }

    public bool TakeMenuRequest()
    {
        bool requested = MenuRequested;
        MenuRequested = false;
        return requested;
    }

    /// <summary>Save flags this scene reads and writes; owned by the session, borrowed here.</summary>
    public FlagStore Flags => _flags;

    /// <summary>Live NPC actors in the ordinal key order they tick in.</summary>
    public IReadOnlyList<NpcActor> Npcs => _npcs;

    public StepOutcome? TakePending()
    {
        StepOutcome? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Enter() => UpdateCamera();

    public void Update(TickInput input)
    {
        if (_dialogue is { Finished: false } page)
        {
            // Dialogue owns input while it runs; movement resumes when the last page is dismissed.
            page.Tick();
            if (input.WasPressed(GameAction.Confirm))
                page.Confirm();
            if (page.Finished)
                _dialogue = null;
            return;
        }

        // Devices merge by action and the resolver picks one direction; the scene queues no more
        // than that single intent per tick.
        _merger.Observe(InputDevice.Keyboard, input.Held);
        GridPos before = _mover.Position;
        _mover.Tick(Direction(_merger.Direction()));
        UpdateCamera();

        // Advance the walk cycle by one tick, in sync with the sim, so the sprite and the movement
        // interpolation never drift apart.
        _playerSprite = _playerWalk?.Advance(_mover.Facing, _mover.State == MoverState.Moving, _tickMs);

        // NPCs tick after the player, in ordinal key order, sharing the session RNG stream.
        foreach (NpcActor npc in _npcs)
            npc.Tick(_rng);

        // The menu is an overlay the host pushes; the scene only reports the request.
        if (input.WasPressed(GameAction.Menu))
        {
            MenuRequested = true;
            return;
        }

        if (_mover.Position != before)
            Apply(OverworldStep.Resolve(_mover.Position, _map, _collision, _flags,
                _trainers, _tables, _rng));
        else if (input.WasPressed(GameAction.Confirm))
            Apply(OverworldStep.Interact(_mover.Position, _mover.Facing, _map));
    }

    /// <summary>Presents a Core outcome. Dialogue and flags are handled here because they are pure
    /// presentation and save state; warps, battles, and encounters are surfaced as
    /// <see cref="Pending"/> for the host, which owns scene transitions.</summary>
    private void Apply(StepOutcome outcome)
    {
        switch (outcome)
        {
            case StepOutcome.None:
                return;

            case StepOutcome.Trigger trigger:
                Execute(trigger.Entity.Actions);
                return;

            case StepOutcome.Interact { Entity: TriggerEntity entity }:
                Execute(entity.Actions);
                return;

            case StepOutcome.Interact { Entity: SignEntity sign }:
                Say(sign.Text);
                return;

            case StepOutcome.Interact { Entity: NpcEntity { Dialogue: { } text } }:
                Say(text);
                return;

            // An already-collected pickup is inert: its flag is the record of collection, so it
            // cannot be taken twice even though the entity is still authored on the map.
            case StepOutcome.Interact { Entity: PickupEntity pickup } when Collected(pickup):
                return;

            default:
                // Warp, TrainerSpotted, WildEncounter, and anything else needing a scene change.
                Pending = outcome;
                return;
        }
    }

    /// <summary>Runs the closed action vocabulary. Validation guarantees each action is complete, so
    /// there is nothing to interpret or repair here — only a dispatch on the op.</summary>
    private void Execute(IReadOnlyList<TriggerAction> actions)
    {
        foreach (TriggerAction action in actions)
        {
            switch (action.Op)
            {
                case TriggerOp.Dialogue:
                    Say(action.Text ?? "");
                    break;

                case TriggerOp.SetFlag:
                    _flags.SetInt(action.Flag!, action.Value);
                    break;

                case TriggerOp.ClearFlag:
                    _flags.SetInt(action.Flag!, 0);
                    break;

                default:
                    // giveItem, heal, and startBattle need Core operations the host owns.
                    Pending = new StepOutcome.Trigger(new TriggerEntity { Key = "", Actions = [action] });
                    return;
            }
        }
    }

    /// <summary>A pickup's flag is the record that it was taken; an empty flag means it is repeatable
    /// by authoring choice.</summary>
    public bool Collected(PickupEntity pickup)
    {
        ArgumentNullException.ThrowIfNull(pickup);
        return !string.IsNullOrWhiteSpace(pickup.Flag) && _flags.GetBool(pickup.Flag);
    }

    private void Say(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        _dialogue = new Typewriter([_ui.Font.Wrap(text, _width - 24)]);
    }

    public void Render()
    {
        _ui.Panel(new RectI(0, 0, _width, _height), new Rgba(0x0A, 0x0C, 0x10, 0xFF));
        DrawTiles();
        DrawEntities();
        DrawPlayer();
        DrawDialogue();
    }

    private void DrawEntities()
    {
        foreach (PickupEntity pickup in _map.Entities.OfType<PickupEntity>())
            if (!Collected(pickup))   // a taken pickup leaves nothing to draw
                _ui.Panel(Inset(TileRect(pickup.Pos.X, pickup.Pos.Y), 5),
                    new Rgba(0xE0, 0xD0, 0x60, 0xFF), layer: 1);

        foreach (SignEntity sign in _map.Entities.OfType<SignEntity>())
            _ui.Panel(Inset(TileRect(sign.Pos.X, sign.Pos.Y), 4),
                new Rgba(0x90, 0x70, 0x40, 0xFF), layer: 1);

        foreach (NpcActor npc in _npcs)
        {
            (int dx, int dy) = npc.State == MoverState.Moving ? Offset(npc.Facing) : (0, 0);
            int progress = (int)(npc.Progress * _tileSize);
            RectI tile = Inset(TileRect(npc.Position.X, npc.Position.Y), 2);
            _ui.Panel(tile with { X = tile.X + dx * progress, Y = tile.Y + dy * progress },
                new Rgba(0x70, 0xA0, 0xE0, 0xFF), layer: 1);
        }
    }

    private static RectI Inset(RectI rect, int by) =>
        new(rect.X + by, rect.Y + by, rect.Width - by * 2, rect.Height - by * 2);

    private void DrawDialogue()
    {
        if (_dialogue is not { } page)
            return;
        var box = new RectI(8, _height - 56, _width - 16, 48);
        _ui.Panel(box, new Rgba(0x18, 0x1C, 0x28, 0xF0), layer: 10);
        _ui.TextBlock(page.VisibleLines(), box.X + 8, box.Y + 8,
            new Rgba(0xF0, 0xEC, 0xD8, 0xFF), layer: 11);
    }

    public void Exit() { }

    public void Dispose() { }

    private void DrawTiles()
    {
        int firstX = Math.Max(0, Camera.X / _tileSize);
        int firstY = Math.Max(0, Camera.Y / _tileSize);
        int lastX = Math.Min(_map.Width - 1, (Camera.X + _width) / _tileSize);
        int lastY = Math.Min(_map.Height - 1, (Camera.Y + _height) / _tileSize);

        for (int y = firstY; y <= lastY; y++)
            for (int x = firstX; x <= lastX; x++)
                DrawCell(x, y);
    }

    /// <summary>Draws one cell's three layers. Ground and decoBelow sit under the player (layer 0),
    /// decoAbove over it (layer 2) so tree canopies and roof edges occlude correctly. A cell with no
    /// drawable sprite falls back to its collision colour, which keeps an unarted map playable and
    /// legible instead of blank.</summary>
    private void DrawCell(int x, int y)
    {
        int cell = y * _map.Width + x;
        RectI dest = TileRect(x, y);

        bool drewUnder = DrawTile(_map.Layers.Ground, cell, dest, 0);
        drewUnder |= DrawTile(_map.Layers.DecoBelow, cell, dest, 0);
        if (!drewUnder)
            _ui.Panel(dest, Colour(_collision[cell]));

        DrawTile(_map.Layers.DecoAbove, cell, dest, 2);
    }

    /// <summary>Draws one layer's tile at a cell, reporting whether anything was actually drawn. An
    /// empty cell (-1), an unknown index, a tile with no sprite, and a sprite whose sheet failed to
    /// load are all "nothing drawn" — none of them should fail the frame.</summary>
    private bool DrawTile(IReadOnlyList<int> indices, int cell, RectI dest, int layer)
    {
        if (_sprites is null || cell >= indices.Count)
            return false;
        if (_palette.At(indices[cell]) is not { Sprite: { } sprite })
            return false;
        if (!_sprites.TryGet(sprite, out TextureHandle texture, out RectI source))
            return false;

        _ui.Sprite(texture, source, dest, layer);
        return true;
    }

    private void DrawPlayer()
    {
        // Interpolate along the facing between tiles; presentation only, never written back.
        (int dx, int dy) = _mover.State == MoverState.Moving ? Offset(_mover.Facing) : (0, 0);
        int progress = (int)(_mover.Progress * _tileSize);
        RectI tile = TileRect(_mover.Position.X, _mover.Position.Y);
        int ox = dx * progress, oy = dy * progress;

        // Character sprites are taller than a tile, so they anchor by the feet: bottom-aligned to the
        // tile and horizontally centred, with the top overhanging upward. Layer 1 keeps the player
        // under decoAbove canopies (layer 2) and over the ground (layer 0).
        if (_sprites is not null && _playerSprite is { } sprite
            && _sprites.TryGet(sprite, out TextureHandle texture, out RectI source))
        {
            var dest = new RectI(
                tile.X + (_tileSize - source.Width) / 2 + ox,
                tile.Y + _tileSize - source.Height + oy,
                source.Width, source.Height);
            _ui.Sprite(texture, source, dest, layer: 1);
            return;
        }

        // No art (or none resolved): the flat marker keeps an unarted map playable.
        _ui.Panel(tile with { X = tile.X + ox, Y = tile.Y + oy },
            new Rgba(0xF0, 0xC0, 0x40, 0xFF), layer: 1);
    }

    private RectI TileRect(int x, int y) =>
        new(x * _tileSize - Camera.X, y * _tileSize - Camera.Y, _tileSize, _tileSize);

    /// <summary>NPCs occupy their live tile, so Core treats them as blocking. Positions come from
    /// the actors, not the authored entities, so a moving NPC blocks where it actually is.</summary>
    private IReadOnlySet<GridPos> Occupied() => _npcs.Select(npc => npc.Position).ToHashSet();

    /// <summary>Occupancy from one NPC's point of view: it does not block itself, but the player and
    /// every other NPC do.</summary>
    private IReadOnlySet<GridPos> OccupiedExcept(NpcEntity self)
    {
        var cells = _npcs.Where(actor => actor.Entity != self)
            .Select(actor => actor.Position)
            .ToHashSet();
        cells.Add(_mover.Position);
        return cells;
    }

    private void UpdateCamera() => Camera = Engine.Camera.Clamp(
        _mover.Position.X * _tileSize + _tileSize / 2,
        _mover.Position.Y * _tileSize + _tileSize / 2,
        _width, _height, _map.Width * _tileSize, _map.Height * _tileSize);

    private static Facing? Direction(GameAction? action) => action switch
    {
        GameAction.Up => Cgm.Core.Model.Facing.Up,
        GameAction.Down => Cgm.Core.Model.Facing.Down,
        GameAction.Left => Cgm.Core.Model.Facing.Left,
        GameAction.Right => Cgm.Core.Model.Facing.Right,
        _ => null,
    };

    private static (int X, int Y) Offset(Facing facing) => facing switch
    {
        Cgm.Core.Model.Facing.Up => (0, -1),
        Cgm.Core.Model.Facing.Down => (0, 1),
        Cgm.Core.Model.Facing.Left => (-1, 0),
        _ => (1, 0),
    };

    private static Rgba Colour(CollisionValue value) => value switch
    {
        CollisionValue.Solid => new Rgba(0x38, 0x30, 0x2C, 0xFF),
        CollisionValue.Open => new Rgba(0x2C, 0x50, 0x38, 0xFF),
        _ => new Rgba(0x50, 0x48, 0x2C, 0xFF), // ledges
    };
}
