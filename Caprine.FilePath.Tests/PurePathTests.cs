using System.IO.Enumeration;

namespace Caprine.FilePath.Tests;

public sealed class PurePathTests
{
    [Theory]
    [InlineData("", ".")]
    [InlineData("a//./b/", "a/b")]
    [InlineData("a/../b", "a/../b")]
    [InlineData("///a", "/a")]
    [InlineData("//server/a", "//server/a")]
    [InlineData("a\\b", "a\\b")]
    [InlineData(" ", " ")]
    public void PosixNormalizationPreservesMeaning(string input, string expected)
    {
        Assert.Equal(expected, new PosixPath(input).Value);
    }

    [Theory]
    [InlineData(@"C:notes.txt", "C:", "", false)]
    [InlineData(@"\notes.txt", "", @"\", false)]
    [InlineData(@"C:\notes.txt", "C:", @"\", true)]
    [InlineData(@"\\server\share\file", @"\\server\share", @"\", true)]
    [InlineData(@"\\?\C:\file", @"\\?\C:", @"\", true)]
    [InlineData(@"\\?\UNC\server\share\file", @"\\?\UNC\server\share", @"\", true)]
    public void WindowsAnchorsWorkOnEveryPlatform(string value, string drive, string root, bool absolute)
    {
        var path = new WindowsPath(value);
        Assert.Equal(drive, path.Drive);
        Assert.Equal(root, path.Root);
        Assert.Equal(absolute, path.IsAbsolute);
        Assert.Equal(drive + root, path.Parts[0]);
    }

    [Theory]
    [InlineData(@"C:\base", @"\child", @"C:\child")]
    [InlineData(@"C:\base", @"D:child", @"D:child")]
    [InlineData(@"C:\base", @"C:child", @"C:\base\child")]
    [InlineData(@"C:\base", @"D:\child", @"D:\child")]
    [InlineData(@"C:\base", "", @"C:\base")]
    public void WindowsJoinRetainsOrReplacesAnchors(string start, string next, string expected)
    {
        Assert.Equal(expected, new WindowsPath(start).JoinPath(next).Value);
    }

    [Fact]
    public void RootsAndCurrentDirectoryHaveNoProperParents()
    {
        foreach (var path in new PurePath[] { new WindowsPath(@"C:\"), new WindowsPath(@"\\server\share"), new PosixPath("/"), new PosixPath(".") })
        {
            Assert.Equal(path, path.Parent);
            Assert.Empty(path.Parents);
        }

        Assert.Equal(new[] { @"C:\alpha", @"C:\" }, new WindowsPath(@"C:\alpha\file").Parents.Select(p => p.Value));
    }

    [Fact]
    public void DotComponentsAndCaseUseFlavorRules()
    {
        Assert.Equal(new WindowsPath(@"C:\a\b"), new WindowsPath("c:/a//./b/"));
        Assert.Equal(new PosixPath("a/b"), new PosixPath("./a//b/"));
        Assert.NotEqual(new PosixPath("a"), new PosixPath("A"));
        Assert.NotEqual<PurePath>(new PosixPath("a"), new WindowsPath("a"));
        Assert.Throws<ArgumentException>(() => new PosixPath("a").CompareTo(new WindowsPath("a")));
    }

    [Fact]
    public void RelativePathsCompareComponents()
    {
        var parent = new WindowsPath(@"C:\base");
        var child  = new WindowsPath(@"C:\base\..cache\file");
        Assert.True(child.IsRelativeTo(parent));
        Assert.Equal(@"..cache\file", child.RelativeTo(parent).Value);
        Assert.False(new WindowsPath(@"C:\baseball\file").IsRelativeTo(parent));
        Assert.Throws<ArgumentException>(() => new WindowsPath(@"C:\other\file").RelativeTo(parent));
        Assert.Equal(@"..\other\file", new WindowsPath(@"C:\other\file").RelativeTo(parent, true).Value);
        Assert.False(new WindowsPath(@"D:\file").TryRelativeTo(parent, out _));
    }

    [Fact]
    public void LexicalAncestryDoesNotResolveDotDot()
    {
        Assert.True(new PosixPath("base/../other").IsRelativeTo(new PosixPath("base")));
        Assert.Throws<ArgumentException>(() => new PosixPath("a").RelativeTo(new PosixPath("b/.."), true));
    }

    [Theory]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData(".config.json", ".config", ".json")]
    [InlineData("archive.tar.gz", "archive.tar", ".gz")]
    [InlineData("file.", "file", ".")]
    [InlineData("..", "..", "")]
    public void StemAndSuffixFollowFilenameRules(string name, string stem, string suffix)
    {
        var path = new PosixPath(name);
        Assert.Equal(stem, path.Stem);
        Assert.Equal(suffix, path.Suffix);
    }

    [Fact]
    public void ComponentsCannotBeMutated()
    {
        var path  = new FilePath("base", "file.tar.gz");
        var parts = (IList<string>)path.Parts;
        Assert.Throws<NotSupportedException>(() => parts[0] = "changed");
        var suffixes = (IList<string>)path.Suffixes;
        Assert.Throws<NotSupportedException>(() => suffixes[0] = ".changed");
        Assert.Equal("base", path.Parts[0]);
    }

    [Fact]
    public void ReplacingANameDoesNotAddADotPrefix()
    {
        Assert.Equal("renamed.txt", new FilePath("file.txt").WithName("renamed.txt").Value);
        Assert.Throws<ArgumentException>(() => new PosixPath("file.txt").WithName("a/b"));
        Assert.Throws<ArgumentException>(() => new PosixPath("file.txt").WithSuffix(".a/b"));
        Assert.Throws<ArgumentException>(() => new WindowsPath("file.txt").WithName(@"a\b"));
        Assert.Throws<InvalidOperationException>(() => new PosixPath("/").WithName("file"));
    }

    [Fact]
    public void EmptyConstructionAndJoinsAreUnambiguous()
    {
        Assert.Equal(".", new FilePath().Value);
        var path = new FilePath("base");
        Assert.Same(path, path.JoinPath());
        Assert.Equal(path, path / "");
        Assert.Equal(path, new FilePath("./base/"));
    }

    [Fact]
    public void NativePathsSupportGenericParsing()
    {
        Assert.Equal(new FilePath("a/b"), Parse<FilePath>("a/b"));
        Assert.True(FilePath.TryParse("", out var current));
        Assert.Equal(".", current!.Value);
        Assert.False(FilePath.TryParse("a\0b", out _));
        Assert.False(FilePath.TryParse(null, out _));
    }

    [Fact]
    public void CachedComponentsRemainCorrectDuringConcurrentReads()
    {
        var path = new WindowsPath(@"C:\alpha\archive.tar.gz");
        Parallel.For(0, 1000, _ =>
        {
            Assert.Equal<string>([@"C:\", "alpha", "archive.tar.gz"], path.Parts);
            Assert.Equal<string>([".tar", ".gz"], path.Suffixes);
            Assert.Equal(".gz", path.Suffix);
            Assert.Equal("archive.tar", path.Stem);
        });
    }

    [Fact]
    public void SimpleGlobMatchingAgreesWithTheBclForGeneratedCases()
    {
        var random = new Random(4182);
        for (var i = 0; i < 5000; i++)
        {
            var text     = new string(Enumerable.Range(0, random.Next(1, 18)).Select(_ => "abc"[random.Next(3)]).ToArray());
            var pattern  = new string(Enumerable.Range(0, random.Next(1, 12)).Select(_ => "abc*?"[random.Next(5)]).ToArray());
            var expected = FileSystemName.MatchesSimpleExpression(pattern, text, false);
            Assert.Equal(expected, new PosixPath(text).FullMatch(pattern));
        }
    }

    private static T Parse<T>(string value) where T : IParsable<T> => T.Parse(value, null);
}
