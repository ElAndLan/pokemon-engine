namespace Cgm.Core.Timing;

/// <summary>
/// Deterministic fixed-timestep accumulator (ADR-005). The simulation advances in whole
/// ticks at a fixed rate regardless of render/frame timing; leftover time is carried and
/// exposed as <see cref="InterpolationAlpha"/> for render-side interpolation.
///
/// Pure by design: it holds no time source. Callers feed elapsed milliseconds, which makes
/// the whole loop replayable and unit-testable. To prevent a spiral of death after a stall,
/// a single <see cref="Advance"/> yields at most <see cref="MaxTicksPerAdvance"/> ticks and
/// discards the backlog beyond that cap.
/// </summary>
public sealed class FixedStepClock
{
    public const int DefaultTickRate = 60;
    public const int DefaultMaxTicksPerAdvance = 5;

    private double _accumulatorMs;

    // Absorbs floating-point representational error at the tick boundary so that feeding an
    // exact multiple of the interval yields exactly that many ticks (repeated subtraction of
    // 1000.0/rate can land a hair under the interval otherwise). Tiny relative to a tick, so
    // it never ticks early in practice and contributes no drift (negatives are clamped below).
    private readonly double _boundaryEpsilonMs;

    public FixedStepClock(int tickRate = DefaultTickRate, int maxTicksPerAdvance = DefaultMaxTicksPerAdvance)
    {
        if (tickRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Tick rate must be positive.");
        if (maxTicksPerAdvance <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance), maxTicksPerAdvance, "Max ticks per advance must be positive.");

        TickRate = tickRate;
        MaxTicksPerAdvance = maxTicksPerAdvance;
        TickIntervalMs = 1000.0 / tickRate;
        _boundaryEpsilonMs = TickIntervalMs * 1e-9;
    }

    /// <summary>Simulation ticks per second (Hz).</summary>
    public int TickRate { get; }

    /// <summary>Milliseconds represented by one tick.</summary>
    public double TickIntervalMs { get; }

    /// <summary>Upper bound on ticks produced by a single <see cref="Advance"/> call.</summary>
    public int MaxTicksPerAdvance { get; }

    /// <summary>Unconsumed time in milliseconds; always in <c>[0, TickIntervalMs)</c>.</summary>
    public double AccumulatorMs => _accumulatorMs;

    /// <summary>
    /// Fraction of the way to the next tick, in <c>[0, 1)</c>. Render code uses this to
    /// interpolate visual positions between the previous and current simulation state.
    /// </summary>
    public double InterpolationAlpha => _accumulatorMs / TickIntervalMs;

    /// <summary>
    /// Accumulates <paramref name="elapsedMs"/> and returns how many whole simulation ticks
    /// are now due (0..<see cref="MaxTicksPerAdvance"/>). If the cap is hit, surplus backlog
    /// is discarded so <see cref="InterpolationAlpha"/> stays in range.
    /// </summary>
    public int Advance(double elapsedMs)
    {
        if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs))
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), elapsedMs, "Elapsed time must be finite.");
        if (elapsedMs < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), elapsedMs, "Elapsed time cannot be negative.");

        _accumulatorMs += elapsedMs;

        int ticks = 0;
        while (_accumulatorMs + _boundaryEpsilonMs >= TickIntervalMs && ticks < MaxTicksPerAdvance)
        {
            _accumulatorMs -= TickIntervalMs;
            ticks++;
        }

        // A boundary tick can leave a sub-epsilon negative remainder; snap it to zero.
        if (_accumulatorMs < 0.0)
            _accumulatorMs = 0.0;

        // Spiral-of-death guard: if we stopped because of the cap while time remains,
        // drop the backlog down to a sub-tick remainder.
        if (_accumulatorMs >= TickIntervalMs)
            _accumulatorMs %= TickIntervalMs;

        return ticks;
    }

    /// <summary>Clears accumulated time (e.g. after a load screen or pause).</summary>
    public void Reset() => _accumulatorMs = 0.0;
}
