# Caprine.FilePath

An immutable path API for .NET 10 and .NET 11, inspired by Python's `pathlib`.
The package includes XML IntelliSense documentation for every public API. Building
from source requires the .NET 11 SDK and C# 15.

```csharp
using Caprine.FilePath;
using PathValue = Caprine.FilePath.FilePath;

internal static class Example
{
    internal static async Task RunAsync(CancellationToken ct)
    {
        var folder = PathValue.Home() / "reports";
        folder.CreateDirectory(parents: true, existOk: true);

        var report = folder / "summary.txt";
        await report.WriteTextAtomicAsync("Ready", ct: ct);

        foreach (var file in folder.Glob("**/*.txt", ct: ct))
        {
            Console.WriteLine(file.RelativeTo(folder));
        }
    }
}
```

## Path values

`FilePath` uses the current operating system's rules and provides filesystem
operations. `PurePath` performs lexical operations only. `WindowsPath` and
`PosixPath` let you manipulate paths for another operating system without I/O.

- Empty text represents `.`. Empty join segments do nothing.
- Repeated separators, trailing separators, and `.` components are normalized.
  `..` components remain intact until you call `Absolute` or `Resolve`.
- Windows values compare with ordinal case-insensitive rules. POSIX values compare
  with ordinal case-sensitive rules, including on macOS. These are lexical rules,
  not a prediction of a particular volume's case behavior.
- `Value` is normalized stored text. `Absolute` makes it fully qualified without
  following links. `Resolve` follows links in each component before processing
  `..`. Set `strict: true` to require every component to exist.
- `Parts` and `Suffixes` are immutable arrays. A root is its own `Parent`;
  `Parents` contains only proper ancestors.
- `.gitignore` has no suffix. `.config.json` has suffix `.json`.
  `archive.tar.gz` has suffixes `.tar`, `.gz`. A trailing dot is a suffix.
- `WithName`, `WithStem`, and `WithSuffix` replace the final filename only.
- `RelativeTo` requires a lexical descendant by default. Use `walkUp: true`
  to produce parent hops, or `TryRelativeTo` for a non-throwing conversion.
  These operations do not prove filesystem containment.
- `IParsable<FilePath>`, `ISpanParsable<FilePath>`, and the included
  `System.Text.Json` converter support integration with generic APIs. JSON uses
  normalized strings, including dictionary keys.

Pure path operations retain the concrete runtime flavor; their declared result
type is `PurePath`. POSIX backslashes remain filename characters, including in
`AsPosix()`.

## Matching and traversal

Patterns support `*`, `?`, `[abc]`, `[a-z]`, and `[!abc]`.
A bracket expression can include a literal closing bracket first, as in `[]]`.
An unmatched opening bracket is literal. Hidden files are ordinary matches.

`Match("*.txt")` checks trailing components. An anchored pattern checks the
whole path. In `Match`, `**` behaves like `*`.
`FullMatch("**/*.txt")` checks the whole path and lets a whole `**` component
consume zero or more components.

`Glob("sub/*.txt")` searches below this directory; `RGlob("*.txt")` is shorthand
for `Glob("**/*.txt")`. Patterns must be relative. A trailing separator limits
results to directories. A bare `**` includes the starting directory.
A file or missing receiver produces no matches.

`EnumerateEntries`, `EnumerateFiles`, and `EnumerateDirectories` enumerate
immediate children by default. Pass `PathTraversalOptions` to recurse.
`Walk` recursively yields immutable `DirectoryVisit` snapshots in pre-order.

Options control maximum directory depth (root is zero), hidden entries,
access-denied handling, case sensitivity, link following, and `ShouldDescend`
pruning. Directory links are returned but not followed by default. When link
following is enabled, repeated resolved directory paths are skipped. Enumeration
is lazy, unsorted, cancellable, and buffers the immediate children of one visited
directory. Filesystem changes can invalidate entries after enumeration.

## Reading, writing, and metadata

Text writes default to UTF-8 without a BOM. Pass an encoding to select another
encoding or request a BOM. Text reads detect an existing BOM.

Use `ReadText` / `ReadBytes` for whole-file reads and `ReadLines` /
`ReadLinesAsync` for streaming. `WriteLines`, `AppendText`, and `AppendLines`
have async counterparts. Text and binary writes also accept spans or memory.
`OpenRead`, `OpenText`, `OpenWrite` (a text writer), and `Open(FileStreamOptions)`
return streams or readers/writers owned by the caller.

`Touch` preserves file contents. It creates parents only with `parents: true`.
`existOk: false` uses exclusive file creation. `CreateDirectory` / `Mkdir`
also require explicit parent creation; their existing-directory check is not
an exclusive creation primitive.

`Stat` reads an immutable snapshot and throws when inspection fails.
It follows links by default. Set `followLinks: false` to inspect the entry.
`ToFileInfo`, `ToDirectoryInfo`, and `ToFileSystemInfo` expose BCL wrappers
whose cached metadata can be refreshed. `Exists`, `IsFile`, and `IsDirectory`
use BCL existence checks, which can return false for inaccessible entries.

## Links, copying, and moving

`LinkTarget` reads stored link text; `ReadLink` returns it as a path.
`CreateSymbolicLink` creates a link at the receiver. Relative targets are
relative to the link's parent. Specify `isDirectory: true` for directory links
on Windows. `Unlink` removes links, including dangling links, without deleting
their targets. `Rmdir` removes empty directories.

`CreateHardLink` creates a file alias on the same filesystem. The .NET 11 build
uses `File.CreateHardLink`; the .NET 10 build uses the native platform API.
`SameFile` compares volume and file identifiers, recognizing both symbolic and
hard links. Native identity support covers Windows, Linux with `statx`, and
macOS. Other platforms throw `PlatformNotSupportedException`.

`CopyTo` and `MoveTo` take an exact destination. `CopyInto` and `MoveInto`
take an existing directory and retain the source filename. All return the
destination path. Destination parents must already exist.

Copy defaults reject existing entries and recreate symbolic links.
`PathCopyOptions` allows file overwrite, directory merging, following links,
and metadata preservation. Following a link cycle throws. Metadata preservation
covers timestamps, attributes, and Unix permission bits, not ownership, ACLs,
extended attributes, or a guarantee about alternate data streams. Existing
destination directory links are rejected during merges.

Moves never replace or merge existing directories. File moves use BCL behavior,
including copy/delete across volumes. Directory moves normally require a rename.
Opt into `copyDirectoryOnFailure` to allow a failed directory rename to copy
then delete when the destination is absent. This fallback is non-atomic.
`Replace` supports files only, consistently across operating systems.

Copying, directory fallback moves, and cancellation can leave partial destination
trees. Successful mutations do not update existing path values. Containment and
identity checks are not a security boundary against concurrent filesystem changes.

## Temporary entries and atomic writes

`CreateTemporaryFile` and `CreateTemporaryDirectory` allocate entries; the
caller owns cleanup. `TempDir` only returns the system temporary directory.
For scoped cleanup, use `TemporaryPath.CreateFile` or `CreateDirectory` in a
`using` statement. An owner rejects a replaced entry during disposal; a
directory owner recursively removes its contents.

`WriteTextAtomic` and `WriteBytesAtomic`, including async counterparts, write
a temporary sibling and rename it over the destination. A destination link is
replaced rather than followed. The old destination remains intact until rename
succeeds. Existing metadata is not preserved. `flushToDisk` flushes file data;
it does not guarantee directory-entry durability after power loss. Atomicity
depends on the filesystem's rename guarantees. Cancellation is checked before
rename and cannot undo a rename already in progress.

## Migrating from 0.1.1 to 0.2.0

This is a breaking release with a .NET 10 minimum.

- Use `Value` for stored text; `FullPath` is obsolete.
- `Parts` and `Suffixes` are immutable arrays.
- Equality uses normalized lexical values, without consulting the working directory.
- `IsAbsolute` requires a fully qualified path, including its Windows drive.
- `RelativeTo` is strict by default; opt into `walkUp` for parent hops.
- `Resolve` now follows links; `Absolute` retains lexical behavior.
- `Touch` no longer silently creates parents.
- Default text writes omit the UTF-8 BOM.
- `Stat` returns `FilePathInfo`, not a lazy BCL wrapper.
- Dotfiles, trailing dots, roots, and glob boundaries follow the rules above.
- `Replace` rejects directories on every platform.
- Use `ToFileInfo`, `ReadTextAsync`, and `WriteTextAsync` instead of the
  obsolete `FileInfo`, `ReadAllTextAsync`, and `WriteAllTextAsync` aliases.
