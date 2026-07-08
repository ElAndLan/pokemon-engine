using Cgm.Core.Model;

namespace Cgm.Creator.Assets;

/// <summary>One sprite placed in an atlas. <see cref="SpriteIndex"/> is the caller's input index,
/// so it can map back to the sprite id and rewrite its region. <see cref="Rect"/> is atlas-local.</summary>
public readonly record struct AtlasPlacement(int SpriteIndex, Rect Rect);

/// <summary>A packed atlas: its used extent and the sprites within it (ASSET_PIPELINE_SPEC v5).</summary>
public readonly record struct Atlas(int Width, int Height, IReadOnlyList<AtlasPlacement> Placements);

/// <summary>
/// Export-time atlas packing (import v5, Addendum §9). Skyline Bottom-Left: places sprites
/// largest-first into ≤<c>maxSize</c>² atlases, splitting to a new atlas on overflow. Pure — sizes
/// in, placements out; the pixel copy and pack write consume the result. Deterministic.
/// </summary>
public static class AtlasPacker
{
    public static IReadOnlyList<Atlas> Pack(IReadOnlyList<(int W, int H)> sizes, int maxSize = 2048)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Atlas size must be positive.");

        // Largest-first, with a stable final tie-break on the input index for determinism.
        int[] order = [.. Enumerable.Range(0, sizes.Count)];
        Array.Sort(order, (a, b) =>
        {
            int byH = sizes[b].H.CompareTo(sizes[a].H);
            if (byH != 0) return byH;
            int byW = sizes[b].W.CompareTo(sizes[a].W);
            return byW != 0 ? byW : a.CompareTo(b);
        });

        var atlases = new List<Atlas>();
        var skyline = new Skyline(maxSize);
        var placements = new List<AtlasPlacement>();

        foreach (int i in order)
        {
            (int w, int h) = sizes[i];
            if (w <= 0 || h <= 0)
                throw new ArgumentException($"Sprite {i} has non-positive size {w}×{h}.");
            if (w > maxSize || h > maxSize)
                throw new ArgumentException($"Sprite {i} ({w}×{h}) exceeds the {maxSize}² atlas limit.");

            if (!skyline.TryPlace(w, h, out Rect rect))
            {
                atlases.Add(skyline.ToAtlas(placements));
                skyline = new Skyline(maxSize);
                placements = [];
                skyline.TryPlace(w, h, out rect); // fresh atlas always fits (w,h ≤ maxSize)
            }
            placements.Add(new AtlasPlacement(i, rect));
        }

        if (placements.Count > 0)
            atlases.Add(skyline.ToAtlas(placements));
        return atlases;
    }

    /// <summary>The classic skyline of horizontal segments; finds the lowest bottom-left fit.</summary>
    private sealed class Skyline
    {
        private readonly int _maxSize;
        private readonly List<(int X, int Y, int Width)> _nodes;

        public Skyline(int maxSize)
        {
            _maxSize = maxSize;
            _nodes = [(0, 0, maxSize)];
        }

        public bool TryPlace(int w, int h, out Rect rect)
        {
            int bestIndex = -1, bestY = int.MaxValue, bestX = int.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                int y = Fit(i, w, h);
                if (y >= 0 && (y < bestY || (y == bestY && _nodes[i].X < bestX)))
                {
                    bestY = y;
                    bestX = _nodes[i].X;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                rect = default;
                return false;
            }

            rect = new Rect(bestX, bestY, w, h);
            AddLevel(bestIndex, bestX, bestY, w, h);
            return true;
        }

        /// <summary>The y at which a w×h rect starting at node i would rest, or -1 if it can't fit.</summary>
        private int Fit(int i, int w, int h)
        {
            int x = _nodes[i].X;
            if (x + w > _maxSize)
                return -1;

            int widthLeft = w, y = _nodes[i].Y;
            for (int j = i; widthLeft > 0; j++)
            {
                y = Math.Max(y, _nodes[j].Y);
                if (y + h > _maxSize)
                    return -1;
                widthLeft -= _nodes[j].Width;
            }
            return y;
        }

        private void AddLevel(int i, int x, int y, int w, int h)
        {
            _nodes.Insert(i, (x, y + h, w));

            // Trim/remove the segments the new node now covers.
            for (int k = i + 1; k < _nodes.Count;)
            {
                (int nx, int ny, int nw) = _nodes[k];
                if (nx < x + w)
                {
                    int shrink = x + w - nx;
                    if (nw <= shrink)
                    {
                        _nodes.RemoveAt(k);
                        continue;
                    }
                    _nodes[k] = (nx + shrink, ny, nw - shrink);
                    break;
                }
                break;
            }

            // Merge adjacent segments at the same height.
            for (int k = 0; k < _nodes.Count - 1;)
            {
                if (_nodes[k].Y == _nodes[k + 1].Y)
                {
                    _nodes[k] = (_nodes[k].X, _nodes[k].Y, _nodes[k].Width + _nodes[k + 1].Width);
                    _nodes.RemoveAt(k + 1);
                }
                else k++;
            }
        }

        public Atlas ToAtlas(IReadOnlyList<AtlasPlacement> placements)
        {
            int w = 0, h = 0;
            foreach (AtlasPlacement p in placements)
            {
                w = Math.Max(w, p.Rect.X + p.Rect.W);
                h = Math.Max(h, p.Rect.Y + p.Rect.H);
            }
            return new Atlas(w, h, placements);
        }
    }
}
