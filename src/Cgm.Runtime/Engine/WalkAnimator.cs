using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>
/// Plays a character's four directional walk clips (DATA_SCHEMA §4.8), picking the clip by facing
/// and advancing frames while the character moves. Pure presentation, driven one fixed tick at a
/// time so playback is deterministic and replay-safe: it advances in tick units, never off a
/// wall-clock read.
/// <para>Clips are ordered by <see cref="Facing"/> (Down, Up, Left, Right); a clip's frame 0 is the
/// standing pose, shown whenever the character is not moving. A missing clip yields no sprite, so a
/// character with no animation simply falls back to the caller's flat marker.</para>
/// </summary>
public sealed class WalkAnimator
{
    private readonly Animation?[] _clips = new Animation?[4];
    private double _elapsedMs;

    public WalkAnimator(IReadOnlyList<Animation?> clipsByFacing)
    {
        ArgumentNullException.ThrowIfNull(clipsByFacing);
        for (int i = 0; i < clipsByFacing.Count && i < _clips.Length; i++)
            _clips[i] = clipsByFacing[i];
    }

    /// <summary>Advances one fixed tick and returns the sprite to draw, or null when this facing has
    /// no usable clip. Standing (<paramref name="moving"/> false) resets to the clip's rest frame, so
    /// every step begins from the standing pose.</summary>
    public EntityId? Advance(Facing facing, bool moving, double tickMs)
    {
        Animation? clip = _clips[(int)facing];
        if (clip is not { Frames.Count: > 0 })
            return null;

        if (!moving)
        {
            _elapsedMs = 0;
            return clip.Frames[0].Sprite;
        }

        _elapsedMs += tickMs;
        return FrameAt(clip, _elapsedMs).Sprite;
    }

    /// <summary>The frame whose duration window contains the looped elapsed time.</summary>
    private static AnimFrame FrameAt(Animation clip, double elapsedMs)
    {
        double total = 0;
        foreach (AnimFrame frame in clip.Frames)
            total += Math.Max(0, frame.Ms);
        if (total <= 0)
            return clip.Frames[0];

        double t = elapsedMs % total;
        foreach (AnimFrame frame in clip.Frames)
        {
            if (t < frame.Ms)
                return frame;
            t -= frame.Ms;
        }
        return clip.Frames[^1];   // only reached by floating-point edge at the exact loop boundary
    }
}
