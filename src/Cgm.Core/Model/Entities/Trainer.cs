namespace Cgm.Core.Model;

public enum AiProfile { Random, Basic, Smart }

/// <summary>A trainer (DATA_SCHEMA.md §4.13). `defeatedFlag` is derived, not stored.</summary>
public sealed record Trainer : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public string Class { get; init; } = "";
    public EntityId? BattleSprite { get; init; }
    public EntityId? OverworldSprite { get; init; }
    public int SightRange { get; init; }
    public AiProfile AiProfile { get; init; } = AiProfile.Basic;
    public int Money { get; init; }
    public IReadOnlyList<PartyMember> Party { get; init; } = [];
    public TrainerDialogue Dialogue { get; init; } = new();
}

public sealed record PartyMember
{
    public EntityId Species { get; init; }
    public int Level { get; init; } = 5;
    public IReadOnlyList<EntityId>? Moves { get; init; }
    public Stats? Ivs { get; init; }
    public string? Nature { get; init; }
    public EntityId? HeldItem { get; init; }
}

public sealed record TrainerDialogue
{
    public string Sight { get; init; } = "";
    public string Intro { get; init; } = "";
    public string Defeat { get; init; } = "";
    public string PostDefeat { get; init; } = "";
}
