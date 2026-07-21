using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16C input table: bindings, rebinding rules, device merge, direction
/// resolution, and disconnect.</summary>
public sealed class InputBindingsTests
{
    private static InputBindings Defaults() => InputBindings.Defaults();

    [Fact]
    public void KeyboardDefaults_MatchTheSpecifiedKeys()
    {
        InputBindings b = Defaults();
        Assert.Equal(["Up", "W"], b.For(InputDevice.Keyboard, GameAction.Up));
        Assert.Equal(["Enter", "Z"], b.For(InputDevice.Keyboard, GameAction.Confirm));
        Assert.Equal(["Escape", "X"], b.For(InputDevice.Keyboard, GameAction.Cancel));
        Assert.Equal(["C"], b.For(InputDevice.Keyboard, GameAction.Menu));
        Assert.Equal(["ShiftLeft"], b.For(InputDevice.Keyboard, GameAction.Run));
        Assert.Equal(["F3"], b.For(InputDevice.Keyboard, GameAction.DebugToggle));
    }

    [Fact]
    public void GamepadDefaults_MatchTheSpecifiedControls()
    {
        InputBindings b = Defaults();
        Assert.Equal(["FaceSouth"], b.For(InputDevice.Gamepad, GameAction.Confirm));
        Assert.Equal(["FaceEast"], b.For(InputDevice.Gamepad, GameAction.Cancel));
        Assert.Equal(["Start"], b.For(InputDevice.Gamepad, GameAction.Menu));
        Assert.Equal(["FaceWest"], b.For(InputDevice.Gamepad, GameAction.Run));
        Assert.Equal(["Back"], b.For(InputDevice.Gamepad, GameAction.DebugToggle));
        Assert.Equal(0.5, InputBindings.StickDeadzone);
    }

    [Fact]
    public void EveryActionIsBoundOnBothDevices()
    {
        InputBindings b = Defaults();
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            Assert.NotEmpty(b.For(InputDevice.Keyboard, action));
            Assert.NotEmpty(b.For(InputDevice.Gamepad, action));
        }
    }

    [Fact]
    public void ActionFor_ResolvesBoundInputsAndIgnoresUnknownOnes()
    {
        InputBindings b = Defaults();
        Assert.Equal(GameAction.Confirm, b.ActionFor(InputDevice.Keyboard, "Enter"));
        Assert.Equal(GameAction.Up, b.ActionFor(InputDevice.Keyboard, "W"));
        Assert.Null(b.ActionFor(InputDevice.Keyboard, "Q"));
    }

    [Fact]
    public void Rebind_AssignsAFreeInput()
    {
        Assert.True(Defaults().TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Q"], false,
            out InputBindings next, out string? error));
        Assert.Null(error);
        Assert.Equal(["Q"], next.For(InputDevice.Keyboard, GameAction.Menu));
        Assert.Null(next.ActionFor(InputDevice.Keyboard, "C"));
    }

    [Fact]
    public void Rebind_RejectsADuplicateUnlessSwapIsRequested()
    {
        InputBindings b = Defaults();
        Assert.False(b.TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Enter"], false, out _, out string? error));
        Assert.Contains("already bound to Confirm", error);

        Assert.True(b.TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Enter"], true,
            out InputBindings swapped, out _));
        Assert.Equal(GameAction.Menu, swapped.ActionFor(InputDevice.Keyboard, "Enter"));
        Assert.Equal(["Z"], swapped.For(InputDevice.Keyboard, GameAction.Confirm));   // kept its recovery
    }

    /// <summary>Swapping away every Confirm default would lock the player out of the options menu.</summary>
    [Fact]
    public void Rebind_RefusesToStripConfirmOrCancelOfEveryRecoveryDefault()
    {
        InputBindings b = Defaults();
        Assert.False(b.TryRebind(InputDevice.Keyboard, GameAction.Confirm, ["Q"], true, out _, out string? error));
        Assert.Contains("recovery", error);

        Assert.False(b.TryRebind(InputDevice.Keyboard, GameAction.Cancel, ["Q"], true, out _, out error));
        Assert.Contains("recovery", error);
    }

    [Fact]
    public void Rebind_KeepsConfirmWhenOneDefaultSurvives()
    {
        Assert.True(Defaults().TryRebind(InputDevice.Keyboard, GameAction.Confirm, ["Enter", "Q"], false,
            out InputBindings next, out _));
        Assert.Equal(["Enter", "Q"], next.For(InputDevice.Keyboard, GameAction.Confirm));
    }

    /// <summary>A swap that empties another action is refused: no action may end up unbound.</summary>
    [Fact]
    public void Rebind_RefusesToLeaveAnotherActionUnbound()
    {
        InputBindings b = Defaults();
        Assert.False(b.TryRebind(InputDevice.Keyboard, GameAction.Run, ["C"], true, out _, out string? error));
        Assert.Contains("unbound", error);
    }

    [Fact]
    public void Rebind_RejectsEmptyOrBlankInputLists()
    {
        InputBindings b = Defaults();
        foreach (string[] inputs in new[] { [], new[] { "" }, new[] { "  " } })
            Assert.False(b.TryRebind(InputDevice.Keyboard, GameAction.Menu, inputs, false, out _, out _));
    }

    [Fact]
    public void Rebind_RejectsTheSameInputListedTwice()
    {
        Assert.False(Defaults().TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Q", "Q"], false,
            out _, out string? error));
        Assert.Contains("twice", error);
    }

    [Fact]
    public void Rebind_IsPerDeviceAndLeavesTheOriginalUnchanged()
    {
        InputBindings b = Defaults();
        b.TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Q"], false, out InputBindings next, out _);

        Assert.Equal(["C"], b.For(InputDevice.Keyboard, GameAction.Menu));         // immutable original
        Assert.Equal(["Start"], next.For(InputDevice.Gamepad, GameAction.Menu));   // other device intact
    }

    // --- Serialization round trip -------------------------------------------------

    [Fact]
    public void ToMapAndBack_RoundTripsEveryBinding()
    {
        InputBindings b = Defaults();
        Assert.True(InputBindings.TryFromMaps(b.ToMap(InputDevice.Keyboard), b.ToMap(InputDevice.Gamepad),
            out InputBindings restored, out string? warning));
        Assert.Null(warning);
        foreach (GameAction action in Enum.GetValues<GameAction>())
            Assert.Equal(b.For(InputDevice.Keyboard, action), restored.For(InputDevice.Keyboard, action));
    }

    [Fact]
    public void MissingActions_KeepTheirDefaults()
    {
        Assert.True(InputBindings.TryFromMaps(
            new Dictionary<string, IReadOnlyList<string>> { ["Menu"] = ["Q"] }, null,
            out InputBindings result, out _));
        Assert.Equal(["Q"], result.For(InputDevice.Keyboard, GameAction.Menu));
        Assert.Equal(["Enter", "Z"], result.For(InputDevice.Keyboard, GameAction.Confirm));
    }

    [Fact]
    public void UnknownActionsAndEmptyLists_AreIgnoredWithOneWarning()
    {
        Assert.True(InputBindings.TryFromMaps(new Dictionary<string, IReadOnlyList<string>>
        {
            ["Teleport"] = ["Q"],
            ["Menu"] = [],
        }, null, out InputBindings result, out string? warning));

        Assert.Contains("Teleport", warning);
        Assert.Equal(["C"], result.For(InputDevice.Keyboard, GameAction.Menu));
    }

    /// <summary>A duplicate makes the profile ambiguous, so the whole thing falls back to defaults
    /// rather than partially guessing which action wins.</summary>
    [Fact]
    public void DuplicateBindingsAcrossActions_FallBackToDefaults()
    {
        Assert.False(InputBindings.TryFromMaps(new Dictionary<string, IReadOnlyList<string>>
        {
            ["Menu"] = ["Q"],
            ["Run"] = ["Q"],
        }, null, out InputBindings result, out string? warning));

        Assert.Contains("both", warning);
        Assert.Equal(["C"], result.For(InputDevice.Keyboard, GameAction.Menu));
    }
}

public sealed class InputMergerTests
{
    private static InputMerger Merger(params GameAction[] keyboard)
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, keyboard);
        return merger;
    }

    [Fact]
    public void DevicesMergeByAction()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Gamepad, [GameAction.Confirm]);

        Assert.Contains(GameAction.Up, merger.Held);
        Assert.Contains(GameAction.Confirm, merger.Held);
    }

    [Fact]
    public void SameActionOnBothDevices_IsHeldOnce()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Gamepad, [GameAction.Up]);
        Assert.Single(merger.Held);
    }

    [Fact]
    public void NoDirectionHeld_ResolvesToNull() => Assert.Null(new InputMerger().Direction());

    [Theory]
    [InlineData(GameAction.Up)]
    [InlineData(GameAction.Down)]
    [InlineData(GameAction.Left)]
    [InlineData(GameAction.Right)]
    public void ASingleDirection_ResolvesToItself(GameAction direction) =>
        Assert.Equal(direction, Merger(direction).Direction());

    [Fact]
    public void OppositeDirectionsOnOneAxis_Cancel()
    {
        Assert.Null(Merger(GameAction.Up, GameAction.Down).Direction());
        Assert.Null(Merger(GameAction.Left, GameAction.Right).Direction());
    }

    /// <summary>With one axis cancelled, the other still resolves.</summary>
    [Fact]
    public void OneCancelledAxis_LeavesTheOtherActive() =>
        Assert.Equal(GameAction.Left, Merger(GameAction.Up, GameAction.Down, GameAction.Left).Direction());

    [Fact]
    public void PerpendicularDirections_MostRecentlyPressedWins()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Keyboard, [GameAction.Up, GameAction.Right]);
        Assert.Equal(GameAction.Right, merger.Direction());

        merger.Observe(InputDevice.Keyboard, [GameAction.Right]);
        merger.Observe(InputDevice.Keyboard, [GameAction.Right, GameAction.Down]);
        Assert.Equal(GameAction.Down, merger.Direction());
    }

    /// <summary>Pressed in the same poll, ordinal order decides, so replays never depend on set
    /// iteration order.</summary>
    [Fact]
    public void SameFramePerpendicularTie_BreaksOrdinally()
    {
        Assert.Equal(GameAction.Up, Merger(GameAction.Up, GameAction.Right).Direction());
        Assert.Equal(GameAction.Up, Merger(GameAction.Left, GameAction.Up).Direction());
        Assert.Equal(GameAction.Down, Merger(GameAction.Right, GameAction.Down).Direction());
    }

    [Fact]
    public void ReleasingTheRecentDirection_FallsBackToTheHeldOne()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Keyboard, [GameAction.Up, GameAction.Right]);
        Assert.Equal(GameAction.Right, merger.Direction());

        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        Assert.Equal(GameAction.Up, merger.Direction());
    }

    [Fact]
    public void RepressingADirection_RefreshesItsRecency()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up, GameAction.Right]);
        merger.Observe(InputDevice.Keyboard, [GameAction.Right]);        // release Up
        merger.Observe(InputDevice.Keyboard, [GameAction.Right, GameAction.Up]); // press Up again
        Assert.Equal(GameAction.Up, merger.Direction());
    }

    [Fact]
    public void Disconnect_ReleasesThatDevicesHeldActionsOnly()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Gamepad, [GameAction.Right, GameAction.Confirm]);

        merger.Disconnect(InputDevice.Gamepad);
        Assert.Equal(GameAction.Up, merger.Direction());
        Assert.DoesNotContain(GameAction.Confirm, merger.Held);
        Assert.Contains(GameAction.Up, merger.Held);
    }

    [Fact]
    public void DirectionsFromDifferentDevices_StillResolve()
    {
        var merger = new InputMerger();
        merger.Observe(InputDevice.Keyboard, [GameAction.Up]);
        merger.Observe(InputDevice.Gamepad, [GameAction.Down]);
        Assert.Null(merger.Direction());   // opposites cancel across devices too
    }

    [Fact]
    public void NullHeldSet_IsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new InputMerger().Observe(InputDevice.Keyboard, null!));
}

public sealed class RuntimeOptionsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cgm-options").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string json) => File.WriteAllText(Path.Combine(_dir, RuntimeOptionsFileStore.FileName), json);

    [Fact]
    public void MissingFile_IsFirstRunNotAFailure()
    {
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Null(warning);
        Assert.Equal(100, options.MusicVolume);
        Assert.Equal(["Enter", "Z"], options.Bindings.For(InputDevice.Keyboard, GameAction.Confirm));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        RuntimeOptions original = RuntimeOptions.Defaults() with { MusicVolume = 40, SfxVolume = 0 };
        RuntimeOptionsFileStore.Save(_dir, original);

        RuntimeOptions loaded = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Null(warning);
        Assert.Equal(40, loaded.MusicVolume);
        Assert.Equal(0, loaded.SfxVolume);
        foreach (GameAction action in Enum.GetValues<GameAction>())
            Assert.Equal(original.Bindings.For(InputDevice.Gamepad, action),
                loaded.Bindings.For(InputDevice.Gamepad, action));
    }

    [Fact]
    public void RebindingSurvivesARoundTrip()
    {
        RuntimeOptions.Defaults().Bindings.TryRebind(InputDevice.Keyboard, GameAction.Menu, ["Q"], false,
            out InputBindings rebound, out _);
        RuntimeOptionsFileStore.Save(_dir, RuntimeOptions.Defaults() with { Bindings = rebound });

        RuntimeOptions loaded = RuntimeOptionsFileStore.Load(_dir, out _);
        Assert.Equal(["Q"], loaded.Bindings.For(InputDevice.Keyboard, GameAction.Menu));
    }

    [Fact]
    public void CorruptJson_FallsBackToDefaultsWithAWarning()
    {
        Write("{ not json");
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Contains("unreadable", warning);
        Assert.Equal(100, options.MusicVolume);
    }

    [Fact]
    public void NewerVersion_FallsBackToDefaultsRatherThanGuessing()
    {
        Write("""{ "optionsVersion": 99, "musicVolume": 10 }""");
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Contains("newer", warning);
        Assert.Equal(100, options.MusicVolume);
    }

    [Fact]
    public void OlderVersion_LoadsOnBestEffortWithAWarning()
    {
        Write("""{ "optionsVersion": 0, "musicVolume": 25 }""");
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Contains("older", warning);
        Assert.Equal(25, options.MusicVolume);
    }

    [Fact]
    public void DuplicateBindings_FallBackToDefaults()
    {
        Write("""
        { "optionsVersion": 1,
          "keyboardBindings": { "Menu": ["Q"], "Run": ["Q"] } }
        """);
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Contains("both", warning);
        Assert.Equal(["C"], options.Bindings.For(InputDevice.Keyboard, GameAction.Menu));
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(500, 100)]
    public void VolumesAreClampedOnLoad(int written, int expected)
    {
        Write($$"""{ "optionsVersion": 1, "musicVolume": {{written}} }""");
        Assert.Equal(expected, RuntimeOptionsFileStore.Load(_dir, out _).MusicVolume);
    }

    [Fact]
    public void EmptyFile_FallsBackToDefaults()
    {
        Write("null");
        RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out string? warning);
        Assert.Contains("empty", warning);
        Assert.Equal(100, options.SfxVolume);
    }

    /// <summary>Bad options must never stop the game from starting.</summary>
    [Fact]
    public void EveryFailureModeStillYieldsUsableBindings()
    {
        foreach (string json in new[] { "{ not json", """{ "optionsVersion": 99 }""", "null", "{}" })
        {
            Write(json);
            RuntimeOptions options = RuntimeOptionsFileStore.Load(_dir, out _);
            Assert.NotEmpty(options.Bindings.For(InputDevice.Keyboard, GameAction.Confirm));
            Assert.NotEmpty(options.Bindings.For(InputDevice.Keyboard, GameAction.Cancel));
        }
    }
}
