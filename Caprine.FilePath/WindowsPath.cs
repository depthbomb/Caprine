namespace Caprine.FilePath;

/// <summary>A pure Windows path that can be manipulated on any operating system.</summary>
public sealed class WindowsPath : PurePath
{
    /// <summary>Creates a Windows path without accessing the filesystem.</summary>
    /// <param name="path">Windows path text; empty means ".". UNC, device, rooted, and drive-relative paths are supported.</param>
    /// <exception cref="ArgumentException">path contains a null character.</exception>
    public WindowsPath(string path = "") : base(path, PathFlavor.Windows) { }

    /// <inheritdoc />
    protected override PurePath Create(string value) => new WindowsPath(value);
}
