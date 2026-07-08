using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.Tests.Assets;

public sealed class CharacterAnimationTests
{
    // 12 sprites in row-major order: rows = Down, Left, Right, Up; cols = 3 walk frames.
    private static List<EntityId> Grid() =>
        [.. Enumerable.Range(0, 12).Select(i => EntityId.Parse($"sprite:c{i}"))];

    [Fact]
    public void BuildWalkClips_ProducesOneLoopingClipPerFacing()
    {
        var clips = CharacterAnimation.BuildWalkClips("hero", Grid());

        Assert.Equal(4, clips.Count);
        foreach (Facing dir in CharacterAnimation.RowOrder)
        {
            Assert.True(clips.ContainsKey(dir));
            Assert.True(clips[dir].Loop);
            Assert.Equal(3, clips[dir].Frames.Count);
        }
    }

    [Fact]
    public void RowMapping_MatchesStandardLayout()
    {
        var clips = CharacterAnimation.BuildWalkClips("hero", Grid());

        // Down = first row (c0,c1,c2); Up = last row (c9,c10,c11).
        Assert.Equal(
            [EntityId.Parse("sprite:c0"), EntityId.Parse("sprite:c1"), EntityId.Parse("sprite:c2")],
            clips[Facing.Down].Frames.Select(f => f.Sprite));
        Assert.Equal(
            [EntityId.Parse("sprite:c9"), EntityId.Parse("sprite:c10"), EntityId.Parse("sprite:c11")],
            clips[Facing.Up].Frames.Select(f => f.Sprite));
        // Left = row 1 (c3..c5), Right = row 2 (c6..c8).
        Assert.Equal(EntityId.Parse("sprite:c3"), clips[Facing.Left].Frames[0].Sprite);
        Assert.Equal(EntityId.Parse("sprite:c6"), clips[Facing.Right].Frames[0].Sprite);
    }

    [Fact]
    public void ClipIdsAndFrameDuration_AreDerived()
    {
        var clips = CharacterAnimation.BuildWalkClips("hero", Grid(), frameMs: 200);

        Assert.Equal(EntityId.Parse("anim:hero_walk_down"), clips[Facing.Down].Id);
        Assert.Equal(EntityId.Parse("anim:hero_walk_up"), clips[Facing.Up].Id);
        Assert.All(clips.Values, a => Assert.All(a.Frames, f => Assert.Equal(200, f.Ms)));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(0)]
    public void WrongSpriteCount_Throws(int count)
    {
        var sprites = Enumerable.Range(0, count).Select(i => EntityId.Parse($"sprite:c{i}")).ToList();
        Assert.Throws<ArgumentException>(() => CharacterAnimation.BuildWalkClips("hero", sprites));
    }

    [Fact]
    public void NonPositiveFrameMs_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterAnimation.BuildWalkClips("hero", Grid(), frameMs: 0));
    }

    [Fact]
    public void InvalidBaseSlug_Throws()
    {
        // EntityId enforces slug grammar, so a bad base name fails fast.
        Assert.ThrowsAny<ArgumentException>(() => CharacterAnimation.BuildWalkClips("Bad Slug!", Grid()));
    }
}
