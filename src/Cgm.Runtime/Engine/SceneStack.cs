namespace Cgm.Runtime.Engine;

/// <summary>A scene owns its transient presentation state; shared content, renderer, and audio
/// leases are borrowed and outlive it (ENGINE_RUNTIME_SPEC 16C).</summary>
public interface IScene : IDisposable
{
    /// <summary>When true, the scene below still renders beneath this one.</summary>
    bool IsOverlay { get; }

    /// <summary>Runs once after the scene becomes stack-owned, before its first update or render.</summary>
    void Enter();

    /// <summary>Fixed-tick update. Only the top scene receives this, and only outside a transition.</summary>
    void Update(TickInput input);

    void Render();

    /// <summary>Runs once when popped, replaced, or shut down — before <see cref="IDisposable.Dispose"/>.</summary>
    void Exit();
}

/// <summary>Queued stack mutation. Mutations requested during a scene update are applied after that
/// tick, so a callback can never re-enter a lifecycle method.</summary>
internal readonly record struct SceneChange(SceneChangeKind Op, IScene? Scene, bool Fade);

internal enum SceneChangeKind { Push, Pop, Replace }

/// <summary>The runtime scene stack with lifecycle, overlay rendering, deferred mutation, and fade
/// transitions (ENGINE_RUNTIME_SPEC 16C). Platform-free, so the whole flow is testable headless.</summary>
public sealed class SceneStack : IDisposable
{
    /// <summary>Ticks to fade out, and again to fade in, around a transitioned switch.</summary>
    public const int FadeTicks = 15;

    private readonly List<IScene> _scenes = [];
    private readonly Queue<SceneChange> _pending = new();
    private int _fadeElapsed;
    private bool _fading;
    private bool _switched;
    private bool _disposed;

    public int Count => _scenes.Count;

    public IScene? Active => _scenes.Count > 0 ? _scenes[^1] : null;

    /// <summary>True while a fade is running. Input is blocked and no scene updates.</summary>
    public bool IsTransitioning => _fading;

    /// <summary>Fade opacity in [0,1]: 0 fully visible, 1 fully covered.</summary>
    public double FadeAlpha { get; private set; }

    public void Push(IScene scene, bool fade = false) => Enqueue(SceneChangeKind.Push, scene, fade);

    public void Pop(bool fade = false) => Enqueue(SceneChangeKind.Pop, null, fade);

    public void Replace(IScene scene, bool fade = true) => Enqueue(SceneChangeKind.Replace, scene, fade);

    /// <summary>Advances one fixed tick: update the top scene, then apply queued mutations. During a
    /// transition the fade advances instead and the switch happens at its midpoint.</summary>
    public void Tick(TickInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fading)
        {
            AdvanceFade();
            return;
        }

        Active?.Update(input);
        ApplyPending();
    }

    /// <summary>Renders from the highest scene that is not an overlay, upward. Scenes below that
    /// point are covered and are not rendered.</summary>
    public void Render()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int i = FirstVisible(); i < _scenes.Count; i++)
            _scenes[i].Render();
    }

    /// <summary>The scenes <see cref="Render"/> would draw, bottom to top.</summary>
    public IReadOnlyList<IScene> RenderOrder() => _scenes.Skip(FirstVisible()).ToArray();

    /// <summary>Exits and disposes every scene, top first, and drops queued mutations.</summary>
    public void Shutdown()
    {
        // Pending scenes never entered the stack, so they are disposed without Exit.
        foreach (SceneChange change in _pending)
            change.Scene?.Dispose();
        _pending.Clear();

        for (int i = _scenes.Count - 1; i >= 0; i--)
            Retire(_scenes[i]);
        _scenes.Clear();
        _fading = false;
        FadeAlpha = 0.0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Shutdown();
    }

    private int FirstVisible()
    {
        int i = _scenes.Count - 1;
        while (i > 0 && _scenes[i].IsOverlay)
            i--;
        return Math.Max(0, i);
    }

    private void Enqueue(SceneChangeKind op, IScene? scene, bool fade)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (op != SceneChangeKind.Pop)
            ArgumentNullException.ThrowIfNull(scene);
        _pending.Enqueue(new SceneChange(op, scene, fade));
        if (fade && !_fading)
        {
            _fading = true;
            _fadeElapsed = 0;
            _switched = false;
            FadeAlpha = 0.0;
        }
    }

    private void AdvanceFade()
    {
        _fadeElapsed++;
        if (_fadeElapsed <= FadeTicks)
        {
            FadeAlpha = (double)_fadeElapsed / FadeTicks;
            return;
        }

        if (!_switched)
        {
            // Fully covered: the switch and any loading happen here, between ticks, never in Render.
            FadeAlpha = 1.0;
            ApplyPending();
            _switched = true;
            return;
        }

        int inTicks = _fadeElapsed - FadeTicks - 1;
        FadeAlpha = Math.Max(0.0, 1.0 - (double)inTicks / FadeTicks);
        if (inTicks >= FadeTicks)
        {
            _fading = false;
            FadeAlpha = 0.0;
        }
    }

    private void ApplyPending()
    {
        while (_pending.Count > 0)
        {
            SceneChange change = _pending.Dequeue();
            switch (change.Op)
            {
                case SceneChangeKind.Push:
                    _scenes.Add(change.Scene!);
                    change.Scene!.Enter();
                    break;

                case SceneChangeKind.Pop:
                    if (_scenes.Count == 0)
                        break;
                    IScene popped = _scenes[^1];
                    _scenes.RemoveAt(_scenes.Count - 1);
                    Retire(popped);
                    break;

                case SceneChangeKind.Replace:
                    if (_scenes.Count > 0)
                    {
                        IScene replaced = _scenes[^1];
                        _scenes.RemoveAt(_scenes.Count - 1);
                        Retire(replaced);
                    }
                    _scenes.Add(change.Scene!);
                    change.Scene!.Enter();
                    break;
            }
        }
    }

    /// <summary>Exit then Dispose, in that order. A throwing Exit must not leak the scene.</summary>
    private static void Retire(IScene scene)
    {
        try
        {
            scene.Exit();
        }
        finally
        {
            scene.Dispose();
        }
    }
}
