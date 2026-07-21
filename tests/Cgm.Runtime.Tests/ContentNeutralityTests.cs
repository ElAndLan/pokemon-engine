using System.Text.RegularExpressions;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16A content-neutrality row: Runtime source contains no content ID,
/// fallback construction, or fixture path. This guards the invariant for every later package, so a
/// content assumption cannot creep back in once 16A has removed it.</summary>
public sealed class ContentNeutralityTests
{
    private static readonly string RuntimeSource = Path.Combine(TestRepo.Root, "src", "Cgm.Runtime");

    /// <summary>Each rule is (name, pattern, a line that must trip it). The example doubles as proof
    /// the pattern still detects, so a scan can never pass merely because its regex stopped matching.</summary>
    public static TheoryData<string, string, string> Rules() => new()
    {
        {
            "content ID literal",
            @"""(ability|encounter|item|map|move|project|species|tileset|trainer|type|sheet|clip):[a-z0-9_]+""",
            @"var id = ""species:asterling"";"
        },
        { "content ID construction", @"EntityId\.Parse", @"EntityId.Parse(""move:leaf_jab"")" },
        {
            "fallback entity selection",
            @"\b(FirstOrDefault|LastOrDefault|SingleOrDefault)\s*\(",
            "var s = db.All<Species>().FirstOrDefault();"
        },
        { "fixture or sample path", @"(samples[/\\]|fixture-min|demo-game|pokeapi)", @"Load(""samples/demo-game"");" },
        { "official franchise term", @"\b(pokemon|pokedex|pikachu|nintendo|game ?freak)\b", "// like Pokemon does" },
    };

    private static IEnumerable<(string File, int Line, string Text)> Lines() =>
        Directory.EnumerateFiles(RuntimeSource, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .SelectMany(path => File.ReadAllLines(path)
                .Select((text, index) => (File: Path.GetRelativePath(TestRepo.Root, path), Line: index + 1, Text: text)));

    [Theory]
    [MemberData(nameof(Rules))]
    public void RuntimeSource_ViolatesNoNeutralityRule(string name, string pattern, string example)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        Assert.True(regex.IsMatch(example), $"The '{name}' pattern no longer detects its own example.");

        var offenders = Lines().Where(l => regex.IsMatch(l.Text))
            .Select(l => $"  {l.File}:{l.Line}: {l.Text.Trim()}").ToList();
        Assert.True(offenders.Count == 0, $"Runtime violates '{name}':\n{string.Join("\n", offenders)}");
    }

    /// <summary>The scan is only meaningful if it is actually reading the whole project.</summary>
    [Fact]
    public void Scan_ReadsRuntimeSource()
    {
        Assert.True(Directory.Exists(RuntimeSource), RuntimeSource);
        Assert.True(Lines().Count() > 200, "Expected to scan the whole Runtime project.");
        Assert.Contains(Lines(), l => l.File.EndsWith("BootLoader.cs", StringComparison.Ordinal));
        Assert.Contains(Lines(), l => l.File.EndsWith("RuntimeHost.cs", StringComparison.Ordinal));
    }
}
