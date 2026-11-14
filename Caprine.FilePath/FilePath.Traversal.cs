using System.Collections.Immutable;

namespace Caprine.FilePath;

public sealed partial class FilePath
{
    /// <summary>Matches trailing components, or the entire path for an anchored pattern. Here ** acts as *.</summary>
    /// <param name="pattern">A nonempty glob with *, ?, and bracket classes.</param>
    /// <param name="caseSensitive">Null uses native case rules.</param>
    /// <returns>Whether the lexical path matches, without filesystem access.</returns>
    public bool Match(string pattern, bool? caseSensitive = null) => _pure.Match(pattern, caseSensitive);

    /// <summary>Matches the entire lexical path; ** consumes zero or more complete components.</summary>
    /// <param name="pattern">A nonempty glob, anchored when matching an anchored path.</param>
    /// <param name="caseSensitive">Null uses native case rules.</param>
    /// <returns>Whether all components match, without filesystem access.</returns>
    public bool FullMatch(string pattern, bool? caseSensitive = null) => _pure.FullMatch(pattern, caseSensitive);

    /// <summary>Lazily finds descendants matching a relative glob, including subdirectory patterns and recursive **.</summary>
    /// <param name="pattern">A nonempty relative glob. A trailing separator limits results to directories.</param>
    /// <param name="options">Traversal and case options; null uses defaults.</param>
    /// <param name="ct">Cancels enumeration between entries.</param>
    /// <returns>Matching paths in filesystem order. Missing or non-directory receivers produce no results.</returns>
    /// <remarks>Hidden entries are included by default. Directory links are returned but not traversed unless enabled. A bare ** includes this directory.</remarks>
    /// <exception cref="ArgumentException">The pattern is empty or anchored.</exception>
    /// <exception cref="IOException">Enumeration fails.</exception>
    public IEnumerable<FilePath> Glob(string pattern, PathTraversalOptions? options = null, CancellationToken ct = default)
    {
        var glob = new GlobPattern(pattern, PathFlavor.Native, options?.CaseSensitive);
        if (glob.Rooted)
        {
            throw new ArgumentException("A glob must be relative to this directory.", nameof(pattern));
        }

        options ??= new PathTraversalOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxDepth);
        var directoriesOnly = Path.EndsInDirectorySeparator(pattern);
        var depth = glob.Recursive ? options.MaxDepth : Math.Min(options.MaxDepth, Math.Max(0, glob.Depth - 1));
        foreach (var visit in Walk(options with { MaxDepth = depth }, ct))
        {
            if (visit.Depth == 0 && glob.Matches(".", true))
            {
                yield return this;
            }

            foreach (var entry in visit.Directories.Concat(directoriesOnly ? [] : visit.Files))
            {
                ct.ThrowIfCancellationRequested();
                if (glob.Matches(entry.RelativeTo(this).Value, true))
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>Lazily searches this directory and descendants for a relative glob.</summary>
    /// <param name="pattern">A nonempty relative glob; **/ is prepended.</param>
    /// <param name="options">Traversal and case options.</param>
    /// <param name="ct">Cancels enumeration between entries.</param>
    /// <returns>Matching paths in filesystem order.</returns>
    public IEnumerable<FilePath> RGlob(string pattern, PathTraversalOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        if (new PurePath(pattern).IsRooted)
        {
            throw new ArgumentException("A glob must be relative.", nameof(pattern));
        }

        return Glob("**/" + pattern, options, ct);
    }

    /// <summary>Lazily enumerates immediate children.</summary>
    /// <returns>Child paths in filesystem order, or no results for a missing or non-directory receiver.</returns>
    public IEnumerable<FilePath> IterDir() => EnumerateEntries();

    /// <summary>Lazily enumerates files and directories below this directory.</summary>
    /// <param name="options">Null enumerates immediate children only. Otherwise MaxDepth controls recursion.</param>
    /// <param name="ct">Cancels traversal between entries.</param>
    /// <returns>Entries grouped by directory in pre-order, without sorting.</returns>
    public IEnumerable<FilePath> EnumerateEntries(PathTraversalOptions? options = null, CancellationToken ct = default) =>
        Walk(options ?? new PathTraversalOptions { MaxDepth = 0 }, ct).SelectMany(static visit => visit.Directories.Concat(visit.Files));

    /// <summary>Lazily enumerates non-directory entries, including dangling links.</summary>
    /// <param name="options">Null enumerates immediate files only.</param>
    /// <param name="ct">Cancels traversal between entries.</param>
    /// <returns>File paths in filesystem order.</returns>
    public IEnumerable<FilePath> EnumerateFiles(PathTraversalOptions? options = null, CancellationToken ct = default) =>
        Walk(options ?? new PathTraversalOptions { MaxDepth = 0 }, ct).SelectMany(static visit => visit.Files);

    /// <summary>Lazily enumerates child directories, including directory links.</summary>
    /// <param name="options">Null enumerates immediate directories only.</param>
    /// <param name="ct">Cancels traversal between entries.</param>
    /// <returns>Directory paths in filesystem order.</returns>
    public IEnumerable<FilePath> EnumerateDirectories(PathTraversalOptions? options = null, CancellationToken ct = default) =>
        Walk(options ?? new PathTraversalOptions { MaxDepth = 0 }, ct).SelectMany(static visit => visit.Directories);

    /// <summary>Lazily walks directories in pre-order, taking one immediate-child snapshot per visited directory.</summary>
    /// <param name="options">Depth, link, hidden-entry, error, and pruning options. Null uses defaults.</param>
    /// <param name="ct">Cancels traversal between entries and directories.</param>
    /// <returns>Snapshots starting with this directory at depth zero. Missing or non-directory receivers produce no results.</returns>
    /// <remarks>Use ShouldDescend to prune subtrees. When following links, repeated resolved directories are skipped. Filesystem changes can invalidate snapshots.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">MaxDepth is negative.</exception>
    /// <exception cref="UnauthorizedAccessException">Access is denied and IgnoreInaccessible is false.</exception>
    public IEnumerable<DirectoryVisit> Walk(PathTraversalOptions? options = null, CancellationToken ct = default)
    {
        options ??= new PathTraversalOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxDepth);
        ct.ThrowIfCancellationRequested();
        if (!IsDirectory)
        {
            yield break;
        }

        var pending = new Stack<(FilePath Path, int Depth)>();
        var visited = new HashSet<FilePath>();
        pending.Push((this, 0));
        while (pending.TryPop(out var item))
        {
            ct.ThrowIfCancellationRequested();
            if (options.FollowSymbolicLinks && !visited.Add(item.Path.Resolve()))
            {
                continue;
            }

            var directories = ImmutableArray.CreateBuilder<FilePath>();
            var files = ImmutableArray.CreateBuilder<FilePath>();
            var enumeration = new EnumerationOptions
            {
                IgnoreInaccessible = options.IgnoreInaccessible,
                AttributesToSkip = options.IncludeHidden ? 0 : FileAttributes.Hidden,
                ReturnSpecialDirectories = false
            };
            foreach (var info in new DirectoryInfo(item.Path.Value).EnumerateFileSystemInfos("*", enumeration))
            {
                ct.ThrowIfCancellationRequested();
                var entry = item.Path / info.Name;
                if (!options.IncludeHidden && info.Name.StartsWith('.'))
                {
                    continue;
                }

                if ((info.Attributes & FileAttributes.Directory) != 0)
                {
                    directories.Add(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }

            var children = directories.ToImmutable();
            yield return new DirectoryVisit(item.Path, children, files.ToImmutable(), item.Depth);
            if (item.Depth >= options.MaxDepth)
            {
                continue;
            }

            for (var i = children.Length - 1; i >= 0; i--)
            {
                var child = children[i];
                if ((!options.FollowSymbolicLinks && child.IsSymbolicLink) || options.ShouldDescend?.Invoke(child) == false)
                {
                    continue;
                }

                pending.Push((child, item.Depth + 1));
            }
        }
    }
}
