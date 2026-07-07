namespace Cgm.Core.Model;

/// <summary>The single randomness seam (CODING_STANDARDS: all sim randomness is injected and
/// seeded, never <c>new Random()</c>). Golden replays depend on this.</summary>
public interface IRng
{
    /// <summary>A value in <c>[0, maxExclusive)</c>.</summary>
    int Next(int maxExclusive);

    /// <summary>A value in <c>[minInclusive, maxExclusive)</c>.</summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>A value in <c>[0, 1)</c>.</summary>
    double NextDouble();
}

/// <summary>The one sanctioned <see cref="Random"/> wrapper — seeded, deterministic.</summary>
public sealed class Rng : IRng
{
    private readonly Random _random;

    public Rng(int seed) => _random = new Random(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    public double NextDouble() => _random.NextDouble();
}
