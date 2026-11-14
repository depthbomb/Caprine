namespace Caprine.FilePath;

/// <summary>Controls recursive copying. Copy operations are not transactional.</summary>
public sealed record PathCopyOptions
{
    /// <summary>Gets whether existing files or links may be replaced. Defaults to false.</summary>
    public bool Overwrite { get; init; }
    /// <summary>Gets whether an existing destination directory may be merged. Defaults to false.</summary>
    public bool MergeDirectories { get; init; }
    /// <summary>Gets whether link targets are copied instead of recreating links. Defaults to false; cycles throw when following links.</summary>
    public bool FollowSymbolicLinks { get; init; }
    /// <summary>Gets whether timestamps, attributes, and Unix permission bits are copied. Defaults to false.</summary>
    /// <remarks>Ownership, ACLs, extended attributes, and alternate data streams are not guaranteed to be preserved.</remarks>
    public bool PreserveMetadata { get; init; }
}
