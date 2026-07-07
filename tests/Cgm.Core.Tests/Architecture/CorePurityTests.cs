using System.Reflection;
using Cgm.Core.Timing;

namespace Cgm.Core.Tests.Architecture;

/// <summary>
/// Enforces the load-bearing invariant from docs/CODING_STANDARDS.md and ADR-008:
/// Cgm.Core references only the BCL (+ serialization). No UI, graphics, audio, or
/// windowing dependencies may leak in — all game rules must stay headless and testable.
/// </summary>
public sealed class CorePurityTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    {
        "Avalonia",
        "Silk.NET",
        "OpenTK",
        "SkiaSharp",
        "System.Windows",
        "PresentationFramework",
        "Microsoft.Maui",
        "MonoGame",
    };

    [Fact]
    public void CoreAssembly_DoesNotReferenceUiOrGraphics()
    {
        Assembly core = typeof(FixedStepClock).Assembly;

        var violations = core.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => ForbiddenAssemblyPrefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Cgm.Core must stay UI/graphics-free but references: {string.Join(", ", violations)}");
    }
}
