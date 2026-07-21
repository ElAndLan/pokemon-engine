using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>
/// Where asset bytes come from. This is the 16A source seam that lets raw-project and exported
/// modes behave identically (ADR-006): scenes ask for a project-relative path and never learn
/// whether it arrived from a folder or a pack.
/// </summary>
public interface IAssetSource
{
    /// <summary>Reads an asset, or returns false when it is absent or the path is unsafe. Never
    /// throws for a missing asset — callers decide whether a gap is fatal.</summary>
    bool TryRead(string path, out byte[] bytes);
}

/// <summary>Assets embedded in a <c>.cgmpack</c>, already verified by the pack's content hash.</summary>
public sealed class PackAssetSource(IReadOnlyDictionary<string, byte[]> assets) : IAssetSource
{
    private readonly IReadOnlyDictionary<string, byte[]> _assets =
        assets ?? throw new ArgumentNullException(nameof(assets));

    public bool TryRead(string path, out byte[] bytes)
    {
        string key = AssetPath.Normalize(path);
        // TryGetValue nulls its out parameter on a miss, so the empty default is assigned after it.
        if (key.Length != 0 && _assets.TryGetValue(key, out byte[]? found))
        {
            bytes = found;
            return true;
        }
        bytes = [];
        return false;
    }
}

/// <summary>Assets read from a project folder during development. <see cref="AssetPath"/> confines
/// every read to the root, so authored data cannot reach a file outside the project.</summary>
public sealed class FolderAssetSource(string root) : IAssetSource
{
    private readonly string _root = root ?? throw new ArgumentNullException(nameof(root));

    public bool TryRead(string path, out byte[] bytes)
    {
        bytes = [];
        string? full = AssetPath.Resolve(_root, path);
        if (full is null || !File.Exists(full))
            return false;

        bytes = File.ReadAllBytes(full);
        return true;
    }
}
