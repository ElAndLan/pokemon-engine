namespace Cgm.Runtime.Engine;

/// <summary>The scaled, letterboxed render area within the window (ENGINE_RUNTIME_SPEC).</summary>
public readonly record struct Viewport(int Scale, int OffsetX, int OffsetY, int Width, int Height);

/// <summary>Integer-scales a fixed virtual resolution to fit the window, centered and letterboxed.</summary>
public static class VirtualResolution
{
    public static Viewport Fit(int windowW, int windowH, int virtualW, int virtualH)
    {
        if (virtualW <= 0 || virtualH <= 0)
            throw new ArgumentOutOfRangeException(nameof(virtualW), "Virtual size must be positive.");

        int scale = Math.Max(1, Math.Min(windowW / virtualW, windowH / virtualH));
        int w = virtualW * scale, h = virtualH * scale;
        return new Viewport(scale, (windowW - w) / 2, (windowH - h) / 2, w, h);
    }
}
