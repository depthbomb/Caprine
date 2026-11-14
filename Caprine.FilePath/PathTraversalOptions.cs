using System.Collections.Immutable;

namespace Caprine.FilePath;

/// <summary>Controls lazy directory traversal. Instances can be reused by multiple enumerations.</summary>
public sealed record PathTraversalOptions
{
    /// <summary>Gets the deepest directory to enumerate, with the starting directory at zero. Defaults to unlimited.</summary>
    public int MaxDepth { get; init; } = int.MaxValue;

    /// <summary>
    ///     Gets whether directory links are traversed. Defaults to false; repeated resolved directories are skipped when
    ///     enabled.
    /// </summary>
    public bool FollowSymbolicLinks { get; init; }

    /// <summary>Gets whether access-denied errors are skipped. Other I/O failures still propagate. Defaults to false.</summary>
    public bool IgnoreInaccessible { get; init; }

    /// <summary>Gets whether hidden entries are returned and traversed. Defaults to true.</summary>
    public bool IncludeHidden { get; init; } = true;

    /// <summary>
    ///     Gets an optional predicate called before descending into a child directory. Returning false prunes that
    ///     subtree.
    /// </summary>
    public Predicate<FilePath>? ShouldDescend { get; init; }

    /// <summary>Gets an optional glob case override. Null uses Windows insensitive or POSIX sensitive matching.</summary>
    public bool? CaseSensitive { get; init; }
}

/// <summary>A snapshot of one directory encountered during a walk.</summary>
/// <param name="Directory">The directory that was enumerated.</param>
/// <param name="Directories">Immediate child directories, including directory links.</param>
/// <param name="Files">Immediate non-directory entries, including dangling links.</param>
/// <param name="Depth">The directory depth, starting at zero.</param>
public sealed record DirectoryVisit(FilePath Directory, ImmutableArray<FilePath> Directories, ImmutableArray<FilePath> Files, int Depth);
