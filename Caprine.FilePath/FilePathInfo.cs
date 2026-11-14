namespace Caprine.FilePath;

/// <summary>An immutable metadata snapshot returned by FilePath.Stat.</summary>
public sealed class FilePathInfo
{
    /// <summary>Gets the inspected path, resolved when Stat follows links.</summary>
    public FilePath Path { get; }

    /// <summary>Gets captured filesystem attributes.</summary>
    public FileAttributes Attributes { get; }

    /// <summary>Gets the captured file length in bytes, or null for a directory.</summary>
    public long? Length { get; }

    /// <summary>Gets the captured creation time in UTC. Filesystem support and precision vary.</summary>
    public DateTime CreationTimeUtc { get; }

    /// <summary>Gets the captured last-access time in UTC.</summary>
    public DateTime LastAccessTimeUtc { get; }

    /// <summary>Gets the captured last-write time in UTC.</summary>
    public DateTime LastWriteTimeUtc { get; }

    /// <summary>Gets the original entry's stored symbolic-link target, or null for a non-link.</summary>
    public string? LinkTarget { get; }

    /// <summary>Gets whether the inspected entry was a directory.</summary>
    public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;

    internal FilePathInfo(FilePath path, FileAttributes attributes, long? length, DateTime created, DateTime accessed, DateTime modified, string? linkTarget)
    {
        Path              = path;
        Attributes        = attributes;
        Length            = length;
        CreationTimeUtc   = created;
        LastAccessTimeUtc = accessed;
        LastWriteTimeUtc  = modified;
        LinkTarget        = linkTarget;
    }
}
