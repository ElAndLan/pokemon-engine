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
    private readonly int _tileSize;
    private readonly int _width;
    private readonly int _height;
    private Typewriter? _dialogue;

    public OverworldScene(UiPainter ui, Map map, IReadOnlyList<Tileset> tilesets, GridPos start,
        Facing facing, int tileSize, int virtualWidth, int virtualHeight,
        FlagStore? flags = null, IRng? rng = null,
        IReadOnlyDictionary<EntityId, Trainer>? trainers = null,
        IReadOnlyDictionary<EntityId, EncounterTable>? tables = null)
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
        _collision = MapCollision.Derive(map, tilesets);
        // NPCs block movement. They are static until the 16D NPC tick lands, so this is recomputed
        // per resolve rather than cached — correctness first, and the set is map-sized at worst.
        _mover = new GridMover(start, facing, (from, dir) =>
            MovementRules.Resolve(from, dir, _collision, map.Width, map.Height, Occupied()));
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

    /// <summary>Save flags this scene reads and writes; owned by the session, borrowed here.</summary>
    public FlagStore Flags => _flags;

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
        DrawPlayer();
        DrawDialogue();
    }

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
        // ponytail: collision-derived flat colours until IAssetSource can supply a tileset atlas.
        // The chunk walk and camera maths are the same either way, so swapping in real tiles is a
        // texture change, not a rewrite.
        int firstX = Math.Max(0, Camera.X / _tileSize);
        int firstY = Math.Max(0, Camera.Y / _tileSize);
        int lastX = Math.Min(_map.Width - 1, (Camera.X + _width) / _tileSize);
        int lastY = Math.Min(_map.Height - 1, (Camera.Y + _height) / _tileSize);

        for (int y = firstY; y <= lastY; y++)
            for (int x = firstX; x <= lastX; x++)
                _ui.Panel(TileRect(x, y), Colour(_collision[y * _map.Width + x]));
    }

    private void DrawPlayer()
    {
        // Interpolate along the facing between tiles; presentation only, never written back.
        (int dx, int dy) = _mover.State == MoverState.Moving ? Offset(_mover.Facing) : (0, 0);
        int progress = (int)(_mover.Progress * _tileSize);
        RectI tile = TileRect(_mover.Position.X, _mover.Position.Y);
        _ui.Panel(tile with { X = tile.X + dx * progress, Y = tile.Y + dy * progress },
            new Rgba(0xF0, 0xC0, 0x40, 0xFF), layer: 1);
    }

    private RectI TileRect(int x, int y) =>
        new(x * _tileSize - Camera.X, y * _tileSize - Camera.Y, _tileSize, _tileSize);

    /// <summary>NPCs occupy their tile, so Core treats them as blocking.</summary>
    private IReadOnlySet<GridPos> Occupied() =>
        _map.Entities.OfType<NpcEntity>().Select(npc => npc.Pos).ToHashSet();

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
