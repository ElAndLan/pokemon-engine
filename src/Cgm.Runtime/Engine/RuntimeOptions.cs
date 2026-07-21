using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgm.Runtime.Engine;

/// <summary>Runtime-local options (ENGINE_RUNTIME_SPEC 16C, DATA_SCHEMA §Runtime options v1). These
/// are deliberately separate from save data and never bump the project schema.</summary>
public sealed record RuntimeOptionsFile
{
    public const int CurrentVersion = 1;

    public int OptionsVersion { get; init; } = CurrentVersion;
    public Dictionary<string, IReadOnlyList<string>>? KeyboardBindings { get; init; }
    public Dictionary<string, IReadOnlyList<string>>? GamepadBindings { get; init; }
    public int MusicVolume { get; init; } = 100;
    public int SfxVolume { get; init; } = 100;
}

/// <summary>Loaded options plus any single warning produced while reading them.</summary>
public sealed record RuntimeOptions(InputBindings Bindings, int MusicVolume, int SfxVolume)
{
    public static RuntimeOptions Defaults() => new(InputBindings.Defaults(), 100, 100);

    public RuntimeOptionsFile ToFile() => new()
    {
        OptionsVersion = RuntimeOptionsFile.CurrentVersion,
        KeyboardBindings = Bindings.ToMap(InputDevice.Keyboard).ToDictionary(p => p.Key, p => p.Value),
        GamepadBindings = Bindings.ToMap(InputDevice.Gamepad).ToDictionary(p => p.Key, p => p.Value),
        MusicVolume = MusicVolume,
        SfxVolume = SfxVolume,
    };
}

/// <summary>Reads and writes <c>options.json</c> beside the executable. Bad options degrade to
/// defaults with one warning and never prevent boot: the player must always be able to start the
/// game and fix them from the menu.</summary>
public static class RuntimeOptionsFileStore
{
    public const string FileName = "options.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static RuntimeOptions Load(string folder, out string? warning)
    {
        ArgumentNullException.ThrowIfNull(folder);
        warning = null;
        string path = Path.Combine(folder, FileName);
        if (!File.Exists(path))
            return RuntimeOptions.Defaults();   // first run is not a problem

        RuntimeOptionsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<RuntimeOptionsFile>(File.ReadAllText(path), Json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            warning = $"Options file is unreadable ({ex.GetType().Name}); using defaults.";
            return RuntimeOptions.Defaults();
        }

        if (file is null)
            return Warn(out warning, "Options file is empty; using defaults.");
        if (file.OptionsVersion > RuntimeOptionsFile.CurrentVersion)
            return Warn(out warning,
                $"Options version {file.OptionsVersion} is newer than {RuntimeOptionsFile.CurrentVersion}; using defaults.");
        if (file.OptionsVersion < RuntimeOptionsFile.CurrentVersion)
            // Older files are read on a best-effort basis: missing fields simply keep their defaults.
            warning = $"Options version {file.OptionsVersion} is older than {RuntimeOptionsFile.CurrentVersion}; missing values use defaults.";

        if (!InputBindings.TryFromMaps(Wrap(file.KeyboardBindings), Wrap(file.GamepadBindings),
            out InputBindings bindings, out string? bindingWarning))
        {
            warning ??= bindingWarning;
            return RuntimeOptions.Defaults();
        }
        warning ??= bindingWarning;

        return new RuntimeOptions(bindings,
            Math.Clamp(file.MusicVolume, 0, 100),
            Math.Clamp(file.SfxVolume, 0, 100));
    }

    public static void Save(string folder, RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(options.ToFile(), Json));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? Wrap(
        Dictionary<string, IReadOnlyList<string>>? map) => map;

    private static RuntimeOptions Warn(out string? warning, string message)
    {
        warning = message;
        return RuntimeOptions.Defaults();
    }
}
