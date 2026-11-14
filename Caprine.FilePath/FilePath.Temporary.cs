using System.Text;

namespace Caprine.FilePath;

public sealed partial class FilePath
{
    /// <summary>Creates an empty file with a unique name using exclusive creation.</summary>
    /// <param name="directory">An existing parent directory; null uses the system temporary directory.</param>
    /// <param name="prefix">A filename prefix without separators. Defaults to Caprine-.</param>
    /// <returns>The allocated path. The caller owns cleanup.</returns>
    /// <exception cref="IOException">Allocation fails.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public static FilePath CreateTemporaryFile(FilePath? directory = null, string prefix = "Caprine-")
    {
        var (path, stream) = CreateTemporaryStream(directory ?? TempDir(), prefix, false);
        stream.Dispose();
        return path;
    }

    /// <summary>Creates a uniquely named directory in the system temporary directory.</summary>
    /// <param name="prefix">A filename prefix without separators. Defaults to Caprine-.</param>
    /// <returns>The allocated path. The caller owns recursive cleanup.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public static FilePath CreateTemporaryDirectory(string prefix = "Caprine-")
    {
        ValidateTemporaryPrefix(prefix);
        return new FilePath(Directory.CreateTempSubdirectory(prefix).FullName);
    }

    /// <summary>Writes bytes to a temporary sibling and replaces this file with a same-filesystem rename.</summary>
    /// <param name="data">The bytes to write.</param>
    /// <param name="flushToDisk">Whether to flush file data to disk before replacement. Defaults to false.</param>
    /// <remarks>Parents must exist. A destination link is replaced, not followed. Existing metadata is not preserved. Rename atomicity depends on the filesystem; directory-entry durability after power loss is not guaranteed.</remarks>
    /// <exception cref="IOException">Writing or replacement fails. The old destination remains intact until rename succeeds.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteBytesAtomic(ReadOnlySpan<byte> data, bool flushToDisk = false)
    {
        var (temporary, stream) = CreateTemporaryStream(Parent, ".caprine-", false);
        try
        {
            using (stream)
            {
                stream.Write(data);
                stream.Flush(flushToDisk);
            }

            File.Move(temporary.Value, _path, true);
        }
        finally
        {
            stream.Dispose();
            File.Delete(temporary.Value);
        }
    }

    /// <summary>Writes text to a temporary sibling and replaces this file with a same-filesystem rename.</summary>
    /// <param name="content">The text to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="flushToDisk">Whether to flush file data to disk before replacement.</param>
    /// <remarks>Uses the same replacement, metadata, and durability rules as WriteBytesAtomic.</remarks>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteTextAtomic(string content, Encoding? encoding = null, bool flushToDisk = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        var (temporary, stream) = CreateTemporaryStream(Parent, ".caprine-", false);
        try
        {
            using (stream)
            {
                using (var writer = new StreamWriter(stream, encoding ?? DefaultEncoding, leaveOpen: true))
                {
                    writer.Write(content);
                }

                stream.Flush(flushToDisk);
            }

            File.Move(temporary.Value, _path, true);
        }
        finally
        {
            stream.Dispose();
            File.Delete(temporary.Value);
        }
    }

    /// <summary>Asynchronously writes bytes to a temporary sibling, then replaces this file.</summary>
    /// <param name="data">Memory that must remain unchanged until completion.</param>
    /// <param name="flushToDisk">Whether to synchronously flush file data to disk before replacement.</param>
    /// <param name="ct">Cancels writing or stops before rename. Cancellation arriving after rename begins cannot undo replacement.</param>
    /// <returns>A task completing after replacement and temporary-file cleanup.</returns>
    /// <remarks>Uses the same replacement and durability rules as WriteBytesAtomic.</remarks>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public async Task WriteBytesAtomicAsync(ReadOnlyMemory<byte> data, bool flushToDisk = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (temporary, stream) = CreateTemporaryStream(Parent, ".caprine-", true);
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(data, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                if (flushToDisk)
                {
                    stream.Flush(true);
                }
            }

            ct.ThrowIfCancellationRequested();
            File.Move(temporary.Value, _path, true);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            File.Delete(temporary.Value);
        }
    }

    /// <summary>Asynchronously writes text to a temporary sibling, then replaces this file.</summary>
    /// <param name="content">The text to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="flushToDisk">Whether to synchronously flush file data before replacement.</param>
    /// <param name="ct">Cancels writing or stops before rename.</param>
    /// <returns>A task completing after replacement and temporary-file cleanup.</returns>
    /// <remarks>Uses the same replacement and durability rules as WriteBytesAtomic.</remarks>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public async Task WriteTextAtomicAsync(string content, Encoding? encoding = null, bool flushToDisk = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();
        var (temporary, stream) = CreateTemporaryStream(Parent, ".caprine-", true);
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                await using (var writer = new StreamWriter(stream, encoding ?? DefaultEncoding, leaveOpen: true))
                {
                    await writer.WriteAsync(content.AsMemory(), ct).ConfigureAwait(false);
                    await writer.FlushAsync(ct).ConfigureAwait(false);
                }

                if (flushToDisk)
                {
                    stream.Flush(true);
                }
            }

            ct.ThrowIfCancellationRequested();
            File.Move(temporary.Value, _path, true);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            File.Delete(temporary.Value);
        }
    }

    private static (FilePath Path, FileStream Stream) CreateTemporaryStream(FilePath directory, string prefix, bool asynchronous)
    {
        ValidateTemporaryPrefix(prefix);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var path = directory / (prefix + Guid.NewGuid().ToString("N"));
            try
            {
                return (path, new FileStream(path.Value, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = asynchronous ? FileOptions.Asynchronous : FileOptions.None
                }));
            }
            catch (IOException) when (path.Exists)
            {
            }
        }

        throw new IOException("Could not allocate a unique temporary file.");
    }

    private static void ValidateTemporaryPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        PathSyntax.ValidateName(prefix + "file", OperatingSystem.IsWindows() ? PathFlavor.Windows : PathFlavor.Posix);
    }
}