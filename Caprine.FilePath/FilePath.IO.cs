using System.Text;

namespace Caprine.FilePath;

public sealed partial class FilePath
{
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(false);

    /// <summary>Creates this directory, optionally creating missing parents.</summary>
    /// <param name="parents">Whether to create missing parent directories. Defaults to false.</param>
    /// <param name="existOk">Whether an existing directory is accepted. Defaults to false.</param>
    /// <remarks>The existing-directory check is not an exclusive creation primitive against concurrent callers.</remarks>
    /// <exception cref="IOException">An entry already exists and is not accepted.</exception>
    /// <exception cref="DirectoryNotFoundException">A parent is missing and parents is false.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void CreateDirectory(bool parents = false, bool existOk = false)
    {
        if (!existOk && Directory.Exists(_path))
        {
            throw new IOException($"Directory already exists: {_path}");
        }

        if (!parents && !Parent.IsDirectory)
        {
            throw new DirectoryNotFoundException($"Parent directory does not exist: {Parent}");
        }

        Directory.CreateDirectory(_path);
    }

    /// <summary>Creates a directory using the same rules as CreateDirectory.</summary>
    /// <param name="parents">Whether to create missing parents.</param>
    /// <param name="existOk">Whether to accept an existing directory.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void Mkdir(bool parents = false, bool existOk = false) => CreateDirectory(parents, existOk);

    /// <summary>Removes an empty directory or a directory link, without deleting a link's target.</summary>
    /// <exception cref="IOException">The directory is not empty or cannot be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void Rmdir() => Directory.Delete(_path, recursive: false);

    /// <summary>Removes a file or symbolic link, including a dangling link.</summary>
    /// <param name="missingOk">Whether a missing entry is accepted. Defaults to false.</param>
    /// <exception cref="IOException">The entry is a real directory or deletion fails.</exception>
    /// <exception cref="FileNotFoundException">The entry is missing and missingOk is false.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void Unlink(bool missingOk = false)
    {
        try
        {
            var attributes = File.GetAttributes(_path);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if ((attributes & FileAttributes.ReparsePoint) == 0)
                {
                    throw new IOException("Use Rmdir to remove a directory.");
                }

                Directory.Delete(_path, false);
            }
            else
            {
                File.Delete(_path);
            }
        }
        catch (FileNotFoundException) when (missingOk)
        {
        }
        catch (DirectoryNotFoundException) when (missingOk)
        {
        }
    }

    /// <summary>Creates an empty file or updates the modification time of an existing file without truncating it.</summary>
    /// <param name="existOk">False uses exclusive creation and fails if the name already exists. Defaults to true.</param>
    /// <param name="parents">Whether to create missing parents. Defaults to false.</param>
    /// <exception cref="IOException">Exclusive creation fails or the entry cannot be opened.</exception>
    /// <exception cref="DirectoryNotFoundException">A parent is missing and parents is false.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void Touch(bool existOk = true, bool parents = false)
    {
        if (parents)
        {
            Parent.CreateDirectory(parents: true, existOk: true);
        }

        using var stream = File.Open(_path, existOk ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        File.SetLastWriteTimeUtc(stream.SafeFileHandle, DateTime.UtcNow);
    }

    /// <summary>Creates an uncached FileInfo wrapper, without requiring the file to exist.</summary>
    /// <returns>A new BCL metadata wrapper.</returns>
    public FileInfo ToFileInfo() => new FileInfo(_path);

    /// <summary>Creates an uncached DirectoryInfo wrapper, without requiring the directory to exist.</summary>
    /// <returns>A new BCL metadata wrapper.</returns>
    public DirectoryInfo ToDirectoryInfo() => new DirectoryInfo(_path);

    /// <summary>Creates a DirectoryInfo for an existing directory, otherwise a FileInfo.</summary>
    /// <returns>A BCL metadata wrapper; this is not a metadata snapshot.</returns>
    public FileSystemInfo ToFileSystemInfo() => IsDirectory ? ToDirectoryInfo() : ToFileInfo();

    /// <summary>Creates a FileInfo wrapper. Prefer ToFileInfo for clarity.</summary>
    /// <returns>A new BCL metadata wrapper.</returns>
    [Obsolete("Use ToFileInfo().")]
    public FileInfo FileInfo() => ToFileInfo();

    /// <summary>Reads an immutable metadata snapshot, throwing when the entry cannot be inspected.</summary>
    /// <param name="followLinks">Whether to inspect the resolved target. Defaults to true.</param>
    /// <returns>Captured size, attributes, UTC timestamps, and link target.</returns>
    /// <remarks>Individual metadata fields and link resolution are not an atomic filesystem transaction.</remarks>
    /// <exception cref="IOException">The entry is missing or metadata cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public FilePathInfo Stat(bool followLinks = true)
    {
        var path = followLinks ? Resolve(true) : this;
        var attributes = File.GetAttributes(path.Value);
        var info = (attributes & FileAttributes.Directory) != 0 ? (FileSystemInfo)path.ToDirectoryInfo() : path.ToFileInfo();
        info.Refresh();
        return new FilePathInfo(path, info.Attributes, info is FileInfo file ? file.Length : null,
            info.CreationTimeUtc, info.LastAccessTimeUtc, info.LastWriteTimeUtc, LinkTarget);
    }

    /// <summary>Reads the entire file as text, detecting a leading byte-order mark.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public string ReadText(Encoding? encoding = null) => File.ReadAllText(_path, encoding ?? DefaultEncoding);

    /// <summary>Asynchronously reads the entire file as text, detecting a leading byte-order mark.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>A task containing the file contents.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task<string> ReadTextAsync(Encoding? encoding = null, CancellationToken ct = default) => File.ReadAllTextAsync(_path, encoding ?? DefaultEncoding, ct);

    /// <summary>Reads text asynchronously. Prefer ReadTextAsync.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>A task containing the file contents.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    [Obsolete("Use ReadTextAsync().")]
    public Task<string> ReadAllTextAsync(Encoding? encoding = null, CancellationToken ct = default) => ReadTextAsync(encoding, ct);

    /// <summary>Creates or truncates a file and writes text.</summary>
    /// <param name="content">The text to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM. An explicit encoding controls its preamble.</param>
    /// <remarks>Parents must exist. This operation is not atomic; use WriteTextAtomic for replacement.</remarks>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteText(string content, Encoding? encoding = null) => File.WriteAllText(_path, content, encoding ?? DefaultEncoding);

    /// <summary>Creates or truncates a file and writes a character span.</summary>
    /// <param name="content">The characters to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteText(ReadOnlySpan<char> content, Encoding? encoding = null) => File.WriteAllText(_path, content, encoding ?? DefaultEncoding);

    /// <summary>Asynchronously creates or truncates a file and writes text.</summary>
    /// <param name="content">The text to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing; a partial file may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task WriteTextAsync(string content, Encoding? encoding = null, CancellationToken ct = default) => File.WriteAllTextAsync(_path, content, encoding ?? DefaultEncoding, ct);

    /// <summary>Asynchronously creates or truncates a file and writes character memory.</summary>
    /// <param name="content">Memory that must remain unchanged until completion.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing; a partial file may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task WriteTextAsync(ReadOnlyMemory<char> content, Encoding? encoding = null, CancellationToken ct = default) => File.WriteAllTextAsync(_path, content, encoding ?? DefaultEncoding, ct);

    /// <summary>Writes text asynchronously. Prefer WriteTextAsync.</summary>
    /// <param name="content">The text to write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    [Obsolete("Use WriteTextAsync().")]
    public Task WriteAllTextAsync(string content, Encoding? encoding = null, CancellationToken ct = default) => WriteTextAsync(content, encoding, ct);

    /// <summary>Reads the entire file into memory.</summary>
    /// <returns>The file bytes.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public byte[] ReadBytes() => File.ReadAllBytes(_path);

    /// <summary>Asynchronously reads the entire file into memory.</summary>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>A task containing the file bytes.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task<byte[]> ReadBytesAsync(CancellationToken ct = default) => File.ReadAllBytesAsync(_path, ct);

    /// <summary>Creates or truncates a file and writes bytes.</summary>
    /// <param name="data">The bytes to write. Parents must already exist.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteBytes(ReadOnlySpan<byte> data) => File.WriteAllBytes(_path, data);

    /// <summary>Asynchronously creates or truncates a file and writes bytes.</summary>
    /// <param name="data">Memory that must remain unchanged until completion.</param>
    /// <param name="ct">Cancels writing; a partial file may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task WriteBytesAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => File.WriteAllBytesAsync(_path, data, ct);

    /// <summary>Lazily reads lines, removing line terminators and detecting a byte-order mark.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <returns>Lines read on demand. Dispose the enumerator to close the file when stopping early.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public IEnumerable<string> ReadLines(Encoding? encoding = null) => File.ReadLines(_path, encoding ?? DefaultEncoding);

    /// <summary>Lazily reads lines asynchronously.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>Lines without terminators; disposing the enumerator closes the file.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public IAsyncEnumerable<string> ReadLinesAsync(Encoding? encoding = null, CancellationToken ct = default) => File.ReadLinesAsync(_path, encoding ?? DefaultEncoding, ct);

    /// <summary>Creates or truncates a file and writes lines using the platform newline.</summary>
    /// <param name="lines">Lines to enumerate and write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void WriteLines(IEnumerable<string> lines, Encoding? encoding = null) => File.WriteAllLines(_path, lines, encoding ?? DefaultEncoding);

    /// <summary>Asynchronously creates or truncates a file and writes lines using the platform newline.</summary>
    /// <param name="lines">Lines to enumerate and write.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing; a partial file may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task WriteLinesAsync(IEnumerable<string> lines, Encoding? encoding = null, CancellationToken ct = default) => File.WriteAllLinesAsync(_path, lines, encoding ?? DefaultEncoding, ct);

    /// <summary>Appends text, creating the file if missing.</summary>
    /// <param name="content">Text to append.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM; use the file's existing encoding when appending.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void AppendText(string content, Encoding? encoding = null) => File.AppendAllText(_path, content, encoding ?? DefaultEncoding);

    /// <summary>Asynchronously appends text, creating the file if missing.</summary>
    /// <param name="content">Text to append.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing; partial appended text may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task AppendTextAsync(string content, Encoding? encoding = null, CancellationToken ct = default) => File.AppendAllTextAsync(_path, content, encoding ?? DefaultEncoding, ct);

    /// <summary>Appends lines using the platform newline, creating the file if missing.</summary>
    /// <param name="lines">Lines to append.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public void AppendLines(IEnumerable<string> lines, Encoding? encoding = null) => File.AppendAllLines(_path, lines, encoding ?? DefaultEncoding);

    /// <summary>Asynchronously appends lines using the platform newline, creating the file if missing.</summary>
    /// <param name="lines">Lines to append.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <param name="ct">Cancels writing; partial appended lines may remain.</param>
    /// <returns>A task completing after the file is closed.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    /// <exception cref="OperationCanceledException">ct requests cancellation. Asynchronous methods report cancellation through their task or enumeration.</exception>
    public Task AppendLinesAsync(IEnumerable<string> lines, Encoding? encoding = null, CancellationToken ct = default) => File.AppendAllLinesAsync(_path, lines, encoding ?? DefaultEncoding, ct);

    /// <summary>Opens a text reader with BOM detection. The caller must dispose it.</summary>
    /// <param name="encoding">Fallback encoding; null uses UTF-8.</param>
    /// <returns>An open text reader.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public StreamReader OpenText(Encoding? encoding = null) => new StreamReader(_path, encoding ?? DefaultEncoding);

    /// <summary>Opens a text writer. The caller must dispose it.</summary>
    /// <param name="append">True appends; false creates or truncates.</param>
    /// <param name="encoding">Null uses UTF-8 without a BOM.</param>
    /// <returns>An open text writer.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public StreamWriter OpenWrite(bool append = false, Encoding? encoding = null) => new StreamWriter(_path, append, encoding ?? DefaultEncoding);

    /// <summary>Opens an existing file for binary reading with read sharing.</summary>
    /// <returns>A stream owned by the caller.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public FileStream OpenRead() => File.OpenRead(_path);

    /// <summary>Opens a binary stream with explicit creation and access modes.</summary>
    /// <param name="mode">How the file is opened or created.</param>
    /// <param name="access">Requested access, defaulting to read/write.</param>
    /// <returns>A stream owned by the caller; sharing is disabled.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public FileStream Open(FileMode mode, FileAccess access = FileAccess.ReadWrite) => new FileStream(_path, mode, access);

    /// <summary>Opens a binary stream with explicit sharing, buffering, and asynchronous-handle options.</summary>
    /// <param name="mode">How the file is opened or created.</param>
    /// <param name="access">Requested access.</param>
    /// <param name="fileShare">Access permitted to other open handles.</param>
    /// <param name="bufferSize">Buffer size in bytes.</param>
    /// <param name="useAsync">Whether to request an asynchronous handle.</param>
    /// <returns>A stream owned by the caller.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public FileStream Open(FileMode mode, FileAccess access, FileShare fileShare, int bufferSize, bool useAsync) => new FileStream(_path, mode, access, fileShare, bufferSize, useAsync);

    /// <summary>Opens a binary stream using BCL stream options.</summary>
    /// <param name="options">Creation, access, sharing, buffering, and preallocation options.</param>
    /// <returns>A stream owned by the caller.</returns>
    /// <exception cref="IOException">The filesystem operation fails. Missing entries can report FileNotFoundException or DirectoryNotFoundException.</exception>
    /// <exception cref="UnauthorizedAccessException">The operating system denies the requested access.</exception>
    public FileStream Open(FileStreamOptions options) => new FileStream(_path, options);
}