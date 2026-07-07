namespace Cgm.Runtime.Engine;

/// <summary>Camera positioning: centers on a target, clamped to map edges; centers a small map
/// (ENGINE_RUNTIME_SPEC). Returns the world coord shown at the viewport's top-left, in pixels.</summary>
public static class Camera
{
    public static (int X, int Y) Clamp(int targetX, int targetY, int viewW, int viewH, int mapW, int mapH) =>
        (ClampAxis(targetX - viewW / 2, viewW, mapW),
         ClampAxis(targetY - viewH / 2, viewH, mapH));

    private static int ClampAxis(int cam, int view, int map)
    {
        if (map <= view)
            return -(view - map) / 2; // map narrower than the view → center it
        return Math.Clamp(cam, 0, map - view);
    }
}
