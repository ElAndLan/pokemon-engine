using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

public sealed class BootArgsTests
{
    private static BootArgs Ok(params string[] args)
    {
        Assert.True(BootArgs.TryParse(args, out BootArgs parsed, out BootDiagnostic? error), error?.Summary);
        Assert.Null(error);
        return parsed;
    }

    private static BootDiagnostic Rejected(params string[] args)
    {
        Assert.False(BootArgs.TryParse(args, out _, out BootDiagnostic? error));
        BootDiagnostic diagnostic = Assert.IsType<BootDiagnostic>(error);
        Assert.Equal(RuntimeExit.Arguments, diagnostic.Exit);
        return diagnostic;
    }

    [Fact]
    public void Exported_ModeIsTheAbsenceOfProject()
    {
        BootArgs args = Ok();
        Assert.True(args.Exported);
        Assert.Null(args.ProjectPath);
        Assert.False(args.Debug);
        Assert.False(args.Smoke);
        Assert.Null(args.Spawn);
    }

    [Fact]
    public void Project_RelativePathResolvesAgainstWorkingDirectory()
    {
        BootArgs args = Ok("--project", "game");
        Assert.False(args.Exported);
        Assert.Equal(Path.GetFullPath("game"), args.ProjectPath);
    }

    [Theory]
    [InlineData("--debug")]
    [InlineData("--smoke")]
    public void BoolFlags_AreAcceptedInEitherMode(string flag)
    {
        Assert.True(Ok(flag) is { });
        Assert.True(Ok("--project", "g", flag) is { });
    }

    [Fact]
    public void Smoke_CombinesWithBothDataModes()
    {
        Assert.True(Ok("--smoke").Smoke);
        Assert.True(Ok("--project", "g", "--smoke", "--debug").Smoke);
    }

    [Fact]
    public void Unknown_ArgumentIsRejected() =>
        Assert.Contains("--config", Rejected("--config", "x").Summary);

    [Theory]
    [InlineData("--debug", "--debug")]
    [InlineData("--smoke", "--smoke")]
    public void Duplicate_BoolFlagIsRejected(string first, string second) =>
        Assert.Contains("Duplicate", Rejected(first, second).Summary);

    [Fact]
    public void Duplicate_ValueFlagIsRejected() =>
        Assert.Contains("Duplicate", Rejected("--project", "a", "--project", "b").Summary);

    [Fact]
    public void MissingValue_AtEndOfArgumentsIsRejected() =>
        Assert.Contains("requires a value", Rejected("--project").Summary);

    [Fact]
    public void MissingValue_FollowedByAnotherFlagIsRejected()
    {
        // "--project --debug" must not silently consume "--debug" as the project path.
        Assert.Contains("requires a value", Rejected("--project", "--debug").Summary);
        Assert.Contains("requires a value", Rejected("--spawn-map", "--spawn-x").Summary);
    }

    [Fact]
    public void EmptyProjectPath_IsRejected() =>
        Assert.Contains("non-empty", Rejected("--project", "   ").Summary);

    [Fact]
    public void Spawn_FullSetIsAcceptedWithProjectAndDebug()
    {
        SpawnOverride spawn = Ok("--project", "g", "--debug",
            "--spawn-map", "map:town", "--spawn-x", "3", "--spawn-y", "4").Spawn!;
        Assert.Equal(EntityId.Parse("map:town"), spawn.Map);
        Assert.Equal(3, spawn.X);
        Assert.Equal(4, spawn.Y);
        Assert.Null(spawn.Facing);
    }

    [Theory]
    [InlineData("down", Facing.Down)]
    [InlineData("UP", Facing.Up)]
    [InlineData("Left", Facing.Left)]
    [InlineData("right", Facing.Right)]
    public void SpawnFacing_IsCaseInsensitive(string text, Facing expected)
    {
        SpawnOverride spawn = Ok("--project", "g", "--debug", "--spawn-map", "map:t",
            "--spawn-x", "0", "--spawn-y", "0", "--spawn-facing", text).Spawn!;
        Assert.Equal(expected, spawn.Facing);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("diagonal")]
    [InlineData("")]
    public void SpawnFacing_RejectsNonDirectionIncludingNumericEnumText(string text) =>
        Assert.Contains("down, up, left, or right", Rejected("--project", "g", "--debug",
            "--spawn-map", "map:t", "--spawn-x", "0", "--spawn-y", "0", "--spawn-facing", text).Summary);

    [Theory]
    [InlineData("--spawn-map", "map:t")]
    [InlineData("--spawn-x", "1")]
    [InlineData("--spawn-y", "1")]
    public void Spawn_IsAllOrNone(string flag, string value) =>
        Assert.Contains("all of", Rejected("--project", "g", "--debug", flag, value).Summary);

    [Fact]
    public void SpawnFacing_AloneRequiresTheFullSet() =>
        Assert.Contains("full spawn argument set",
            Rejected("--project", "g", "--debug", "--spawn-facing", "down").Summary);

    [Fact]
    public void Spawn_RequiresProjectAndDebug()
    {
        string[] spawn = ["--spawn-map", "map:t", "--spawn-x", "0", "--spawn-y", "0"];
        Assert.Contains("--project and --debug", Rejected([.. spawn]).Summary);
        Assert.Contains("--project and --debug", Rejected(["--project", "g", .. spawn]).Summary);
        Assert.Contains("--project and --debug", Rejected(["--debug", .. spawn]).Summary);
    }

    [Theory]
    [InlineData("town")]
    [InlineData("species:pal")]
    [InlineData("map:")]
    public void SpawnMap_RequiresAMapEntityId(string id) =>
        Assert.Contains("map entity ID", Rejected("--project", "g", "--debug",
            "--spawn-map", id, "--spawn-x", "0", "--spawn-y", "0").Summary);

    [Theory]
    [InlineData("-1", "0")]
    [InlineData("0", "-1")]
    [InlineData("x", "0")]
    [InlineData("1.5", "0")]
    public void SpawnCoordinates_RejectNegativeAndNonInteger(string x, string y) =>
        Assert.Contains("nonnegative integers", Rejected("--project", "g", "--debug",
            "--spawn-map", "map:t", "--spawn-x", x, "--spawn-y", y).Summary);

    [Fact]
    public void Diagnostic_FormatsWithStableCategoryAndExitCode()
    {
        var diagnostic = new BootDiagnostic(RuntimeExit.Content, "content", "Start map is missing.", "map:town");
        Assert.Equal("[runtime] content (exit 3): Start map is missing. [map:town]", diagnostic.Format());
        Assert.Equal("[runtime] content (exit 3): Start map is missing.",
            diagnostic with { Identifier = null } is var d ? d.Format() : "");
    }

    [Fact]
    public void ExitCodes_MatchTheSpecTable()
    {
        Assert.Equal(0, (int)RuntimeExit.Success);
        Assert.Equal(2, (int)RuntimeExit.Arguments);
        Assert.Equal(3, (int)RuntimeExit.Content);
        Assert.Equal(4, (int)RuntimeExit.Asset);
        Assert.Equal(5, (int)RuntimeExit.Save);
        Assert.Equal(6, (int)RuntimeExit.Initialization);
        Assert.Equal(10, (int)RuntimeExit.SmokeAssertion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--")]
    [InlineData("-project")]
    public void HostileInput_IsRejectedWithoutThrowing(string arg) =>
        Assert.Contains("Unknown argument", Rejected(arg).Summary);

    [Fact]
    public void SpawnCoordinate_OverflowIsRejectedNotWrapped() =>
        Assert.Contains("nonnegative integers", Rejected("--project", "g", "--debug",
            "--spawn-map", "map:t", "--spawn-x", "999999999999999999999", "--spawn-y", "0").Summary);

    /// <summary>Success and failure are mutually exclusive on every input; a caller can never see
    /// both a usable BootArgs and a diagnostic, or neither.</summary>
    [Fact]
    public void Result_IsAlwaysSuccessXorDiagnostic()
    {
        foreach (string[] input in new[]
        {
            [],
            new[] { "--debug" },
            ["--project", "g"],
            ["--bogus"],
            ["--project"],
            ["--project", "g", "--debug", "--spawn-map", "map:t", "--spawn-x", "1", "--spawn-y", "1"],
            ["--spawn-facing", "down"],
        })
            Assert.Equal(BootArgs.TryParse(input, out _, out BootDiagnostic? error), error is null);
    }
}
