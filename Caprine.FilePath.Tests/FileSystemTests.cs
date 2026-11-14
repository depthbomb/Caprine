using System.Text;
using System.Text.Json;

namespace Caprine.FilePath.Tests;

public sealed class FileSystemTests
{
    [Theory]
    [InlineData("a/b.txt", "*.txt", true, false)]
    [InlineData("a/b.txt", "a/*.txt", true, true)]
    [InlineData("a/b.txt", "**/*.txt", true, true)]
    [InlineData("b.txt", "**/*.txt", false, true)]
    [InlineData("a/b/c.txt", "**/*.txt", true, true)]
    [InlineData("ab/file", "a/**/file", false, false)]
    [InlineData("a/file", "a/**/file", false, true)]
    [InlineData("a/x/file", "a/**/file", true, true)]
    [InlineData("a/3.txt", "a/[0-9].txt", true, true)]
    [InlineData("a/x.txt", "a/[!0-9].txt", true, true)]
    [InlineData("a/].txt", "a/[]].txt", true, true)]
    [InlineData("a/[.txt", "a/[.txt", true, true)]
    [InlineData("/a/b.txt", "*.txt", true, false)]
    [InlineData("/a/b.txt", "/a/*.txt", true, true)]
    public void GlobGrammarRespectsComponents(string path, string pattern, bool match, bool fullMatch)
    {
        var pure = new PosixPath(path);
        Assert.Equal(match, pure.Match(pattern));
        Assert.Equal(fullMatch, pure.FullMatch(pattern));
    }

    [Fact]
    public void GlobCaseIsExplicitAndFlavorAware()
    {
        Assert.True(new WindowsPath("A.TXT").FullMatch("a.txt"));
        Assert.False(new PosixPath("A.TXT").FullMatch("a.txt"));
        Assert.True(new PosixPath("A.TXT").FullMatch("a.txt", caseSensitive: false));
        Assert.False(new WindowsPath("A.TXT").FullMatch("a.txt", caseSensitive: true));
    }

    [Fact]
    public void GlobTraversesSubdirectoryPatternsAndNeverSearchesAReceiversParent()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "top.txt").WriteText("top");
        (root / "sub").Mkdir();
        (root / "sub" / "deep.txt").WriteText("deep");
        (root / "sub" / "nested").Mkdir();
        (root / "sub" / "nested" / "third.txt").Touch();
        Assert.Equal(["deep.txt"], root.Glob("sub/*.txt").Select(p => p.Name));
        Assert.Equal(3, root.Glob("**/*.txt").Count());
        Assert.Single(root.Glob("**/*.txt", new PathTraversalOptions { MaxDepth = 0 }));
        Assert.Empty((root / "missing").Glob("*.txt"));
        Assert.Empty((root / "top.txt").Glob("*.txt"));
        Assert.Equal(2, root.Glob("**/").Count(p => p != root));
        Assert.Throws<ArgumentException>(() => root.Glob(root.Value).ToArray());
    }

    [Fact]
    public void WalkSupportsPruningHiddenEntriesDepthAndCancellation()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "keep" / "nested").Mkdir(parents: true);
        (root / "skip").Mkdir();
        (root / ".hidden").Mkdir();
        (root / "keep" / "a").Touch();
        (root / "skip" / "b").Touch();
        var visits = root.Walk(new PathTraversalOptions { IncludeHidden = false, ShouldDescend = p => p.Name != "skip" }).ToArray();
        Assert.Equal([".", "keep", Path.Combine("keep", "nested")], visits.Select(v => v.Directory.RelativeTo(root).Value));
        Assert.Single(root.EnumerateFiles(new PathTraversalOptions { ShouldDescend = p => p.Name == "keep" }));
        Assert.Equal(3, root.EnumerateDirectories().Count());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => root.Walk(ct: cts.Token).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => root.Walk(new PathTraversalOptions { MaxDepth = -1 }).ToArray());
    }

    [Fact]
    public void ResolveFollowsIntermediateLinksBeforeParentComponents()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "actual" / "nested").Mkdir(parents: true);
        (root / "actual" / "marker").Touch();
        (root / "link").CreateSymbolicLink("actual/nested", isDirectory: true);
        Assert.Equal((root / "actual" / "marker").Absolute(), (root / "link" / ".." / "marker").Resolve(true));
        Assert.Equal((root / "actual" / "missing").Absolute(), (root / "link" / ".." / "missing").Resolve());
        Assert.Throws<FileNotFoundException>(() => (root / "link" / ".." / "missing").Resolve(true));
        Assert.Equal(new FilePath("actual/nested"), (root / "link").ReadLink());
        Assert.True((root / "link").IsSymbolicLink);
        Assert.False((root / "absent").IsSymbolicLink);
        Assert.Throws<IOException>(() => (root / "actual" / "marker" / "child").Resolve());
    }

    [Fact]
    public void DanglingLinksCanBeInspectedCopiedAndRemoved()
    {
        using var fixture = new TestDirectory();
        var link = fixture.Path / "dangling";
        link.CreateSymbolicLink("missing");
        Assert.True(link.IsSymbolicLink);
        Assert.Equal("missing", link.LinkTarget);
        var copy = link.CopyTo(fixture.Path / "copy");
        Assert.Equal("missing", copy.LinkTarget);
        Assert.Equal((fixture.Path / "missing").Absolute(), link.Resolve());
        Assert.Throws<FileNotFoundException>(() => link.Resolve(true));
        link.Unlink();
        Assert.False(link.IsSymbolicLink);
        link.Unlink(missingOk: true);
    }

    [Fact]
    public void LinkLoopsFailResolutionAndDoNotLoopDuringTraversal()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "dir").Mkdir();
        (root / "dir" / "back").CreateSymbolicLink(root, isDirectory: true);
        Assert.Equal(2, root.Walk().Count());
        Assert.Equal(2, root.Walk(new PathTraversalOptions { FollowSymbolicLinks = true }).Count());
        (root / "a").CreateSymbolicLink("b");
        (root / "b").CreateSymbolicLink("a");
        Assert.Throws<IOException>(() => (root / "a").Resolve());
    }

    [Fact]
    public void SameFileRecognizesHardLinksAndDirectories()
    {
        using var fixture = new TestDirectory();
        var file = fixture.Path / "source";
        file.WriteText("one");
        var hard = (fixture.Path / "hard").CreateHardLink(file);
        var other = fixture.Path / "other";
        other.WriteText("one");
        Assert.True(file.SameFile(hard));
        Assert.False(file.SameFile(other));
        Assert.True(fixture.Path.SameFile(fixture.Path / "."));
        hard.WriteText("two");
        Assert.Equal("two", file.ReadText());
        Assert.Throws<IOException>(() => file.CopyTo(hard, new PathCopyOptions { Overwrite = true }));
        Assert.Equal("two", file.ReadText());
    }

    [Fact]
    public void TouchDoesNotCreateParentsOrTruncateFiles()
    {
        using var fixture = new TestDirectory();
        var file = fixture.Path / "sub" / "file";
        Assert.Throws<DirectoryNotFoundException>(() => file.Touch());
        file.Touch(parents: true);
        file.WriteText("keep");
        file.Touch();
        Assert.Equal("keep", file.ReadText());
        Assert.Throws<IOException>(() => file.Touch(existOk: false));
        Assert.Equal("keep", file.ReadText());
    }

    [Fact]
    public void StatIsAnImmutableSnapshotAndReportsMissingFiles()
    {
        using var fixture = new TestDirectory();
        var file = fixture.Path / "file";
        file.WriteText("old");
        var before = file.Stat();
        file.WriteText("longer");
        Assert.Equal(3, before.Length);
        Assert.Equal(6, file.Stat().Length);
        Assert.Null(fixture.Path.Stat().Length);
        Assert.True(fixture.Path.Stat().IsDirectory);
        Assert.Throws<FileNotFoundException>(() => (fixture.Path / "missing").Stat());
        Assert.False((fixture.Path / "missing").ToFileInfo().Exists);
    }

    [Fact]
    public async Task TextUsesNoBomByDefaultAndSupportsStreaming()
    {
        using var fixture = new TestDirectory();
        var file = fixture.Path / "file";
        file.WriteText("café");
        Assert.Equal(Encoding.UTF8.GetBytes("café"), file.ReadBytes());
        file.WriteText("bom", new UTF8Encoding(true));
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, file.ReadBytes()[..3]);
        file.WriteLines(["one", "two"]);
        file.AppendLines(["three"]);
        var read = new List<string>();
        await foreach (var line in file.ReadLinesAsync())
        {
            read.Add(line);
        }

        Assert.Equal(["one", "two", "three"], read);
        await file.WriteLinesAsync(["a"]);
        await file.AppendLinesAsync(["b"]);
        Assert.Equal(["a", "b"], file.ReadLines());
        await file.WriteTextAsync("x".AsMemory());
        await file.AppendTextAsync("y");
        Assert.Equal("xy", await file.ReadTextAsync());
        using var stream = file.OpenRead();
        Assert.Equal(2, stream.Length);
    }

    [Fact]
    public void CopyAndMoveUseExactDestinationsAndExplicitMerges()
    {
        using var fixture = new TestDirectory();
        var source = fixture.Path / "source";
        (source / "sub").Mkdir(parents: true);
        (source / "a").WriteText("a");
        (source / "sub" / "b").WriteText("b");
        var target = source.CopyTo(fixture.Path / "target");
        Assert.Equal("b", (target / "sub" / "b").ReadText());
        Assert.Throws<IOException>(() => source.CopyTo(target));
        (source / "a").WriteText("new");
        source.CopyTo(target, new PathCopyOptions { MergeDirectories = true, Overwrite = true });
        Assert.Equal("new", (target / "a").ReadText());
        Assert.Throws<IOException>(() => source.CopyTo(source / "nested"));
        var into = fixture.Path / "into";
        into.Mkdir();
        Assert.Equal(into / "a", (source / "a").CopyInto(into));
        var moved = target.MoveInto(into);
        Assert.Equal(into / "target", moved);
        Assert.False(target.Exists);
        Assert.True(moved.IsDirectory);
        Assert.Throws<IOException>(() => source.Replace(moved));
    }

    [Fact]
    public void CopyPreservesLinksUnlessFollowingIsRequested()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "source").Mkdir();
        (root / "outside").WriteText("target");
        (root / "source" / "link").CreateSymbolicLink("../outside");
        var links = (root / "source").CopyTo(root / "links");
        Assert.Equal(Path.Combine("..", "outside"), (links / "link").LinkTarget);
        var followed = (root / "source").CopyTo(root / "followed", new PathCopyOptions { FollowSymbolicLinks = true });
        Assert.False((followed / "link").IsSymbolicLink);
        Assert.Equal("target", (followed / "link").ReadText());
        (root / "source" / "cycle").CreateSymbolicLink(".", isDirectory: true);
        Assert.Throws<IOException>(() => (root / "source").CopyTo(root / "cycle-copy", new PathCopyOptions { FollowSymbolicLinks = true }));
    }

    [Fact]
    public async Task AtomicWritesReplaceEntriesAndLeaveNoTemporarySiblings()
    {
        using var fixture = new TestDirectory();
        var file = fixture.Path / "file";
        file.WriteTextAtomic("first");
        await file.WriteTextAtomicAsync("second");
        Assert.Equal("second", file.ReadText());
        await file.WriteBytesAtomicAsync(new byte[] { 1, 2, 3 }, flushToDisk: true);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.ReadBytes());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.WriteTextAtomicAsync("cancelled", ct: cts.Token));
        Assert.Equal(new byte[] { 1, 2, 3 }, file.ReadBytes());
        Assert.Single(fixture.Path.IterDir());
        var target = fixture.Path / "target";
        target.WriteText("original");
        var link = fixture.Path / "link";
        link.CreateSymbolicLink(target);
        link.WriteTextAtomic("replacement");
        Assert.False(link.IsSymbolicLink);
        Assert.Equal("original", target.ReadText());
        Assert.Equal("replacement", link.ReadText());
    }

    [Fact]
    public void TemporaryOwnershipCleansUpAndRejectsReplacements()
    {
        var owner = TemporaryPath.CreateDirectory();
        var path = owner.Path;
        (path / "file").Touch();
        owner.Dispose();
        owner.Dispose();
        Assert.False(path.Exists);
        using var fixture = new TestDirectory();
        var fileOwner = TemporaryPath.CreateFile(fixture.Path);
        var original = fileOwner.Path;
        original.MoveTo(fixture.Path / "saved");
        original.Touch();
        Assert.Throws<IOException>(() => fileOwner.Dispose());
        Assert.True(original.Exists);
    }

    [Fact]
    public void JsonSupportsValuesAndDictionaryKeys()
    {
        var path = new FilePath("a/./b");
        Assert.Equal(path, JsonSerializer.Deserialize<FilePath>(JsonSerializer.Serialize(path)));
        var map = new Dictionary<FilePath, int> { [path] = 42 };
        var copy = JsonSerializer.Deserialize<Dictionary<FilePath, int>>(JsonSerializer.Serialize(map));
        Assert.Equal(42, copy![path]);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FilePath>("\"a\\u0000b\""));
        Assert.Null(JsonSerializer.Deserialize<FilePath>("null"));
    }

    [Fact]
    public void CancellationStopsWithinAnAlreadyBufferedDirectory()
    {
        using var fixture = new TestDirectory();
        (fixture.Path / "one").Touch();
        (fixture.Path / "two").Touch();
        using var cts = new CancellationTokenSource();
        using var files = fixture.Path.EnumerateFiles(ct: cts.Token).GetEnumerator();
        Assert.True(files.MoveNext());
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => files.MoveNext());
    }

    [Fact]
    public void DirectoryMergesCannotWriteBackIntoTheirOwnSource()
    {
        using var fixture = new TestDirectory();
        var source = fixture.Path / "source";
        (source / "source").Mkdir(parents: true);
        (source / "source" / "original").WriteText("keep");
        Assert.Throws<IOException>(() => source.CopyTo(fixture.Path, new PathCopyOptions { MergeDirectories = true, Overwrite = true }));
        Assert.Equal("keep", (source / "source" / "original").ReadText());
        Assert.False((source / "original").Exists);
    }

    [Fact]
    public void CopyingFollowedDirectoryLinksRejectsDestinationsInsideTheirTargets()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "source").Mkdir();
        (root / "outside").Mkdir();
        (root / "source" / "link").CreateSymbolicLink(root / "outside", isDirectory: true);
        Assert.Throws<IOException>(() => (root / "source").CopyTo(root / "outside" / "copy", new PathCopyOptions { FollowSymbolicLinks = true }));
        Assert.False((root / "outside" / "copy" / "link").Exists);
    }

    [Fact]
    public void LinkCopiesCanReplaceFilesWithoutOverwritingTheirTargets()
    {
        using var fixture = new TestDirectory();
        var root = fixture.Path;
        (root / "source").CreateSymbolicLink("missing");
        (root / "destination").WriteText("replace");
        (root / "source").CopyTo(root / "destination", new PathCopyOptions { Overwrite = true });
        Assert.Equal("missing", (root / "destination").LinkTarget);
        Assert.Throws<FileNotFoundException>(() => (root / "absent").CopyTo(root / "destination", new PathCopyOptions { Overwrite = true }));
        Assert.Equal("missing", (root / "destination").LinkTarget);
    }

    [Fact]
    public void WindowsFilenameValidationRejectsControlCharactersOnEveryPlatform()
    {
        Assert.Throws<ArgumentException>(() => new WindowsPath("file").WithName("invalid\u0001name"));
        Assert.Equal("valid\u0001name", new PosixPath("file").WithName("valid\u0001name").Name);
    }

    private sealed class TestDirectory : IDisposable
    {
        internal FilePath Path { get; } = new FilePath(Directory.CreateTempSubdirectory("Caprine-tests-").FullName);

        public void Dispose() => Directory.Delete(Path.Value, true);
    }
}
