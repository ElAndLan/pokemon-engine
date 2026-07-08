using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class TriggerConditionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_IsUnconditionallyMet(string? condition)
    {
        Assert.True(TriggerCondition.IsMet(condition, _ => false));
    }

    [Fact]
    public void BadgeGate_BlockedUntilFlagSet()
    {
        var flags = new FlagStore();
        const string cond = "story.badge_1";

        Assert.False(TriggerCondition.IsMet(cond, flags)); // door locked — no badge yet
        flags.SetBool("story.badge_1", true);
        Assert.True(TriggerCondition.IsMet(cond, flags));  // badge earned — door opens
    }

    [Fact]
    public void Negation_InvertsFlag()
    {
        var flags = new FlagStore();
        Assert.True(TriggerCondition.IsMet("!story.badge_1", flags));  // unset → negation true
        flags.SetBool("story.badge_1", true);
        Assert.False(TriggerCondition.IsMet("!story.badge_1", flags)); // set → negation false
    }

    [Fact]
    public void DoubleNegation_IsIdentity()
    {
        Assert.True(TriggerCondition.IsMet("!!x", _ => true));
        Assert.False(TriggerCondition.IsMet("!!x", _ => false));
    }

    [Fact]
    public void IntFlag_TruthyWhenNonZero()
    {
        var flags = new FlagStore();
        flags.SetInt("story.progress", 0);
        Assert.False(TriggerCondition.IsMet("story.progress", flags));
        flags.SetInt("story.progress", 3);
        Assert.True(TriggerCondition.IsMet("story.progress", flags));
    }

    [Fact]
    public void LeadingWhitespaceAndBang_Tolerated()
    {
        Assert.True(TriggerCondition.IsMet("  ! missing_flag ", _ => false)); // trims around the token
    }
}
