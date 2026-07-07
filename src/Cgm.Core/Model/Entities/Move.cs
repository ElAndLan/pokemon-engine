using System.Text.Json;

namespace Cgm.Core.Model;

public enum DamageClass { Physical, Special, Status }

/// <summary>MVP uses Selected/User (1v1); wider targeting lands with doubles (long-term).</summary>
public enum MoveTarget { Selected, User, AllOpponents, AllOtherPokemon, UsersField, EntireField }

/// <summary>A move (DATA_SCHEMA.md §4.4). Effects are the closed op palette (BATTLE_SYSTEM_SPEC).</summary>
public sealed record Move : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public EntityId Type { get; init; }
    public DamageClass DamageClass { get; init; } = DamageClass.Status;
    public int? Power { get; init; }
    public int? Accuracy { get; init; }
    public int Pp { get; init; }
    public int Priority { get; init; }
    public int CritStage { get; init; }
    public MoveTarget Target { get; init; } = MoveTarget.Selected;
    public IReadOnlyList<Effect> Effects { get; init; } = [];
}

/// <summary>
/// One effect-op invocation: <c>{ op, chance?, params }</c>. The op catalog and param shapes are
/// defined in BATTLE_SYSTEM_SPEC.md; here <see cref="Params"/> is an untyped bag that round-trips
/// faithfully — the battle engine interprets it (Phase 8+). Shared by moves and items.
/// </summary>
public sealed record Effect
{
    public string Op { get; init; } = "";
    public int? Chance { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }
}
