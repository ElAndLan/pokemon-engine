using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16C scene-stack contract: lifecycle order, overlay rendering,
/// deferred mutation, and fade transitions.</summary>
public sealed class SceneStackTests
{
    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    /// <summary>Records its lifecycle into a shared log so ordering across scenes is observable.</summary>
    private sealed class Probe(string name, List<string> log, bool overlay = false) : IScene
    {
        public bool IsOverlay => overlay;
        public int Updates { get; private set; }
        public int Renders { get; private set; }
        public Action<Probe>? OnUpdate { get; init; }

        public void Enter() => log.Add($"{name}:enter");

        public void Update(TickInput input)
        {
            Updates++;
            log.Add($"{name}:update");
            OnUpdate?.Invoke(this);
        }

        public void Render()
        {
            Renders++;
            log.Add($"{name}:render");
        }

        public void Exit() => log.Add($"{name}:exit");

        public void Dispose() => log.Add($"{name}:dispose");
    }

    private static (SceneStack Stack, List<string> Log) New()
    {
        var log = new List<string>();
        return (new SceneStack(), log);
    }

    private static void Ticks(SceneStack stack, int count)
    {
        for (int i = 0; i < count; i++)
            stack.Tick(Idle);
    }

    // --- Lifecycle ---------------------------------------------------------------

    [Fact]
    public void Push_EntersOnceBeforeFirstUpdateOrRender()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Assert.Null(stack.Active);          // queued, not yet applied

        stack.Tick(Idle);                   // applied after the tick
        stack.Render();
        Assert.Equal(["a:enter", "a:render"], log);
    }

    [Fact]
    public void Pop_ExitsThenDisposes()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Ticks(stack, 1);
        log.Clear();

        stack.Pop();
        Ticks(stack, 1);
        // The scene still receives that tick's update; the pop applies after it.
        Assert.Equal(["a:update", "a:exit", "a:dispose"], log);
        Assert.Null(stack.Active);
    }

    [Fact]
    public void Replace_RetiresTheOldSceneBeforeEnteringTheNew()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Ticks(stack, 1);
        log.Clear();

        stack.Replace(new Probe("b", log), fade: false);
        Ticks(stack, 1);
        Assert.Equal(["a:update", "a:exit", "a:dispose", "b:enter"], log);
        Assert.Equal(1, stack.Count);
    }

    /// <summary>Covering a scene must not call Exit: it is still owned and resumes on pop.</summary>
    [Fact]
    public void CoveringAScene_DoesNotExitIt()
    {
        var (stack, log) = New();
        stack.Push(new Probe("base", log));
        Ticks(stack, 1);
        stack.Push(new Probe("menu", log, overlay: true));
        Ticks(stack, 1);

        Assert.DoesNotContain("base:exit", log);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void OnlyTheTopSceneUpdates()
    {
        var (stack, log) = New();
        var bottom = new Probe("base", log);
        var top = new Probe("menu", log, overlay: true);
        stack.Push(bottom);
        Ticks(stack, 1);
        stack.Push(top);
        Ticks(stack, 3);

        Assert.Equal(1, bottom.Updates);   // only the tick before the menu arrived
        Assert.Equal(2, top.Updates);
    }

    // --- Overlay rendering -------------------------------------------------------

    [Fact]
    public void OverlayRendersOverTheSceneBelow()
    {
        var (stack, log) = New();
        stack.Push(new Probe("base", log));
        Ticks(stack, 1);
        stack.Push(new Probe("menu", log, overlay: true));
        Ticks(stack, 1);
        log.Clear();

        stack.Render();
        Assert.Equal(["base:render", "menu:render"], log);
    }

    [Fact]
    public void NonOverlayHidesEverythingBelow()
    {
        var (stack, log) = New();
        stack.Push(new Probe("base", log));
        Ticks(stack, 1);
        stack.Push(new Probe("battle", log));   // opaque
        Ticks(stack, 1);
        log.Clear();

        stack.Render();
        Assert.Equal(["battle:render"], log);
    }

    [Fact]
    public void StackedOverlaysAllRenderAboveTheNearestOpaqueScene()
    {
        var (stack, log) = New();
        stack.Push(new Probe("base", log));
        Ticks(stack, 1);
        stack.Push(new Probe("menu", log, overlay: true));
        Ticks(stack, 1);
        stack.Push(new Probe("prompt", log, overlay: true));
        Ticks(stack, 1);
        log.Clear();

        stack.Render();
        Assert.Equal(["base:render", "menu:render", "prompt:render"], log);
        Assert.Equal(3, stack.RenderOrder().Count);
    }

    /// <summary>An overlay at the very bottom still renders; there is nothing beneath it to reveal.</summary>
    [Fact]
    public void OverlayAtTheBaseStillRenders()
    {
        var (stack, log) = New();
        stack.Push(new Probe("only", log, overlay: true));
        Ticks(stack, 1);
        log.Clear();

        stack.Render();
        Assert.Equal(["only:render"], log);
    }

    [Fact]
    public void EmptyStack_RendersAndTicksWithoutThrowing()
    {
        var (stack, _) = New();
        stack.Tick(Idle);
        stack.Render();
        Assert.Empty(stack.RenderOrder());
    }

    // --- Deferred mutation -------------------------------------------------------

    /// <summary>A scene pushing from inside Update must not re-enter lifecycle mid-tick.</summary>
    [Fact]
    public void MutationDuringUpdate_AppliesAfterThatTick()
    {
        var (stack, log) = New();
        SceneStack captured = stack;
        var pusher = new Probe("a", log) { OnUpdate = _ => captured.Push(new Probe("b", log)) };
        stack.Push(pusher);
        Ticks(stack, 1);
        log.Clear();

        stack.Tick(Idle);
        // a updates, then b enters — never b:enter in the middle of a:update.
        Assert.Equal(["a:update", "b:enter"], log);
    }

    [Fact]
    public void QueuedMutations_ApplyInRequestOrder()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        stack.Push(new Probe("b", log));
        stack.Pop();
        Ticks(stack, 1);

        Assert.Equal(["a:enter", "b:enter", "b:exit", "b:dispose"], log);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void PopOnEmptyStack_IsIgnored()
    {
        var (stack, log) = New();
        stack.Pop();
        Ticks(stack, 1);
        Assert.Empty(log);
        Assert.Equal(0, stack.Count);
    }

    [Fact]
    public void ReplaceOnEmptyStack_Pushes()
    {
        var (stack, log) = New();
        stack.Replace(new Probe("title", log), fade: false);
        Ticks(stack, 1);
        Assert.Equal(["title:enter"], log);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void NullScene_IsRejectedAtRequestTime()
    {
        var (stack, _) = New();
        Assert.Throws<ArgumentNullException>(() => stack.Push(null!));
        Assert.Throws<ArgumentNullException>(() => stack.Replace(null!));
    }

    // --- Transitions -------------------------------------------------------------

    [Fact]
    public void FadedReplace_SwitchesAtFullCoverAndRunsThirtyOneTicks()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Ticks(stack, 1);
        log.Clear();

        stack.Replace(new Probe("b", log));
        Assert.True(stack.IsTransitioning);

        Ticks(stack, SceneStack.FadeTicks);           // fade out
        Assert.Equal(1.0, stack.FadeAlpha, 6);
        Assert.Empty(log);                            // nothing switched yet

        stack.Tick(Idle);                             // midpoint: the switch happens here
        Assert.Equal(["a:exit", "a:dispose", "b:enter"], log);

        Ticks(stack, SceneStack.FadeTicks);           // fade in
        Assert.False(stack.IsTransitioning);
        Assert.Equal(0.0, stack.FadeAlpha, 6);
    }

    [Fact]
    public void DuringTransition_ScenesDoNotUpdate()
    {
        var (stack, log) = New();
        var a = new Probe("a", log);
        stack.Push(a);
        Ticks(stack, 1);
        int before = a.Updates;

        stack.Replace(new Probe("b", log));
        Ticks(stack, 5);
        Assert.Equal(before, a.Updates);   // input is blocked while fading
    }

    [Fact]
    public void FadeAlpha_RisesThenFallsMonotonically()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Ticks(stack, 1);
        stack.Replace(new Probe("b", log));

        var samples = new List<double>();
        for (int i = 0; i < SceneStack.FadeTicks * 2 + 2; i++)
        {
            stack.Tick(Idle);
            samples.Add(stack.FadeAlpha);
        }

        Assert.All(samples, a => Assert.InRange(a, 0.0, 1.0));
        Assert.Equal(1.0, samples.Max(), 6);
        Assert.Equal(0.0, samples[^1], 6);
    }

    [Fact]
    public void UnfadedMutation_AppliesImmediatelyWithoutATransition()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log), fade: false);
        Assert.False(stack.IsTransitioning);
        Ticks(stack, 1);
        Assert.Equal(["a:enter"], log);
    }

    // --- Shutdown ----------------------------------------------------------------

    [Fact]
    public void Shutdown_RetiresEverySceneTopFirst()
    {
        var (stack, log) = New();
        stack.Push(new Probe("base", log));
        Ticks(stack, 1);
        stack.Push(new Probe("menu", log, overlay: true));
        Ticks(stack, 1);
        log.Clear();

        stack.Shutdown();
        Assert.Equal(["menu:exit", "menu:dispose", "base:exit", "base:dispose"], log);
        Assert.Equal(0, stack.Count);
    }

    /// <summary>A scene queued but never entered is disposed without Exit, since Enter never ran.</summary>
    [Fact]
    public void Shutdown_DisposesQueuedScenesWithoutExiting()
    {
        var (stack, log) = New();
        stack.Push(new Probe("queued", log));
        stack.Shutdown();
        Assert.Equal(["queued:dispose"], log);
    }

    [Fact]
    public void Dispose_IsIdempotentAndBlocksFurtherUse()
    {
        var (stack, log) = New();
        stack.Push(new Probe("a", log));
        Ticks(stack, 1);

        stack.Dispose();
        stack.Dispose();
        // The single tick applied the queued push; the scene never got an update before shutdown.
        Assert.Equal(["a:enter", "a:exit", "a:dispose"], log);
        Assert.Throws<ObjectDisposedException>(() => stack.Tick(Idle));
        Assert.Throws<ObjectDisposedException>(() => stack.Render());
        Assert.Throws<ObjectDisposedException>(() => stack.Pop());
    }

    /// <summary>A scene whose Exit throws must still be disposed, or its resources leak.</summary>
    [Fact]
    public void ThrowingExit_StillDisposesTheScene()
    {
        var log = new List<string>();
        var stack = new SceneStack();
        stack.Push(new ThrowingScene(log));
        stack.Tick(Idle);

        Assert.Throws<InvalidOperationException>(stack.Shutdown);
        Assert.Contains("dispose", log);
    }

    private sealed class ThrowingScene(List<string> log) : IScene
    {
        public bool IsOverlay => false;
        public void Enter() { }
        public void Update(TickInput input) { }
        public void Render() { }
        public void Exit() => throw new InvalidOperationException("exit failed");
        public void Dispose() => log.Add("dispose");
    }
}
