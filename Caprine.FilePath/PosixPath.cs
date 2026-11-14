namespace Caprine.FilePath;

/// <summary>A pure POSIX path that can be manipulated on any operating system.</summary>
public sealed class PosixPath : PurePath
{
    /// <summary>Creates a POSIX path without accessing the filesystem.</summary>
    /// <param name="path">POSIX path text; empty means ".". Backslashes are ordinary filename characters.</param>
    /// <exception cref="ArgumentException">path contains a null character.</exception>
    public PosixPath(string path = "") : base(path, PathFlavor.Posix) {}

    /// <inheritdoc />
    protected override PurePath Create(string value) => new PosixPath(value);
}