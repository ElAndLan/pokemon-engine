using Cgm.Core.Model;

namespace Cgm.Creator.Assets;

/// <summary>
/// Builds walk-cycle animation clips from a standard character sheet (Phase 13 / Phase 4 deferral).
/// A standard sheet is a 3-frame × 4-direction grid, row-major, with rows in the order Down, Left,
/// Right, Up. Pure computation over sprite ids (the sheet's slicing produced them); produces four
/// looping <see cref="Animation"/> clips, one per facing. UI wiring lives in the Creator.
/// </summary>
public static class CharacterAnimation
{
    public const int Directions = 4;
    public const int FramesPerDirection = 3;

    /// <summary>Sheet row order for a standard 4-direction character sheet.</summary>
    public static readonly IReadOnlyList<Facing> RowOrder = [Facing.Down, Facing.Left, Facing.Right, Facing.Up];

    public static IReadOnlyDictionary<Facing, Animation> BuildWalkClips(
        string baseSlug, IReadOnlyList<EntityId> gridSprites, int frameMs = 150)
    {
        int expected = Directions * FramesPerDirection;
        if (gridSprites.Count != expected)
            throw new ArgumentException($"A standard character sheet needs {expected} sprites (3×4); got {gridSprites.Count}.");
        if (frameMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameMs), "Frame duration must be positive.");

        var clips = new Dictionary<Facing, Animation>(Directions);
        for (int row = 0; row < Directions; row++)
        {
            Facing dir = RowOrder[row];
            var frames = new List<AnimFrame>(FramesPerDirection);
            for (int col = 0; col < FramesPerDirection; col++)
                frames.Add(new AnimFrame(gridSprites[row * FramesPerDirection + col], frameMs));

            string dirName = dir.ToString().ToLowerInvariant();
            clips[dir] = new Animation
            {
                Id = new EntityId(EntityCategory.Anim, $"{baseSlug}_walk_{dirName}"),
                Name = $"{baseSlug} walk {dirName}",
                Frames = frames,
                Loop = true,
            };
        }
        return clips;
    }
}
