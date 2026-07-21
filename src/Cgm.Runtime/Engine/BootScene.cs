namespace Cgm.Runtime.Engine;

/// <summary>The first scene the host enters once content is validated (ENGINE_RUNTIME_SPEC 16A
/// exit). It borrows the renderer and content, owns one neutral placeholder texture, and draws a
/// flat field until 16C's Title scene replaces it. It reads no content IDs.</summary>
public sealed class BootScene(IRenderer renderer, QuadBatch batch, int virtualWidth, int virtualHeight) : IScene
{
    private TextureHandle _blank;
    private bool _entered;

    public bool IsOverlay => false;

    public void Enter()
    {
        // A 2x2 opaque texture proves upload and sampling without depending on any asset.
        _blank = renderer.CreateTexture(2, 2,
        [
            0x30, 0x40, 0x50, 0xFF, 0x50, 0x40, 0x30, 0xFF,
            0x50, 0x40, 0x30, 0xFF, 0x30, 0x40, 0x50, 0xFF,
        ]);
        _entered = true;
    }

    public void Update(TickInput input) { }

    public void Render()
    {
        if (!_entered)
            return;
        batch.Begin();
        batch.Ui(_blank, new RectI(0, 0, 2, 2), new RectI(0, 0, virtualWidth, virtualHeight), layer: 0);
        var (quads, calls, _) = batch.End();
        renderer.Draw(quads, calls);
    }

    public void Exit() { }

    public void Dispose()
    {
        if (!_entered)
            return;
        renderer.DisposeTexture(_blank);
        _entered = false;
    }
}
