namespace Cgm.Runtime.Engine;

/// <summary>An opaque renderer-owned texture lease. Scenes never see a GL handle.</summary>
public readonly record struct TextureHandle(int Id);

/// <summary>Integer pixel rectangle, top-left origin, half-open bounds.</summary>
public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Overlap of two rectangles, or an empty rectangle when they do not meet.</summary>
    public RectI Intersect(RectI other)
    {
        int x = Math.Max(X, other.X);
        int y = Math.Max(Y, other.Y);
        return new RectI(x, y, Math.Min(Right, other.Right) - x, Math.Min(Bottom, other.Bottom) - y);
    }

    public bool Contains(RectI inner) =>
        inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;
}

public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static Rgba White => new(255, 255, 255, 255);
}

[Flags]
public enum Flip { None = 0, Horizontal = 1, Vertical = 2 }

/// <summary>Why a new draw call started, for frame diagnostics.</summary>
public enum FlushReason { FirstQuad, Texture, Layer, Scissor, Capacity }

/// <summary>One submitted quad after camera resolution. Sequence is submission order.</summary>
public readonly record struct Quad(
    TextureHandle Texture,
    RectI Source,
    RectI Dest,
    int Layer,
    Flip Flip,
    Rgba Tint,
    RectI? Scissor,
    long Sequence);

/// <summary>A contiguous run of sorted quads sharing texture, layer, and scissor.</summary>
public readonly record struct DrawCall(
    TextureHandle Texture,
    int Layer,
    RectI? Scissor,
    int Start,
    int Count,
    FlushReason Reason);

public sealed record FrameStats(int Quads, int DrawCalls, int Capacity, IReadOnlyDictionary<FlushReason, int> Flushes);

/// <summary>The renderer's submission and batching core (ENGINE_RUNTIME_SPEC 16B). Pure and
/// GL-free, so ordering, flush boundaries, scissor intersection, and camera resolution are testable
/// headless; the OpenGL backend consumes <see cref="End"/>'s draw calls verbatim.</summary>
public sealed class QuadBatch
{
    public const int InitialCapacity = 2048;

    private readonly List<Quad> _quads = [];
    private readonly List<RectI> _scissors = [];
    private long _sequence;
    private int _cameraX;
    private int _cameraY;
    private bool _open;

    public int Capacity { get; private set; } = InitialCapacity;
    public int Count => _quads.Count;

    /// <summary>Starts a frame. The camera is the world pixel shown at the virtual viewport's
    /// top-left; the scissor stack always resets, so an unbalanced push cannot leak across frames.</summary>
    public void Begin(int cameraX = 0, int cameraY = 0)
    {
        _quads.Clear();
        _scissors.Clear();
        _sequence = 0;
        _cameraX = cameraX;
        _cameraY = cameraY;
        _open = true;
    }

    /// <summary>Pushes a scissor intersected with the current one, so nesting can only narrow.</summary>
    public void PushScissor(RectI rect)
    {
        RequireOpen();
        _scissors.Add(_scissors.Count == 0 ? rect : _scissors[^1].Intersect(rect));
    }

    public void PopScissor()
    {
        RequireOpen();
        if (_scissors.Count == 0)
            throw new InvalidOperationException("Scissor stack is empty.");
        _scissors.RemoveAt(_scissors.Count - 1);
    }

    /// <summary>Submits a world-space quad; the camera is subtracted so Core stays tile-based.</summary>
    public void World(TextureHandle texture, RectI source, RectI dest, int layer,
        Flip flip = Flip.None, Rgba? tint = null) =>
        Submit(texture, source, dest with { X = dest.X - _cameraX, Y = dest.Y - _cameraY }, layer, flip, tint);

    /// <summary>Submits a UI quad in virtual coordinates; the camera never applies.</summary>
    public void Ui(TextureHandle texture, RectI source, RectI dest, int layer,
        Flip flip = Flip.None, Rgba? tint = null) =>
        Submit(texture, source, dest, layer, flip, tint);

    /// <summary>Closes the frame: stable-order by layer then submission sequence, then group into
    /// draw calls that break on texture, layer, scissor, or capacity.</summary>
    public (IReadOnlyList<Quad> Quads, IReadOnlyList<DrawCall> Calls, FrameStats Stats) End()
    {
        RequireOpen();
        _open = false;

        // OrderBy is stable, so equal layers keep exact call order; the renderer never guesses Y sort.
        Quad[] sorted = _quads.OrderBy(q => q.Layer).ThenBy(q => q.Sequence).ToArray();
        // Group against the buffer that was actually in effect this frame, then grow for the next
        // one. Growing first would erase the very boundary that forced the split.
        int active = Capacity;
        if (sorted.Length > Capacity)
            Capacity = NextPowerOfTwo(sorted.Length);

        var calls = new List<DrawCall>();
        var flushes = new Dictionary<FlushReason, int>();
        int start = 0;
        FlushReason runReason = FlushReason.FirstQuad;
        for (int i = 1; i <= sorted.Length && sorted.Length > 0; i++)
        {
            FlushReason? reason = i == sorted.Length ? null : BreakReason(sorted[i - 1], sorted[i], i - start, active);
            if (i < sorted.Length && reason is null)
                continue;

            calls.Add(new DrawCall(sorted[start].Texture, sorted[start].Layer, sorted[start].Scissor,
                start, i - start, runReason));
            flushes[runReason] = flushes.GetValueOrDefault(runReason) + 1;
            if (reason is not null)
            {
                runReason = reason.Value;
                start = i;
            }
        }

        return (sorted, calls, new FrameStats(sorted.Length, calls.Count, Capacity, flushes));
    }

    private static FlushReason? BreakReason(Quad previous, Quad current, int runLength, int capacity)
    {
        if (previous.Texture != current.Texture)
            return FlushReason.Texture;
        if (previous.Layer != current.Layer)
            return FlushReason.Layer;
        if (!Nullable.Equals(previous.Scissor, current.Scissor))
            return FlushReason.Scissor;
        if (runLength >= capacity)
            return FlushReason.Capacity;
        return null;
    }

    private void Submit(TextureHandle texture, RectI source, RectI dest, int layer, Flip flip, Rgba? tint)
    {
        RequireOpen();
        if (source.IsEmpty || dest.IsEmpty)
            throw new ArgumentException("Quad source and destination must be non-empty.", nameof(source));
        if (!Enum.IsDefined(typeof(Flip), (int)flip) && flip != (Flip.Horizontal | Flip.Vertical))
            throw new ArgumentOutOfRangeException(nameof(flip), flip, "Unknown flip flags.");

        RectI? scissor = _scissors.Count == 0 ? null : _scissors[^1];
        if (scissor is { IsEmpty: true })
            return; // fully clipped by nested scissors: nothing to draw

        _quads.Add(new Quad(texture, source, dest, layer, flip, tint ?? Rgba.White, scissor, _sequence++));
    }

    private void RequireOpen()
    {
        if (!_open)
            throw new InvalidOperationException("Batch is not open; call Begin first.");
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = InitialCapacity;
        while (result < value)
            result <<= 1;
        return result;
    }
}
