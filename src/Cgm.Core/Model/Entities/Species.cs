namespace Cgm.Core.Model;

/// <summary>The six battle stats (DATA_SCHEMA.md §4.3). Reused for base stats and EV yields.</summary>
public readonly record struct Stats(int Hp, int Atk, int Def, int Spa, int Spd, int Spe);

public enum EvolutionTrigger { LevelUp, UseItem, Trade, Other }
public enum TimeOfDay { Day, Night }
public enum Gender { Male, Female }

/// <summary>A creature species: merge of PokeAPI pokemon + pokemon-species (DATA_SCHEMA.md §4.3).</summary>
public sealed record Species : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public IReadOnlyList<EntityId> Types { get; init; } = [];
    public Stats BaseStats { get; init; }
    public Stats EvYield { get; init; }
    public int BaseExp { get; init; }
    public string GrowthRate { get; init; } = "medium-fast";
    public int CatchRate { get; init; } = 45;
    public int BaseHappiness { get; init; } = 70;
    public int GenderFemaleEighths { get; init; } = 4; // -1 = genderless
    public int EggCycles { get; init; } = 20;
    public IReadOnlyList<string> EggGroups { get; init; } = [];

    public IReadOnlyList<LearnsetEntry> Learnset { get; init; } = [];
    public IReadOnlyList<Evolution> Evolutions { get; init; } = [];
    public IReadOnlyList<Form> Forms { get; init; } = []; // empty in v1 (Phase 15)

    public SpeciesSprites Sprites { get; init; } = new();
    public SpeciesSpriteUrls? SpriteUrls { get; init; } // import-staging (ADR-010)
    public string? Cry { get; init; }
}

public readonly record struct LearnsetEntry(int Level, EntityId Move);

public sealed record Evolution
{
    public EntityId Target { get; init; }
    public EvolutionTrigger Trigger { get; init; } = EvolutionTrigger.LevelUp;
    public int? MinLevel { get; init; }
    public EntityId? Item { get; init; }
    public EntityId? HeldItem { get; init; }
    public EntityId? KnownMove { get; init; }
    public int? MinHappiness { get; init; }
    public TimeOfDay? TimeOfDay { get; init; }
    public string? Location { get; init; }
    public Gender? Gender { get; init; }
}

/// <summary>Alternate form placeholder — the only sanctioned v1 stub (SCOPE_GUARD); filled in Phase 15.</summary>
public sealed record Form
{
    public string FormId { get; init; } = "";
}

public sealed record SpeciesSprites
{
    public EntityId? Front { get; init; }
    public EntityId? Back { get; init; }
    public EntityId? Icon { get; init; }
}

public sealed record SpeciesSpriteUrls
{
    public string? FrontDefault { get; init; }
    public string? BackDefault { get; init; }
    public string? FrontShiny { get; init; }
    public string? BackShiny { get; init; }
    public string? OfficialArtwork { get; init; }
}
