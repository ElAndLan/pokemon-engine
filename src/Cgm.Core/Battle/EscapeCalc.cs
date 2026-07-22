using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public readonly record struct EscapeResult(bool Escaped, int Odds, int? Roll);

/// <summary>Modern main-series wild-battle escape check. Speeds are unmodified battle stats;
/// repeated attempts become progressively easier.</summary>
public static class EscapeCalc
{
    public static EscapeResult Attempt(int playerSpeed, int wildSpeed, int attempt, IRng rng)
    {
        if (playerSpeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerSpeed));
        if (wildSpeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(wildSpeed));
        if (attempt <= 0)
            throw new ArgumentOutOfRangeException(nameof(attempt));
        ArgumentNullException.ThrowIfNull(rng);

        if (playerSpeed >= wildSpeed)
            return new EscapeResult(true, 256, null);

        int odds = (int)Math.Min(256L,
            (long)playerSpeed * 128 / wildSpeed + (long)30 * attempt);
        if (odds == 256)
            return new EscapeResult(true, odds, null);
        int roll = rng.Next(256);
        return new EscapeResult(roll < odds, odds, roll);
    }
}
