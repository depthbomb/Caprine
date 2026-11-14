namespace Caprine.FilePath;

/// <summary>Owns a temporary file or directory and removes it when disposed.</summary>
/// <remarks>
///     Dispose explicitly. Directory disposal removes its contents, without following directory links. Do not
///     transfer ownership by renaming or replacing the owned entry.
/// </remarks>
public sealed class TemporaryPath : IDisposable
{
    /// <summary>Gets the allocated absolute path.</summary>
    public FilePath Path { get; }

    /// <summary>Gets whether this owner allocated a directory.</summary>
    public bool IsDirectory { get; }

    private bool _disposed;

    private readonly (ulong Device, ulong Low, ulong High) _identity;

    private TemporaryPath(FilePath path, bool isDirectory)
    {
        Path        = path.Absolute();
        IsDirectory = isDirectory;
        _identity   = FileIdentity.Read(Path.Value);
    }

    /// <summary>Removes the owned entry if it still has its original filesystem identity.</summary>
    /// <remarks>
    ///     Repeated successful disposal is harmless. Missing entries are accepted; replacing an owned entry causes an
    ///     exception. These checks do not prevent concurrent filesystem races.
    /// </remarks>
    /// <exception cref="IOException">Identity changed or cleanup failed. Disposal can be retried after a cleanup failure.</exception>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Path.Exists || Path.IsSymbolicLink)
        {
            if (Path.IsSymbolicLink || FileIdentity.Read(Path.Value) != _identity)
            {
                throw new IOException("The temporary entry was replaced; refusing to delete its replacement.");
            }

            if (IsDirectory)
            {
                Directory.Delete(Path.Value, true);
            }
            else
            {
                Path.Unlink();
            }
        }

        _disposed = true;
    }

    /// <summary>Allocates an owned temporary file.</summary>
    /// <param name="directory">An existing parent, or null for the system temporary directory.</param>
    /// <param name="prefix">A filename prefix without separators.</param>
    /// <returns>An owner that must be disposed.</returns>
    public static TemporaryPath CreateFile(FilePath? directory = null, string prefix = "Caprine-") => new(FilePath.CreateTemporaryFile(directory, prefix), false);

    /// <summary>Allocates an owned directory in the system temporary directory.</summary>
    /// <param name="prefix">A filename prefix without separators.</param>
    /// <returns>An owner that must be disposed.</returns>
    public static TemporaryPath CreateDirectory(string prefix = "Caprine-") => new(FilePath.CreateTemporaryDirectory(prefix), true);
}
