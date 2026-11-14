using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Caprine.FilePath;

/// <summary>An immutable native path with convenient filesystem operations.</summary>
/// <remarks>Lexical operations do not access the filesystem. Relative paths use the working directory when an I/O operation runs.</remarks>
[DebuggerDisplay("{Value}")]
[System.Text.Json.Serialization.JsonConverter(typeof(FilePathJsonConverter))]
public sealed partial class FilePath : IEquatable<FilePath>, IComparable<FilePath>, ISpanParsable<FilePath>
{
    /// <summary>Gets normalized path text without making it absolute or following links.</summary>
    public string Value => _path;
    /// <summary>Gets stored path text. Prefer Value or Absolute to make the intended operation explicit.</summary>
    [Obsolete("Use Value for stored path text or Absolute() for an absolute path.")]
    public string FullPath => _path;
    /// <summary>Gets immutable components including the anchor when present.</summary>
    public ImmutableArray<string> Parts => _pure.Parts;
    /// <summary>Gets the drive letter or UNC share.</summary>
    public string Drive => _pure.Drive;
    /// <summary>Gets the root separator.</summary>
    public string Root => _pure.Root;
    /// <summary>Gets the complete drive and root.</summary>
    public string Anchor => _pure.Anchor;
    /// <summary>Gets the lexical parent. Roots and the current-directory path are their own parents.</summary>
    public FilePath Parent => new FilePath(_pure.Parent.Value);
    /// <summary>Gets proper ancestors, nearest first, stopping at the root or current directory.</summary>
    public IEnumerable<FilePath> Parents => _pure.Parents.Select(static path => new FilePath(path.Value));
    /// <summary>Gets the final component, or an empty string for roots and the current-directory path.</summary>
    public string Name => _pure.Name;
    /// <summary>Gets the final component without its last suffix.</summary>
    public string Stem => _pure.Stem;
    /// <summary>Gets the last suffix, including its dot. Leading dots do not introduce suffixes.</summary>
    public string Suffix => _pure.Suffix;
    /// <summary>Gets immutable suffixes in their original order.</summary>
    public ImmutableArray<string> Suffixes => _pure.Suffixes;
    /// <summary>Checks whether a file or directory exists. Inaccessible or invalid paths can report false.</summary>
    public bool Exists => File.Exists(_path) || Directory.Exists(_path);
    /// <summary>Checks whether the path identifies a file.</summary>
    public bool IsFile => File.Exists(_path);
    /// <summary>Checks whether the path identifies a directory.</summary>
    public bool IsDirectory => Directory.Exists(_path);
    /// <summary>Gets whether the path is fully qualified independently of the working directory and drive.</summary>
    public bool IsAbsolute => _pure.IsAbsolute;
    /// <summary>Gets whether the path contains a drive or root.</summary>
    public bool IsRooted => _pure.IsRooted;

    private readonly PurePath _pure;
    private readonly string _path;

    /// <summary>Creates a normalized native path. Empty text means the current directory.</summary>
    /// <param name="path">Path text; empty means ".". Separators and "." components are normalized; ".." is retained.</param>
    /// <exception cref="ArgumentNullException">path is null.</exception>
    /// <exception cref="ArgumentException">path contains a null character.</exception>
    public FilePath(string path)
    {
        _pure = new PurePath(path);
        _path = _pure.Value;
    }

    /// <summary>Creates a native path by joining segments. No segments means the current directory.</summary>
    /// <param name="segments">Components to join. An absolute component replaces preceding components; empty components are ignored.</param>
    /// <exception cref="ArgumentNullException">segments or one of its elements is null.</exception>
    public FilePath(params string[] segments)
    {
        _pure = new PurePath().JoinPath(segments);
        _path = _pure.Value;
    }

    /// <inheritdoc />
    public override string ToString() => _path;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FilePath path && Equals(path);
    /// <inheritdoc />
    public override int GetHashCode() => _pure.GetHashCode();
    /// <inheritdoc />
    public bool Equals(FilePath? other) => other is not null && _pure.Equals(other._pure);
    /// <inheritdoc />
    public int CompareTo(FilePath? other) => other is null ? 1 : _pure.CompareTo(other._pure);
    /// <summary>Returns this path unchanged.</summary>
    /// <returns>This immutable instance.</returns>
    public FilePath JoinPath() => this;
    /// <summary>Joins segments, ignoring empty segments and respecting absolute paths.</summary>
    /// <param name="others">Components to join; Windows rooted components retain the preceding drive.</param>
    /// <returns>The joined native path.</returns>
    /// <exception cref="ArgumentNullException">others or an element is null.</exception>
    public FilePath JoinPath(params string[] others) => new FilePath(_pure.JoinPath(others).Value);
    /// <summary>Joins native paths using the same rules as string segments.</summary>
    /// <param name="others">Paths to join in order; absolute paths replace preceding components.</param>
    /// <returns>The joined native path.</returns>
    /// <exception cref="ArgumentNullException">others or an element is null.</exception>
    public FilePath JoinPath(params FilePath[] others)
    {
        ArgumentNullException.ThrowIfNull(others);
        return JoinPath(others.Select(static path => (path ?? throw new ArgumentNullException(nameof(others))).Value).ToArray());
    }

    /// <summary>Replaces the final filename with one valid filename component.</summary>
    /// <param name="newName">A nonempty filename component without separators, "." or "..". Windows paths also reject reserved filename characters.</param>
    /// <returns>A path with the same parent and the new filename.</returns>
    /// <exception cref="ArgumentException">newName is invalid.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public FilePath WithName(string newName) => new FilePath(_pure.WithName(newName).Value);
    /// <summary>Replaces the stem while retaining the final suffix.</summary>
    /// <param name="newStem">A nonempty stem that produces a valid filename.</param>
    /// <returns>A path with the new stem and the existing final suffix.</returns>
    /// <exception cref="ArgumentException">newStem is invalid.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public FilePath WithStem(string newStem) => new FilePath(_pure.WithStem(newStem).Value);
    /// <summary>Replaces or removes the final suffix.</summary>
    /// <param name="newSuffix">A dot-prefixed suffix, including "." alone, or empty text to remove the suffix.</param>
    /// <returns>A path with its final suffix replaced.</returns>
    /// <exception cref="ArgumentException">newSuffix has invalid syntax or filename characters.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public FilePath WithSuffix(string newSuffix) => new FilePath(_pure.WithSuffix(newSuffix).Value);
    /// <summary>Returns an absolute path, removing dot components without following symbolic links.</summary>
    /// <param name="basePath">An absolute base for relative paths; null uses the current working directory.</param>
    /// <returns>A fully qualified lexical path.</returns>
    /// <remarks>Use Resolve when links affect the meaning of "..". This operation does not require entries to exist.</remarks>
    /// <exception cref="ArgumentException">basePath is not fully qualified or the path is invalid for the operating system.</exception>
    public FilePath Absolute(FilePath? basePath = null) => new FilePath(basePath is null ? Path.GetFullPath(_path) : Path.GetFullPath(_path, basePath.Value));
    /// <summary>Returns a lexical relative path, optionally allowing traversal up from the base.</summary>
    /// <param name="other">The lexical base with the same anchor.</param>
    /// <param name="walkUp">Whether to allow ".." components for a non-ancestor base. Defaults to false.</param>
    /// <returns>A relative path, or "." for identical paths.</returns>
    /// <exception cref="ArgumentException">Anchors differ, the base is not an ancestor with walkUp false, or its unmatched components contain "..".</exception>
    public FilePath RelativeTo(FilePath other, bool walkUp = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new FilePath(_pure.RelativeTo(other._pure, walkUp).Value);
    }

    /// <summary>Attempts a lexical relative conversion without accessing the filesystem.</summary>
    /// <param name="other">The lexical base. Null is a programmer error and throws.</param>
    /// <param name="result">The relative path on success; otherwise null.</param>
    /// <param name="walkUp">Whether to allow parent hops from the base. Defaults to false.</param>
    /// <returns>Whether conversion succeeded under the requested rules.</returns>
    public bool TryRelativeTo(FilePath other, [NotNullWhen(true)] out FilePath? result, bool walkUp = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        var success = _pure.TryRelativeTo(other._pure, out var relative, walkUp);
        result = success ? new FilePath(relative!.Value) : null;
        return success;
    }

    /// <summary>Tests lexical ancestry without resolving parent components or links.</summary>
    /// <param name="other">The possible ancestor, compared by complete components using native case rules.</param>
    /// <returns>True for the same path or a lexical descendant.</returns>
    /// <remarks>This does not establish filesystem containment. It preserves ".." and does not inspect links.</remarks>
    public bool IsRelativeTo(FilePath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _pure.IsRelativeTo(other._pure);
    }

    /// <summary>Formats Windows separators as slashes, preserving POSIX filename characters.</summary>
    /// <returns>Text with forward-slash separators; backslashes in POSIX filenames are retained.</returns>
    public string AsPosix() => _pure.AsPosix();
    /// <summary>Gets the current working directory.</summary>
    /// <returns>The current absolute working directory.</returns>
    public static FilePath Cwd() => new FilePath(Directory.GetCurrentDirectory());
    /// <summary>Gets the current user's profile directory.</summary>
    /// <returns>The current user profile directory.</returns>
    /// <exception cref="DirectoryNotFoundException">The operating system reports no user profile directory.</exception>
    public static FilePath Home() => FromSpecialFolder(Environment.SpecialFolder.UserProfile);
    /// <summary>Creates a native path from text.</summary>
    /// <param name="value">Path text; empty means the current directory.</param>
    /// <returns>A normalized native path.</returns>
    /// <exception cref="ArgumentException">value contains a null character.</exception>
    public static FilePath From(string value) => new FilePath(value);
    /// <summary>Gets the system temporary directory without creating a directory.</summary>
    /// <returns>The configured temporary directory, without creating it or checking access.</returns>
    public static FilePath TempDir() => new FilePath(Path.GetTempPath());
    /// <summary>Creates an empty temporary file.</summary>
    /// <returns>A uniquely allocated empty file. The caller owns cleanup.</returns>
    /// <exception cref="IOException">The temporary file could not be allocated.</exception>
    public static FilePath TempFile() => new FilePath(Path.GetTempFileName());
    /// <summary>Gets a system special folder.</summary>
    /// <param name="specialFolder">The system folder identifier.</param>
    /// <returns>The folder path reported by the operating system.</returns>
    /// <exception cref="DirectoryNotFoundException">The folder has no path on this system.</exception>
    public static FilePath FromSpecialFolder(Environment.SpecialFolder specialFolder)
    {
        var path = Environment.GetFolderPath(specialFolder);
        return path.Length > 0 ? new FilePath(path) : throw new DirectoryNotFoundException($"The special folder {specialFolder} has no path on this system.");
    }
    /// <inheritdoc />
    public static FilePath Parse(string s, IFormatProvider? provider = null) => new FilePath(s);
    /// <inheritdoc />
    public static FilePath Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) => new FilePath(s.ToString());
    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [NotNullWhen(true)] out FilePath? result)
    {
        if (s is null || s.Contains('\0'))
        {
            result = null;
            return false;
        }

        result = new FilePath(s);
        return true;
    }
    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [NotNullWhen(true)] out FilePath? result) => TryParse(s.ToString(), provider, out result);
    /// <summary>Attempts to create a native path from text.</summary>
    /// <param name="s">Path text; empty is valid, null and embedded null characters are invalid.</param>
    /// <param name="result">The parsed path, or null on failure.</param>
    /// <returns>Whether the text is a valid lexical native path. Existence and OS-specific filename restrictions are not checked.</returns>
    public static bool TryParse(string? s, [NotNullWhen(true)] out FilePath? result) => TryParse(s, null, out result);
    /// <summary>Joins native paths.</summary>
    /// <param name="left">The starting path.</param>
    /// <param name="right">The path to join.</param>
    /// <returns>The joined native path.</returns>
    public static FilePath operator /(FilePath left, FilePath right) => left.JoinPath(right);
    /// <summary>Joins native path text.</summary>
    /// <param name="left">The starting path.</param>
    /// <param name="right">Path text to join.</param>
    /// <returns>The joined native path.</returns>
    public static FilePath operator /(FilePath left, string right) => left.JoinPath(right);
    /// <summary>Joins native path text.</summary>
    /// <param name="left">Starting path text.</param>
    /// <param name="right">The path to join.</param>
    /// <returns>The joined native path.</returns>
    public static FilePath operator /(string left, FilePath right) => new FilePath(left).JoinPath(right);
    /// <summary>Creates a native path from text.</summary>
    /// <param name="value">Native path text; empty means ".".</param>
    /// <returns>A normalized native path.</returns>
    public static implicit operator FilePath(string value) => new FilePath(value);
    /// <summary>Gets normalized path text.</summary>
    /// <param name="path">The non-null path to format.</param>
    /// <returns>Normalized native path text.</returns>
    public static explicit operator string(FilePath path) => path.Value;
    /// <summary>Compares normalized path values using native case rules.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator ==(FilePath? left, FilePath? right) => Equals(left, right);
    /// <summary>Compares normalized path values using native case rules.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator !=(FilePath? left, FilePath? right) => !Equals(left, right);
    /// <summary>Orders native path values, with null before other values.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator <(FilePath? left, FilePath? right) => left is null ? right is not null : left.CompareTo(right) < 0;
    /// <summary>Orders native path values, with null before other values.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator >(FilePath? left, FilePath? right) => left is not null && left.CompareTo(right) > 0;
    /// <summary>Orders native path values, with null before other values.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator <=(FilePath? left, FilePath? right) => !(left > right);
    /// <summary>Orders native path values, with null before other values.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>The result of the normalized lexical comparison using native case rules. Null sorts before non-null paths.</returns>
    public static bool operator >=(FilePath? left, FilePath? right) => !(left < right);
}
