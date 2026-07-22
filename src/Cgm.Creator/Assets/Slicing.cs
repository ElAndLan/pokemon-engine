using Cgm.Core.Model;

namespace Cgm.Creator.Assets;

/// <summary>Uniform grid parameters for slicing a sheet (ASSET_PIPELINE_SPEC v0).</summary>
public readonly record struct GridSpec(
    int CellW, int CellH, int OffsetX = 0, int OffsetY = 0, int SpacingX = 0, int SpacingY = 0);

/// <summary>Import v0 — slices an image into cells on a uniform grid.</summary>
public static class GridSlicer
{
    public static IReadOnlyList<Rect> Slice(int imageW, int imageH, GridSpec g)
    {
        if (g.CellW <= 0 || g.CellH <= 0)
            throw new ArgumentOutOfRangeException(nameof(g), "Cell size must be positive.");
        if (g.OffsetX < 0 || g.OffsetY < 0 || g.SpacingX < 0 || g.SpacingY < 0)
            throw new ArgumentOutOfRangeException(nameof(g), "Offsets and spacing must be non-negative.");

        var rects = new List<Rect>();
        for (int y = g.OffsetY; y + g.CellH <= imageH; y += g.CellH + g.SpacingY)
            for (int x = g.OffsetX; x + g.CellW <= imageW; x += g.CellW + g.SpacingX)
                rects.Add(new Rect(x, y, g.CellW, g.CellH));
        return rects;
    }
}

/// <summary>Import v1 — suggests a cell size from image dimensions alone.</summary>
public static class SizeSuggester
{
    private static readonly int[] Common = [16, 32, 48, 64];

    public static int? Suggest(int imageW, int imageH, int? preferTileSize = null)
    {
        List<int> fits = Common.Where(s => imageW % s == 0 && imageH % s == 0).ToList();
        if (fits.Count == 0)
            return null;
        if (preferTileSize is { } p && fits.Contains(p))
            return p;
        return fits.Max();
    }
}

/// <summary>Import v3 — finds sprites on irregular sheets no grid fits (ASSET_PIPELINE_SPEC 17B):
/// 4-neighbor flood fill over opaque pixels, 1-pixel noise discarded, near-touching bounds merged,
/// reading order.</summary>
public static class ComponentSlicer
{
    /// <param name="opaque">Row-major, <c>opaque[y*width + x]</c> = pixel is not fully transparent.</param>
    public static IReadOnlyList<Rect> Detect(bool[] opaque, int width, int height, int mergeThreshold = 2)
    {
        ArgumentNullException.ThrowIfNull(opaque);
        if (width <= 0 || height <= 0 || opaque.Length != width * height)
            throw new ArgumentException("opaque length must equal width*height.", nameof(opaque));
        if (mergeThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(mergeThreshold));

        // Flood fill with an explicit stack: a 4096² image must not recurse.
        var seen = new bool[opaque.Length];
        var bounds = new List<(int MinX, int MinY, int MaxX, int MaxY, int Pixels)>();
        var stack = new Stack<int>();

        for (int start = 0; start < opaque.Length; start++)
        {
            if (!opaque[start] || seen[start])
                continue;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, pixels = 0;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % width, y = i / width;
                pixels++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;

                Visit(i - width, y > 0);
                Visit(i + width, y < height - 1);
                Visit(i - 1, x > 0);
                Visit(i + 1, x < width - 1);
            }

            if (pixels > 1) // 1-pixel components are noise by default
                bounds.Add((minX, minY, maxX, maxY, pixels));

            continue;

            void Visit(int index, bool inBounds)
            {
                if (inBounds && opaque[index] && !seen[index])
                {
                    seen[index] = true;
                    stack.Push(index);
                }
            }
        }

        // Merge to a fixed point: overlapping bounds, or a gap ≤ threshold on one axis with
        // overlap on the other, are one sprite whose outline happens to break apart.
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int a = 0; a < bounds.Count && !merged; a++)
                for (int b = a + 1; b < bounds.Count && !merged; b++)
                    if (ShouldMerge(bounds[a], bounds[b], mergeThreshold))
                    {
                        bounds[a] = (
                            Math.Min(bounds[a].MinX, bounds[b].MinX),
                            Math.Min(bounds[a].MinY, bounds[b].MinY),
                            Math.Max(bounds[a].MaxX, bounds[b].MaxX),
                            Math.Max(bounds[a].MaxY, bounds[b].MaxY),
                            bounds[a].Pixels + bounds[b].Pixels);
                        bounds.RemoveAt(b);
                        merged = true;
                    }
        }

        return bounds
            .OrderBy(c => c.MinY).ThenBy(c => c.MinX) // reading order, deterministic
            .Select(c => new Rect(c.MinX, c.MinY, c.MaxX - c.MinX + 1, c.MaxY - c.MinY + 1))
            .ToList();
    }

    private static bool ShouldMerge(
        (int MinX, int MinY, int MaxX, int MaxY, int Pixels) a,
        (int MinX, int MinY, int MaxX, int MaxY, int Pixels) b, int threshold)
    {
        int gapX = Math.Max(Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX) - 1, 0);
        int gapY = Math.Max(Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY) - 1, 0);
        return gapX <= threshold && gapY <= threshold;
    }
}

public readonly record struct GutterFit(
    int CellW, int CellH, int SpacingX, int SpacingY, int MarginX, int MarginY);

/// <summary>Import v2 — infers a cell grid from transparent gutters between sprites.</summary>
public static class GutterDetector
{
    /// <param name="opaque">Row-major, <c>opaque[y*width + x]</c> = pixel is not fully transparent.</param>
    public static GutterFit? Detect(bool[] opaque, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(opaque);
        if (width <= 0 || height <= 0 || opaque.Length != width * height)
            throw new ArgumentException("opaque length must equal width*height.", nameof(opaque));

        if (AnalyzeAxis(ColumnsOccupied(opaque, width, height)) is not { } x) return null;
        if (AnalyzeAxis(RowsOccupied(opaque, width, height)) is not { } y) return null;

        return new GutterFit(x.Cell, y.Cell, x.Spacing, y.Spacing, x.Margin, y.Margin);
    }

    private static bool[] ColumnsOccupied(bool[] op, int w, int h)
    {
        var col = new bool[w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (op[y * w + x]) { col[x] = true; break; }
        return col;
    }

    private static bool[] RowsOccupied(bool[] op, int w, int h)
    {
        var row = new bool[h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (op[y * w + x]) { row[y] = true; break; }
        return row;
    }

    /// <summary>Uniform cell/gap pattern along one axis, or null if it isn't a clean grid.</summary>
    private static (int Cell, int Spacing, int Margin)? AnalyzeAxis(bool[] occupied)
    {
        List<(bool On, int Len)> runs = Runs(occupied);
        if (runs.Count == 0) return null;

        int margin = runs[0].On ? 0 : runs[0].Len;

        List<int> cells = runs.Where(r => r.On).Select(r => r.Len).ToList();
        if (cells.Count < 2 || cells.Distinct().Count() != 1) // need a repeating, uniform grid
            return null;

        // Interior gaps: the "off" runs that sit between two "on" runs.
        var interiorGaps = new List<int>();
        for (int i = 1; i < runs.Count - 1; i++)
            if (!runs[i].On)
                interiorGaps.Add(runs[i].Len);

        if (interiorGaps.Count == 0 || interiorGaps.Distinct().Count() != 1)
            return null;

        return (cells[0], interiorGaps[0], margin);
    }

    private static List<(bool On, int Len)> Runs(bool[] a)
    {
        var runs = new List<(bool, int)>();
        int i = 0;
        while (i < a.Length)
        {
            int j = i;
            while (j < a.Length && a[j] == a[i]) j++;
            runs.Add((a[i], j - i));
            i = j;
        }
        return runs;
    }
}
