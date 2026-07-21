using System.Collections.ObjectModel;

namespace Cgm.Runtime.Engine;

public enum InputDevice { Keyboard, Gamepad }

/// <summary>Per-device action bindings (ENGINE_RUNTIME_SPEC 16C). Inputs are stable platform-adapter
/// names, never numeric device codes, so a saved profile survives driver reordering.</summary>
public sealed class InputBindings
{
    /// <summary>Confirm and Cancel must always keep one of these, so a bad rebind can never lock the
    /// player out of the menu that would fix it.</summary>
    private static readonly IReadOnlyDictionary<GameAction, string[]> KeyboardDefaults =
        new Dictionary<GameAction, string[]>
        {
            [GameAction.Up] = ["Up", "W"],
            [GameAction.Down] = ["Down", "S"],
            [GameAction.Left] = ["Left", "A"],
            [GameAction.Right] = ["Right", "D"],
            [GameAction.Confirm] = ["Enter", "Z"],
            [GameAction.Cancel] = ["Escape", "X"],
            [GameAction.Menu] = ["C"],
            [GameAction.Run] = ["ShiftLeft"],
            [GameAction.DebugToggle] = ["F3"],
        };

    private static readonly IReadOnlyDictionary<GameAction, string[]> GamepadDefaults =
        new Dictionary<GameAction, string[]>
        {
            [GameAction.Up] = ["DPadUp", "LeftStickUp"],
            [GameAction.Down] = ["DPadDown", "LeftStickDown"],
            [GameAction.Left] = ["DPadLeft", "LeftStickLeft"],
            [GameAction.Right] = ["DPadRight", "LeftStickRight"],
            [GameAction.Confirm] = ["FaceSouth"],
            [GameAction.Cancel] = ["FaceEast"],
            [GameAction.Menu] = ["Start"],
            [GameAction.Run] = ["FaceWest"],
            [GameAction.DebugToggle] = ["Back"],
        };

    /// <summary>Left-stick displacement past this magnitude reads as a direction press.</summary>
    public const double StickDeadzone = 0.5;

    private readonly Dictionary<InputDevice, Dictionary<GameAction, List<string>>> _bindings;

    private InputBindings(Dictionary<InputDevice, Dictionary<GameAction, List<string>>> bindings) =>
        _bindings = bindings;

    public static InputBindings Defaults() => new(new()
    {
        [InputDevice.Keyboard] = Clone(KeyboardDefaults),
        [InputDevice.Gamepad] = Clone(GamepadDefaults),
    });

    public static IReadOnlyList<string> DefaultsFor(InputDevice device, GameAction action) =>
        (device == InputDevice.Keyboard ? KeyboardDefaults : GamepadDefaults)
            .GetValueOrDefault(action, []);

    public IReadOnlyList<string> For(InputDevice device, GameAction action) =>
        _bindings[device].TryGetValue(action, out List<string>? inputs) ? inputs.AsReadOnly() : [];

    /// <summary>The action an input triggers on a device, or null when unbound.</summary>
    public GameAction? ActionFor(InputDevice device, string input)
    {
        foreach ((GameAction action, List<string> inputs) in _bindings[device])
            if (inputs.Contains(input, StringComparer.Ordinal))
                return action;
        return null;
    }

    /// <summary>Rebinds one action. A duplicate is rejected unless <paramref name="allowSwap"/>, in
    /// which case the previous owner gives the input up. Confirm and Cancel must keep a recovery
    /// default, and no action may be left with nothing bound.</summary>
    public bool TryRebind(InputDevice device, GameAction action, IReadOnlyList<string> inputs,
        bool allowSwap, out InputBindings result, out string? error)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        result = this;
        error = null;

        if (inputs.Count == 0 || inputs.Any(string.IsNullOrWhiteSpace))
            return Fail(out error, $"{action} needs at least one non-empty input.");
        if (inputs.Distinct(StringComparer.Ordinal).Count() != inputs.Count)
            return Fail(out error, $"{action} lists the same input twice.");

        Dictionary<InputDevice, Dictionary<GameAction, List<string>>> next = CloneAll();
        Dictionary<GameAction, List<string>> deviceMap = next[device];

        foreach (string input in inputs)
        {
            GameAction? owner = ActionFor(device, input);
            if (owner is null || owner == action)
                continue;
            if (!allowSwap)
                return Fail(out error, $"'{input}' is already bound to {owner}.");
            deviceMap[owner.Value].RemoveAll(existing => existing == input);
        }

        deviceMap[action] = inputs.ToList();

        foreach ((GameAction other, List<string> bound) in deviceMap)
            if (bound.Count == 0)
                return Fail(out error, $"Rebinding {action} would leave {other} unbound.");

        foreach (GameAction recovery in (GameAction[])[GameAction.Confirm, GameAction.Cancel])
            if (!deviceMap[recovery].Intersect(DefaultsFor(device, recovery), StringComparer.Ordinal).Any())
                return Fail(out error, $"{recovery} must keep one of its default inputs as recovery.");

        result = new InputBindings(next);
        return true;
    }

    /// <summary>Action → input list for one device, for serialization.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ToMap(InputDevice device) =>
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            _bindings[device].ToDictionary(pair => pair.Key.ToString(),
                pair => (IReadOnlyList<string>)pair.Value.AsReadOnly()));

    /// <summary>Rebuilds from a serialized map. Missing actions keep defaults; unknown action names
    /// and empty lists are ignored. Returns false when a duplicate makes the profile ambiguous.</summary>
    public static bool TryFromMaps(IReadOnlyDictionary<string, IReadOnlyList<string>>? keyboard,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? gamepad,
        out InputBindings result, out string? warning)
    {
        warning = null;
        InputBindings bindings = Defaults();
        var next = bindings.CloneAll();
        var ignored = new List<string>();

        foreach ((InputDevice device, IReadOnlyDictionary<string, IReadOnlyList<string>>? map) in
            (ReadOnlySpan<(InputDevice, IReadOnlyDictionary<string, IReadOnlyList<string>>?)>)
            [(InputDevice.Keyboard, keyboard), (InputDevice.Gamepad, gamepad)])
        {
            if (map is null)
                continue;
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string name, IReadOnlyList<string> inputs) in map)
            {
                if (!Enum.TryParse(name, out GameAction action) || !Enum.IsDefined(action)
                    || inputs is null || inputs.Count == 0 || inputs.Any(string.IsNullOrWhiteSpace))
                {
                    ignored.Add(name);
                    continue;
                }
                foreach (string input in inputs)
                {
                    if (seen.TryGetValue(input, out string? owner) && owner != name)
                    {
                        result = Defaults();
                        warning = $"Options bind '{input}' to both {owner} and {name}; using defaults.";
                        return false;
                    }
                    seen[input] = name;
                }
                next[device][action] = inputs.ToList();
            }
        }

        if (ignored.Count > 0)
            warning = $"Ignored unknown option bindings: {string.Join(", ", ignored)}.";
        result = new InputBindings(next);
        return true;
    }

    private Dictionary<InputDevice, Dictionary<GameAction, List<string>>> CloneAll() =>
        _bindings.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToDictionary(inner => inner.Key, inner => inner.Value.ToList()));

    private static Dictionary<GameAction, List<string>> Clone(IReadOnlyDictionary<GameAction, string[]> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value.ToList());

    private static bool Fail(out string? error, string message)
    {
        error = message;
        return false;
    }
}

/// <summary>Merges held actions across devices and resolves a single facing direction
/// (ENGINE_RUNTIME_SPEC 16C). Opposite directions on one axis cancel; when two perpendicular
/// directions are held the most recently pressed wins; exact same-frame ties break ordinally
/// Up, Down, Left, Right. The resolution is deterministic, so it replays identically.</summary>
public sealed class InputMerger
{
    private static readonly GameAction[] Ordinal =
        [GameAction.Up, GameAction.Down, GameAction.Left, GameAction.Right];

    private readonly Dictionary<InputDevice, HashSet<GameAction>> _byDevice = new()
    {
        [InputDevice.Keyboard] = [],
        [InputDevice.Gamepad] = [],
    };
    private readonly Dictionary<GameAction, long> _pressedAt = [];
    private long _sequence;

    /// <summary>Replaces one device's held set. Devices merge by action, so either can hold any.</summary>
    public void Observe(InputDevice device, IEnumerable<GameAction> held)
    {
        ArgumentNullException.ThrowIfNull(held);
        _byDevice[device] = held.ToHashSet();
        Restamp();
    }

    /// <summary>Releases a disconnected device's held actions while keeping its binding profile for
    /// reconnection.</summary>
    public void Disconnect(InputDevice device)
    {
        _byDevice[device].Clear();
        Restamp();
    }

    public IReadOnlySet<GameAction> Held =>
        _byDevice[InputDevice.Keyboard].Union(_byDevice[InputDevice.Gamepad]).ToHashSet();

    /// <summary>The single active direction, or null when none is held or an axis cancels out.</summary>
    public GameAction? Direction()
    {
        IReadOnlySet<GameAction> held = Held;
        GameAction? vertical = Axis(held, GameAction.Up, GameAction.Down);
        GameAction? horizontal = Axis(held, GameAction.Left, GameAction.Right);

        if (vertical is null || horizontal is null)
            return vertical ?? horizontal;

        long v = _pressedAt.GetValueOrDefault(vertical.Value);
        long h = _pressedAt.GetValueOrDefault(horizontal.Value);
        if (v != h)
            return v > h ? vertical : horizontal;

        // Same-frame tie: ordinal order decides, so replays never depend on set iteration.
        return Array.IndexOf(Ordinal, vertical.Value) < Array.IndexOf(Ordinal, horizontal.Value)
            ? vertical
            : horizontal;
    }

    private static GameAction? Axis(IReadOnlySet<GameAction> held, GameAction low, GameAction high)
    {
        bool a = held.Contains(low);
        bool b = held.Contains(high);
        return a == b ? null : a ? low : high; // both or neither cancels
    }

    /// <summary>Stamps newly pressed actions with one shared sequence per poll, so actions pressed
    /// in the same frame tie exactly rather than by enumeration order.</summary>
    private void Restamp()
    {
        IReadOnlySet<GameAction> held = Held;
        long stamp = ++_sequence;
        foreach (GameAction action in Ordinal)
        {
            if (held.Contains(action))
                _pressedAt.TryAdd(action, stamp);
            else
                _pressedAt.Remove(action);
        }
    }
}
