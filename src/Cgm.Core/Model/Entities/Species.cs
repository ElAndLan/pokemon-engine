namespace Cgm.Core.Model;

/// <summary>The six battle stats (DATA_SCHEMA.md §4.3). Reused for base stats and EV yields.</summary>
public readonly record struct Stats(int Hp, int Atk, int Def, int Spa, int Spd, int Spe);

public enum EvolutionTrigger { LevelUp, UseItem, Trade, Other }
public enum TimeOfDay { Day, Night }
public enum Gender { Male, Female }
public enum FormActivation { Permanent, BattleTemporary, BattleTimed, Condition }

/// <summary>A creature species: merge of PokeAPI pokemon + pokemon-species (DATA_SCHEMA.md §4.3).</summary>
public sealed record Species : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public IReadOnlyList<EntityId> Types { get; init; } = [];
    public Stats BaseStats { get; init; }
    public int WeightHectograms { get; init; } = 1;
    public int HeightDecimeters { get; init; } = 1;
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
    public IReadOnlyList<EntityId> Abilities { get; init; } = [];
    public EntityId? HiddenAbility { get; init; }
    public IReadOnlyList<Form> Forms { get; init; } = [];

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
    public FormActivation Activation { get; init; } = FormActivation.Permanent;
    public Stats? StatOverrides { get; init; }
    public IReadOnlyList<EntityId>? TypeOverrides { get; init; }
    public EntityId? AbilityOverride { get; init; }
    public SpeciesSprites Sprites { get; init; } = new();
    public EntityId? RequiredHeldItem { get; init; }
    public EntityId? RequiredTrainerItem { get; init; }
    public int? Turns { get; init; }
    public int? HpMultiplierPercent { get; init; }
    public IReadOnlyDictionary<EntityId, EntityId>? MoveRemap { get; init; }
    public FormCondition? Condition { get; init; }
}

public sealed record FormCondition
{
    public string? Weather { get; init; }
    public EntityId? HeldItem { get; init; }
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
