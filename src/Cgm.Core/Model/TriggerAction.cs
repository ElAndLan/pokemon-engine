namespace Cgm.Core.Model;

/// <summary>The closed world-interaction vocabulary (DATA_SCHEMA §4.11b, ENGINE_RUNTIME_SPEC 16D
/// prerequisite). Runtime dispatches on this enum and never on an arbitrary string, so authored data
/// can never become a script the engine interprets.</summary>
public enum TriggerOp
{
    /// <summary>Show a dialogue page.</summary>
    Dialogue,

    /// <summary>Set a save flag to <see cref="TriggerAction.Value"/>.</summary>
    SetFlag,

    /// <summary>Reset a save flag to zero.</summary>
    ClearFlag,

    /// <summary>Give <see cref="TriggerAction.Value"/> of an item, through Core inventory rules.</summary>
    GiveItem,

    /// <summary>Restore the party, as a healing service does.</summary>
    Heal,

    /// <summary>Request a trainer battle, through the Core battle boundary.</summary>
    StartBattle,
}

/// <summary>One authored world action. Every op reads a fixed subset of the fields; validation
/// enforces which, so an incomplete action is rejected at author time rather than at play time.
/// Adding an op means adding an enum member and its validation, never a new string convention.</summary>
public sealed record TriggerAction
{
    public TriggerOp Op { get; init; }

    /// <summary>Dialogue text. Display strings are data, never IDs.</summary>
    public string? Text { get; init; }

    /// <summary>Save-flag name for <see cref="TriggerOp.SetFlag"/> and <see cref="TriggerOp.ClearFlag"/>.</summary>
    public string? Flag { get; init; }

    /// <summary>Flag value to set, or item quantity to give. Defaults to one.</summary>
    public int Value { get; init; } = 1;

    /// <summary>Referenced entity: an <c>item:</c> for GiveItem, a <c>trainer:</c> for StartBattle.</summary>
    public EntityId? Entity { get; init; }
}
