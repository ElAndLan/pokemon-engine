namespace Cgm.Creator.Maps;

/// <summary>Pure tile-layer edits (MAP_EDITOR_SPEC). Each op returns a new row-major
/// <c>int[width*height]</c> so the map editor commits a stroke as one undoable snapshot.
/// <c>-1</c> is the empty cell.</summary>
public static class MapLayerOps
{
    public const int Empty = -1;

    public static int[] Paint(int[] layer, int w, int h, int x, int y, int tile)
    {
        var next = (int[])layer.Clone();
        if (InBounds(x, y, w, h))
            next[y * w + x] = tile;
        return next;
    }

    public static int[] RectFill(int[] layer, int w, int h, int x0, int y0, int x1, int y1, int tile)
    {
        var next = (int[])layer.Clone();
        int lx = Math.Max(0, Math.Min(x0, x1)), ly = Math.Max(0, Math.Min(y0, y1));
        int hx = Math.Min(w - 1, Math.Max(x0, x1)), hy = Math.Min(h - 1, Math.Max(y0, y1));
        for (int y = ly; y <= hy; y++)
            for (int x = lx; x <= hx; x++)
                next[y * w + x] = tile;
        return next;
    }

    /// <summary>4-connected flood fill of the contiguous region matching the start cell's value.</summary>
    public static int[] BucketFill(int[] layer, int w, int h, int x, int y, int tile)
    {
        var next = (int[])layer.Clone();
        if (!InBounds(x, y, w, h))
            return next;

        int target = layer[y * w + x];
        if (target == tile)
            return next;

        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            (int cx, int cy) = stack.Pop();
            if (!InBounds(cx, cy, w, h) || next[cy * w + cx] != target)
                continue;
            next[cy * w + cx] = tile;
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
        return next;
    }

    /// <summary>Resizes a layer, preserving the top-left overlap and padding new cells with -1.</summary>
    public static int[] Resize(int[] layer, int oldW, int oldH, int newW, int newH)
    {
        if (newW <= 0 || newH <= 0)
            throw new ArgumentOutOfRangeException(nameof(newW), "New dimensions must be positive.");

        var next = new int[newW * newH];
        Array.Fill(next, Empty);
        int cw = Math.Min(oldW, newW), ch = Math.Min(oldH, newH);
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                next[y * newW + x] = layer[y * oldW + x];
        return next;
    }

    private static bool InBounds(int x, int y, int w, int h) => x >= 0 && y >= 0 && x < w && y < h;
}
