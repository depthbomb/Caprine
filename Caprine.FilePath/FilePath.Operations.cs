namespace Caprine.FilePath;

public sealed partial class FilePath
{
    /// <summary>Copies this file, link, or directory tree to an exact destination path.</summary>
    /// <param name="target">The exact destination, whose parent must exist.</param>
    /// <param name="options">Overwrite, directory merge, link, and metadata policies. Null uses defaults.</param>
    /// <param name="ct">Cancels between entries. Completed copies remain after cancellation or failure.</param>
    /// <returns>The destination path.</returns>
    /// <remarks>Existing destination directory links are rejected during merges. Recursive copies reject destinations inside the source. Filesystem races can invalidate these checks.</remarks>
    /// <exception cref="IOException">A destination conflicts, a cycle is encountered, or copying fails.</exception>
    public FilePath CopyTo(FilePath target, PathCopyOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        options ??= new PathCopyOptions();
        EnsureDistinctDestination(target);
        var pending = new Stack<(FilePath Source, FilePath Target, HashSet<FilePath> Ancestors, bool Finish)>();
        pending.Push((this, target, [], false));
        while (pending.TryPop(out var item))
        {
            ct.ThrowIfCancellationRequested();
            if (item.Finish)
            {
                CopyMetadata(item.Source, item.Target);
                continue;
            }

            var link = item.Source.LinkTarget;
            _ = File.GetAttributes(item.Source.Value);
            if (link is not null && !options.FollowSymbolicLinks)
            {
                PrepareFileDestination(item.Target, options.Overwrite, removeExisting: true);
                item.Target.CreateSymbolicLink(new FilePath(link), (File.GetAttributes(item.Source.Value) & FileAttributes.Directory) != 0);
                continue;
            }

            if (item.Source.IsDirectory)
            {
                var resolved = item.Source.Resolve(true);
                if (!item.Ancestors.Add(resolved))
                {
                    throw new IOException($"A directory link cycle was encountered at {item.Source}.");
                }

                if (item.Target.IsSymbolicLink || item.Target.IsFile || (item.Target.IsDirectory && !options.MergeDirectories))
                {
                    throw new IOException($"Destination already exists: {item.Target}");
                }

                item.Target.CreateDirectory(existOk: options.MergeDirectories);
                if (options.PreserveMetadata)
                {
                    pending.Push((item.Source, item.Target, item.Ancestors, true));
                }

                foreach (var child in item.Source.IterDir())
                {
                    var destination = item.Target / child.Name;
                    pending.Push((child, destination, new HashSet<FilePath>(item.Ancestors), false));
                }
            }
            else
            {
                if (item.Target.Exists && item.Source.SameFile(item.Target))
                {
                    throw new IOException("Source and destination refer to the same file.");
                }

                PrepareFileDestination(item.Target, options.Overwrite);
                File.Copy(item.Source.Value, item.Target.Value, options.Overwrite);
                if (options.PreserveMetadata)
                {
                    CopyMetadata(item.Source, item.Target);
                }
            }
        }

        return target;
    }

    /// <summary>Copies this entry into an existing directory, retaining its filename.</summary>
    /// <param name="directory">An existing destination directory.</param>
    /// <param name="options">Copy policies.</param>
    /// <param name="ct">Cancels between entries.</param>
    /// <returns>The destination directory joined with this entry's filename.</returns>
    /// <exception cref="DirectoryNotFoundException">The destination directory does not exist.</exception>
    public FilePath CopyInto(FilePath directory, PathCopyOptions? options = null, CancellationToken ct = default) =>
        CopyTo(IntoTarget(directory), options, ct);

    /// <summary>Moves this entry to an exact destination.</summary>
    /// <param name="target">The exact destination, whose parent must exist.</param>
    /// <param name="overwrite">Whether an existing file may be replaced. Existing directories are never replaced or merged.</param>
    /// <param name="copyDirectoryOnFailure">Allows a failed directory rename to fall back to copying then deleting the source when the destination is absent. Defaults to false.</param>
    /// <returns>The destination path.</returns>
    /// <remarks>File moves use BCL behavior, including copy/delete across volumes. Directory fallback is non-atomic and can leave a partial destination on failure. Links are moved as entries.</remarks>
    /// <exception cref="IOException">A destination conflicts or the move fails.</exception>
    public FilePath MoveTo(FilePath target, bool overwrite = false, bool copyDirectoryOnFailure = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        EnsureDistinctDestination(target);
        if (IsDirectory || (IsSymbolicLink && (File.GetAttributes(_path) & FileAttributes.Directory) != 0))
        {
            try
            {
                Directory.Move(_path, target.Value);
            }
            catch (IOException) when (copyDirectoryOnFailure && !IsSymbolicLink && !target.Exists && !target.IsSymbolicLink)
            {
                CopyTo(target, new PathCopyOptions { PreserveMetadata = true });
                Directory.Delete(_path, true);
            }
        }
        else
        {
            if (target.IsDirectory)
            {
                throw new IOException("A file cannot replace a directory.");
            }

            File.Move(_path, target.Value, overwrite);
        }

        return target;
    }

    /// <summary>Moves this entry into an existing directory, retaining its filename.</summary>
    /// <param name="directory">The existing destination directory.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="copyDirectoryOnFailure">Whether to allow non-atomic directory copy/delete fallback.</param>
    /// <returns>The destination directory joined with this entry's filename.</returns>
    public FilePath MoveInto(FilePath directory, bool overwrite = false, bool copyDirectoryOnFailure = false) =>
        MoveTo(IntoTarget(directory), overwrite, copyDirectoryOnFailure);

    /// <summary>Moves an entry to an exact destination without replacing an existing entry.</summary>
    /// <param name="target">The exact destination.</param>
    /// <returns>The destination path.</returns>
    public FilePath Rename(FilePath target) => MoveTo(target);

    /// <summary>Moves an entry to an exact destination without replacing an existing entry.</summary>
    /// <param name="target">The exact destination text.</param>
    /// <returns>The destination path.</returns>
    public FilePath Rename(string target) => MoveTo(new FilePath(target));

    /// <summary>Moves a file to an exact destination, replacing an existing file.</summary>
    /// <param name="target">The exact destination.</param>
    /// <returns>The destination path.</returns>
    /// <remarks>Directory replacement is deliberately unsupported on every platform. Use MoveTo for directory moves.</remarks>
    /// <exception cref="IOException">Either entry is a directory or the replacement fails.</exception>
    public FilePath Replace(FilePath target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (IsDirectory || target.IsDirectory)
        {
            throw new IOException("Replace supports files only. Use MoveTo for directories.");
        }

        return MoveTo(target, overwrite: true);
    }

    /// <summary>Moves a file to an exact destination, replacing an existing file.</summary>
    /// <param name="target">The exact destination text.</param>
    /// <returns>The destination path.</returns>
    public FilePath Replace(string target) => Replace(new FilePath(target));

    private FilePath IntoTarget(FilePath directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!directory.IsDirectory)
        {
            throw new DirectoryNotFoundException($"Destination directory does not exist: {directory}");
        }

        if (Name.Length == 0)
        {
            throw new IOException("The source has no filename.");
        }

        return directory / Name;
    }

    private void EnsureDistinctDestination(FilePath target)
    {
        var source = Resolve();
        var destination = target.Resolve();
        if (source == destination || (IsDirectory && destination.IsRelativeTo(source)) ||
            (Exists && target.Exists && SameFile(target)))
        {
            throw new IOException("The destination is the source, an alias of it, or inside the source directory.");
        }
    }

    private static void PrepareFileDestination(FilePath target, bool overwrite, bool removeExisting = false)
    {
        if (target.IsDirectory)
        {
            throw new IOException($"A directory cannot be replaced by a file: {target}");
        }

        if (target.IsSymbolicLink)
        {
            if (!overwrite)
            {
                throw new IOException($"Destination already exists: {target}");
            }

            target.Unlink();
        }
        else if (!overwrite && target.Exists)
        {
            throw new IOException($"Destination already exists: {target}");
        }
        else if (overwrite && removeExisting && target.Exists)
        {
            target.Unlink();
        }
    }

    private static void CopyMetadata(FilePath source, FilePath target)
    {
        File.SetCreationTimeUtc(target.Value, File.GetCreationTimeUtc(source.Value));
        File.SetLastAccessTimeUtc(target.Value, File.GetLastAccessTimeUtc(source.Value));
        File.SetLastWriteTimeUtc(target.Value, File.GetLastWriteTimeUtc(source.Value));
        File.SetAttributes(target.Value, File.GetAttributes(source.Value) & ~FileAttributes.ReparsePoint);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target.Value, File.GetUnixFileMode(source.Value));
        }
    }
}
