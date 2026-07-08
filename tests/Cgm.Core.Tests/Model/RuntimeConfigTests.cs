using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class RuntimeConfigTests
{
    [Theory]
    [InlineData("My Game!", "My Game")]
    [InlineData("already_safe-1", "already_safe-1")]
    [InlineData("  weird // name  ", "weird name")]   // trims, drops slashes, collapses the gap
    [InlineData("A   B", "A B")]                       // collapses internal whitespace
    [InlineData("!!!", "Game")]                        // nothing safe left → fallback
    [InlineData("", "Game")]
    public void SafeSaveDir_ProducesFilesystemSafeName(string input, string expected)
    {
        Assert.Equal(expected, RuntimeConfig.SafeSaveDir(input));
    }

    [Fact]
    public void SafeSaveDir_HasNoInvalidPathChars()
    {
        string dir = RuntimeConfig.SafeSaveDir("weird:/\\*?name<>|");
        Assert.DoesNotContain(dir.ToCharArray(), c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0);
    }
}
