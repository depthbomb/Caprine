namespace Caprine.FilePath.Tests;

public sealed class FilePathTests
{
    [Theory]
    [InlineData("archive.tar.gz", ".tar|.gz")]
    [InlineData("file.txt", ".txt")]
    [InlineData("README", "")]
    [InlineData(".gitignore", "")]
    [InlineData(".config.json", ".json")]
    [InlineData("name.", ".")]
    [InlineData("a..b", ".|.b")]
    [InlineData("folder.with.dots/file.txt", ".txt")]
    public void SuffixesPreserveOrderAndEdgeCases(string value, string expected)
    {
        var path = new FilePath(value);

        Assert.Equal(expected, string.Join('|', path.Suffixes));
        Assert.Equal<string>(path.Suffixes, path.Suffixes);
    }

    [Theory]
    [InlineData("alpha/beta/file.txt", "alpha|beta|file.txt")]
    [InlineData("alpha//beta///file.txt", "alpha|beta|file.txt")]
    [InlineData("alpha/beta/", "alpha|beta")]
    [InlineData(".", "")]
    [InlineData("../file", "..|file")]
    [InlineData("café/🐐.txt", "café|🐐.txt")]
    public void RelativePartsSkipEmptySegments(string value, string expected)
    {
        var path = new FilePath(value);

        Assert.Equal(expected, string.Join('|', path.Parts));
        Assert.Equal<string>(path.Parts, path.Parts);
    }

    [Fact]
    public void AbsolutePartsRetainTheAnchor()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var path = new FilePath(Path.Combine(root, "alpha", "file.txt"));

        Assert.Equal<string>([root, "alpha", "file.txt"], path.Parts);
        Assert.Equal<string>([root], new FilePath(root).Parts);
    }

    [Fact]
    public void JoiningAndReplacingSuffixesUseTheCurrentPlatform()
    {
        var path = new FilePath("alpha").JoinPath("beta", "archive.tar.gz");

        Assert.Equal(Path.Combine("alpha", "beta", "archive.tar.gz"), path.Value);
        Assert.Equal(Path.Combine("alpha", "beta", "archive.tar.zip"), path.WithSuffix(".zip").Value);
        Assert.Equal("archive.tar", path.Stem);
    }

    [Fact]
    public async Task TextAndBytesRoundTripOnEachRuntime()
    {
        var directory = Directory.CreateTempSubdirectory("Caprine.FilePath.Tests-");
        var path      = new FilePath(Path.Combine(directory.FullName, "content.txt"));
        try
        {
            await path.WriteTextAsync("café 🐐");
            Assert.Equal("café 🐐", await path.ReadTextAsync());
            Assert.True(path.IsFile);

            byte[] bytes = [0, 1, 2, 127, 255];
            await path.WriteBytesAsync(bytes);
            Assert.Equal(bytes, path.ReadBytes());
        }
        finally
        {
            File.Delete(path.Value);
            directory.Delete();
        }
    }
}
