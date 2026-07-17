namespace Cgm.Core.Model;

public enum AbilityHookPoint
{
    OnSwitchIn,
    OnModifyOutgoingDamage,
    OnModifyIncomingDamage,
    OnModifyStat,
    OnStatusAttempt,
    OnEndOfTurn,
    OnContactReceived,
    OnWeatherChange,
    OnTerrainChange,
    OnGroundedQuery,
    OnFaint,
}

public sealed record Ability : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<AbilityHook> Hooks { get; init; } = [];
}

public sealed record AbilityHook
{
    public AbilityHookPoint Hook { get; init; }
    public IReadOnlyList<Effect> Effects { get; init; } = [];
}
