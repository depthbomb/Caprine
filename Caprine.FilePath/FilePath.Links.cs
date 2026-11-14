namespace Caprine.FilePath;

public sealed partial class FilePath
{
    /// <summary>Gets whether this entry is a symbolic link, including a dangling link.</summary>
    /// <remarks>Access and filesystem errors can throw. A missing entry returns false.</remarks>
    public bool IsSymbolicLink => LinkTarget is not null;

    /// <summary>Gets the link's stored target text, or null for a non-link or missing entry.</summary>
    /// <remarks>A relative target is relative to the link's parent. This does not follow the target.</remarks>
    public string? LinkTarget => new FileInfo(_path).LinkTarget;

    /// <summary>Reads the immediate target of a symbolic link.</summary>
    /// <returns>The target as a native path, retaining relative targets.</returns>
    /// <exception cref="IOException">This entry is not a symbolic link.</exception>
    public FilePath ReadLink() => new FilePath(LinkTarget ?? throw new IOException($"The path is not a symbolic link: {_path}"));

    /// <summary>Creates a symbolic link at this path.</summary>
    /// <param name="target">The stored target; relative paths are interpreted from this link's parent.</param>
    /// <param name="isDirectory">Whether to create a directory link. Required for directory targets on Windows, including missing targets.</param>
    /// <returns>This link's path.</returns>
    /// <exception cref="IOException">The entry already exists or creation fails.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller lacks link-creation permission.</exception>
    public FilePath CreateSymbolicLink(FilePath target, bool isDirectory = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (isDirectory)
        {
            Directory.CreateSymbolicLink(_path, target.Value);
        }
        else
        {
            File.CreateSymbolicLink(_path, target.Value);
        }

        return this;
    }

    /// <summary>Creates a hard link at this path to an existing file on the same filesystem.</summary>
    /// <param name="target">The existing file. Relative paths use the working directory.</param>
    /// <returns>This new link's path.</returns>
    /// <remarks>Uses the .NET 11 hard-link API when available. Changes through either name affect the same file.</remarks>
    /// <exception cref="IOException">Creation fails, the destination exists, or the filesystems differ.</exception>
    public FilePath CreateHardLink(FilePath target)
    {
        ArgumentNullException.ThrowIfNull(target);
#if NET11_0_OR_GREATER
        File.CreateHardLink(_path, target.Value);
#else
        FileIdentity.CreateHardLink(_path, target.Value);
#endif
        return this;
    }

    /// <summary>Resolves an absolute path, following links in every component before processing parent components.</summary>
    /// <param name="strict">True requires every component to exist; false retains unresolved missing components. Defaults to false.</param>
    /// <returns>An absolute path with links and parent components resolved.</returns>
    /// <remarks>Resolution observes the filesystem at several instants and is not a security boundary against concurrent changes.</remarks>
    /// <exception cref="IOException">More than 40 links are followed, or a component is not a directory.</exception>
    /// <exception cref="FileNotFoundException">A required component is missing in strict mode.</exception>
    /// <exception cref="UnauthorizedAccessException">A component cannot be inspected.</exception>
    public FilePath Resolve(bool strict = false)
    {
        var anchored = this;
        if (!IsAbsolute)
        {
            if (OperatingSystem.IsWindows() && Drive.Length > 0)
            {
                anchored = new FilePath(Path.GetFullPath(Drive + ".")).JoinPath(Parts.Skip(1).ToArray());
            }
            else
            {
                anchored = Cwd().JoinPath(this);
            }
        }

        var pending = new Queue<string>(anchored.Parts.Skip(1));
        var resolved = new FilePath(anchored.Anchor);
        var links = 0;
        while (pending.TryDequeue(out var part))
        {
            if (part == "..")
            {
                resolved = resolved.Parent;
                continue;
            }

            var candidate = resolved / part;
            var target = candidate.LinkTarget;
            if (target is not null)
            {
                if (++links > 40)
                {
                    throw new IOException($"Too many symbolic links while resolving {_path}.");
                }

                var next = resolved / target;
                pending = new Queue<string>(next.Parts.Skip(1).Concat(pending));
                resolved = new FilePath(next.Anchor);
                continue;
            }

            try
            {
                var attributes = File.GetAttributes(candidate.Value);
                if (pending.Count > 0 && (attributes & FileAttributes.Directory) == 0)
                {
                    throw new IOException($"A path component is not a directory: {candidate}");
                }
            }
            catch (FileNotFoundException) when (!strict)
            {
            }
            catch (DirectoryNotFoundException) when (!strict)
            {
            }

            resolved = candidate;
        }

        if (strict)
        {
            _ = File.GetAttributes(resolved.Value);
        }

        return resolved;
    }

    /// <summary>Compares filesystem identities, following symbolic links and recognizing hard links.</summary>
    /// <param name="other">The other existing file or directory.</param>
    /// <returns>True when both paths identify the same volume and file identifier.</returns>
    /// <remarks>Supported on Windows, Linux with statx, and macOS. Concurrent replacement can change identity between calls.</remarks>
    /// <exception cref="IOException">An entry is missing or its identity cannot be read.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not provide a supported identity API.</exception>
    public bool SameFile(FilePath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FileIdentity.Read(Resolve(true).Value) == FileIdentity.Read(other.Resolve(true).Value);
    }
}
