using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The party panel's status tags — the burn/poison/sleep/etc. symbols the player reads at a
/// glance. Every major status must map to a tag so none renders blank.</summary>
public sealed class BattleStatusTagTests
{
    [Theory]
    [InlineData(PersistentStatus.Burn, "BRN")]
    [InlineData(PersistentStatus.Poison, "PSN")]
    [InlineData(PersistentStatus.Toxic, "TOX")]
    [InlineData(PersistentStatus.Paralysis, "PAR")]
    [InlineData(PersistentStatus.Sleep, "SLP")]
    [InlineData(PersistentStatus.Freeze, "FRZ")]
    public void EveryStatusMapsToATag(PersistentStatus status, string expected) =>
        Assert.Equal(expected, BattleHostScene.StatusTag(status));

    [Fact]
    public void NoStatusHasNoTag() => Assert.Null(BattleHostScene.StatusTag(null));

    /// <summary>Guards against a new PersistentStatus value slipping in without a tag.</summary>
    [Fact]
    public void EveryEnumMemberHasATag()
    {
        foreach (PersistentStatus s in Enum.GetValues<PersistentStatus>())
            Assert.False(string.IsNullOrEmpty(BattleHostScene.StatusTag(s)), $"{s} has no tag");
    }
}
