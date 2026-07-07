namespace Cgm.Core.Model;

public enum PersistentStatus { Burn, Poison, Toxic, Paralysis, Sleep, Freeze }

/// <summary>A known move on a creature instance + its remaining PP.</summary>
public readonly record struct MoveSlot(EntityId Move, int Pp);

/// <summary>A runtime/saved creature (DATA_SCHEMA §5) — distinct from the species definition.</summary>
public sealed record CreatureInstance
{
    public EntityId Species { get; init; }
    public string? Form { get; init; }
    public int Level { get; init; } = 1;
    public long Exp { get; init; }
    public Stats Ivs { get; init; }
    public Stats Evs { get; init; }
    public string Nature { get; init; } = "hardy";
    public string? Ability { get; init; }
    public int CurHp { get; init; }
    public PersistentStatus? Status { get; init; }
    public int StatusCounter { get; init; }
    public IReadOnlyList<MoveSlot> Moves { get; init; } = [];
    public int Happiness { get; init; } = 70;
    public EntityId? HeldItem { get; init; }
    public string? Nickname { get; init; }
    public string OtName { get; init; } = "";
    public EntityId? Ball { get; init; }
}

public readonly record struct BagEntry(EntityId Item, int Count);
public readonly record struct RespawnPoint(EntityId Map, GridPos Pos);

public sealed record DexRecord
{
    public IReadOnlyList<EntityId> Seen { get; init; } = [];
    public IReadOnlyList<EntityId> Caught { get; init; } = [];
}

/// <summary>A save game (DATA_SCHEMA §5). Versioned independently of project schema.</summary>
public sealed record SaveFile
{
    public int SaveFormatVersion { get; init; } = 1;
    public string GameContentHash { get; init; } = "";

    public EntityId? Map { get; init; }
    public GridPos Pos { get; init; }
    public Facing Facing { get; init; } = Facing.Down;

    public IReadOnlyList<CreatureInstance> Party { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<CreatureInstance>> Boxes { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<BagEntry>> Bag { get; init; } =
        new Dictionary<string, IReadOnlyList<BagEntry>>();

    public int Money { get; init; }
    public IReadOnlyDictionary<string, int> Flags { get; init; } = new Dictionary<string, int>();
    public RespawnPoint? Respawn { get; init; }
    public long ClockOffset { get; init; }
    public IReadOnlyDictionary<string, int> RngStates { get; init; } = new Dictionary<string, int>();
    public long PlaytimeSeconds { get; init; }
    public DexRecord Dex { get; init; } = new();
}
