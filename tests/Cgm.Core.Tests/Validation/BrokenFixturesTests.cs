using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;

namespace Cgm.Core.Tests.Validation;

/// <summary>End-to-end regression: committed broken projects must be caught (load or validate).</summary>
public sealed class BrokenFixturesTests
{
    [Theory]
    [InlineData("missing-ref", "broken-reference")]
    [InlineData("bad-growth", "growth-rate")]
    [InlineData("empty-encounter", "encounter-table")]
    [InlineData("status-move-power", "move")]
    public void BrokenProject_LoadsButReportsExpectedRule(string folder, string expectedRuleId)
    {
        Project p = ProjectLoader.Load(TestPaths.Fixture($"projects/{folder}"));
        ValidationReport report = Validator.Run(p);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, i => i.RuleId == expectedRuleId);
    }

    [Theory]
    [InlineData("filename-mismatch")]
    [InlineData("malformed-json")]
    public void UnloadableProject_ThrowsOnLoad(string folder)
    {
        Assert.ThrowsAny<Exception>(() => ProjectLoader.Load(TestPaths.Fixture($"projects/{folder}")));
    }
}
