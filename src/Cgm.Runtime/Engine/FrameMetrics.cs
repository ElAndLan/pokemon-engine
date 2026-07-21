using System.Diagnostics;

namespace Cgm.Runtime.Engine;

/// <summary>A finished measurement series in milliseconds.</summary>
public sealed record DurationStats(int Samples, double Min, double Median, double P95, double Max)
{
    public static DurationStats Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Percentiles use nearest-rank on the sorted samples, which needs no interpolation and
    /// gives the same answer for the same inputs on every machine.</summary>
    public static DurationStats From(IReadOnlyList<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            return Empty;

        double[] sorted = [.. samples.Order()];
        return new DurationStats(sorted.Length, sorted[0], At(sorted, 0.50), At(sorted, 0.95), sorted[^1]);
    }

    private static double At(double[] sorted, double percentile)
    {
        int rank = (int)Math.Ceiling(percentile * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }
}

/// <summary>The 16G resource report for one measured run.</summary>
public sealed record FrameReport(
    DurationStats Update,
    DurationStats Render,
    long TotalAllocatedBytes,
    int Frames)
{
    /// <summary>Managed bytes allocated per frame, the budget's steady-state figure.</summary>
    public double AllocatedBytesPerFrame => Frames == 0 ? 0 : (double)TotalAllocatedBytes / Frames;

    public string Format() =>
        $"frames={Frames} update(ms) p50={Update.Median:F3} p95={Update.P95:F3} max={Update.Max:F3} "
        + $"render(ms) p50={Render.Median:F3} p95={Render.P95:F3} max={Render.Max:F3} "
        + $"alloc/frame={AllocatedBytesPerFrame:F0}B";
}

/// <summary>Collects per-frame update and render durations plus managed allocation
/// (ENGINE_RUNTIME_SPEC 16G budgets). Measurement is opt-in and must never change what is simulated:
/// it records elapsed time around calls the host already makes and reads a counter the runtime
/// already maintains.</summary>
public sealed class FrameMetrics
{
    private readonly List<double> _update = [];
    private readonly List<double> _render = [];
    private long _allocatedAtStart;
    private long _allocated;
    private int _frames;

    /// <summary>Begins a measured run, taking the allocation baseline. Called after loading, so
    /// content loads do not count against the steady-state budget.</summary>
    public void Begin()
    {
        _update.Clear();
        _render.Clear();
        _frames = 0;
        _allocated = 0;
        _allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>Times one update. The action runs exactly once whether or not metrics are enabled.</summary>
    public void Update(Action update)
    {
        ArgumentNullException.ThrowIfNull(update);
        long start = Stopwatch.GetTimestamp();
        update();
        _update.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    public void Render(Action render)
    {
        ArgumentNullException.ThrowIfNull(render);
        long start = Stopwatch.GetTimestamp();
        render();
        _render.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        _frames++;
    }

    /// <summary>Closes the run and captures total managed allocation since <see cref="Begin"/>.</summary>
    public FrameReport End()
    {
        _allocated = GC.GetAllocatedBytesForCurrentThread() - _allocatedAtStart;
        return new FrameReport(DurationStats.From(_update), DurationStats.From(_render),
            Math.Max(0, _allocated), _frames);
    }
}
