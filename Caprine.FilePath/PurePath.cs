using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Caprine.FilePath;

/// <summary>An immutable path value whose operations never access the filesystem.</summary>
/// <remarks>
///     Equality includes the path flavor and normalized text. Windows values ignore case using ordinal rules; POSIX
///     values compare case-sensitively. Parent components are retained. Derived result values preserve their runtime
///     flavor.
/// </remarks>
public class PurePath : IEquatable<PurePath>, IComparable<PurePath>
{
    /// <summary>Gets the normalized path text, retaining parent components.</summary>
    public string Value => Syntax.Value;

    /// <summary>Gets the path's resolved Windows or POSIX flavor.</summary>
    public PathFlavor Flavor => Syntax.Flavor;

    /// <summary>Gets the drive or UNC share, if present.</summary>
    public string Drive => Syntax.Drive;

    /// <summary>Gets the root separator, if present.</summary>
    public string Root => Syntax.Root;

    /// <summary>Gets the complete drive and root.</summary>
    public string Anchor => Syntax.Anchor;

    /// <summary>Gets whether the path identifies its drive and root independently of the working directory.</summary>
    public bool IsAbsolute => Syntax.IsAbsolute;

    /// <summary>Gets whether the path has a drive or root.</summary>
    public bool IsRooted => Anchor.Length > 0;

    /// <summary>Gets immutable components, including the anchor as the first component when present.</summary>
    public ImmutableArray<string> Parts => field.IsDefault ? field = Anchor.Length == 0 ? Syntax.Segments : [Anchor, .. Syntax.Segments] : field;

    /// <summary>Gets the final component, or an empty string for a root or current-directory path.</summary>
    public string Name => Syntax.Segments.IsEmpty ? "" : Syntax.Segments[^1];

    /// <summary>Gets the final suffix. A leading dot alone does not introduce a suffix.</summary>
    public string Suffix
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            var name = Name.TrimStart('.');
            var dot  = name.LastIndexOf('.');

            return field = dot < 0 ? "" : name[dot..];
        }
    }
    /// <summary>Gets all suffixes in their original order.</summary>
    public ImmutableArray<string> Suffixes
    {
        get
        {
            if (!field.IsDefault)
            {
                return field;
            }

            var name   = Name.TrimStart('.');
            var pieces = name.Split('.');
            return field = [.. pieces.Skip(1).Select(static piece => "." + piece)];
        }
    }

    /// <summary>Gets the final component without its final suffix.</summary>
    public string Stem => field ??= Name[..(Name.Length - Suffix.Length)];

    /// <summary>Gets the lexical parent. A root or current-directory path is its own parent.</summary>
    public PurePath Parent => Syntax.Segments.IsEmpty ? this : Create(PathSyntax.Compose(Drive, Root, Syntax.Segments.RemoveAt(Syntax.Segments.Length - 1), Syntax.Separator));

    /// <summary>Gets proper ancestors, nearest first, stopping at the root or current directory.</summary>
    public IEnumerable<PurePath> Parents
    {
        get
        {
            var current = this;
            while (!current.Syntax.Segments.IsEmpty)
            {
                current = current.Parent;
                yield return current;
            }
        }
    }

    internal PathSyntax Syntax { get; }

    /// <summary>Creates a path using the requested flavor. Empty text means the current directory.</summary>
    /// <param name="path">
    ///     Path text; empty text becomes ".". Repeated separators and "." components are removed; ".." is
    ///     retained.
    /// </param>
    /// <param name="flavor">Parsing and comparison rules; Native resolves to the current operating system.</param>
    /// <exception cref="ArgumentNullException">path is null.</exception>
    /// <exception cref="ArgumentException">path contains a null character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">flavor is not a defined value.</exception>
    public PurePath(string path = "", PathFlavor flavor = PathFlavor.Native)
    {
        Syntax = new PathSyntax(path, flavor);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PurePath path && Equals(path);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Flavor, Syntax.Comparer.GetHashCode(Value));

    /// <inheritdoc />
    public bool Equals(PurePath? other) => other is not null && Flavor == other.Flavor && Syntax.Comparer.Equals(Value, other.Value);

    /// <inheritdoc />
    public int CompareTo(PurePath? other)
    {
        if (other is null)
        {
            return 1;
        }

        EnsureSameFlavor(other);
        return Syntax.Comparer.Compare(Value, other.Value);
    }

    /// <summary>Joins components, respecting absolute paths and Windows drive-relative paths.</summary>
    /// <param name="segments">
    ///     Components to join. Empty components are ignored; an absolute component replaces the preceding
    ///     path. A Windows rooted component retains the current drive.
    /// </param>
    /// <returns>A new path of the same flavor, or this path when no segments are supplied.</returns>
    /// <exception cref="ArgumentNullException">segments or one of its elements is null.</exception>
    public PurePath JoinPath(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var current = this;
        foreach (var segment in segments)
        {
            var next  = new PurePath(segment, Flavor);
            var drive = current.Drive;
            var root  = current.Root;
            var tail  = current.Syntax.Segments;
            if (next.Drive.Length > 0 && !Syntax.Comparer.Equals(next.Drive, drive))
            {
                drive = next.Drive;
                root  = next.Root;
                tail  = [];
            }

            if (next.Root.Length > 0)
            {
                root = next.Root;
                tail = [];
            }

            current = Create(PathSyntax.Compose(drive, root, tail.AddRange(next.Syntax.Segments), Syntax.Separator));
        }

        return current;
    }

    /// <summary>Returns a path with a different final filename.</summary>
    /// <param name="name">
    ///     A nonempty filename component, excluding "." and "..", separators, and Windows reserved characters
    ///     for Windows paths.
    /// </param>
    /// <returns>A path with the same parent and the new filename.</returns>
    /// <exception cref="ArgumentException">name is not a valid filename component.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public PurePath WithName(string name)
    {
        PathSyntax.ValidateName(name, Flavor);
        if (Name.Length == 0)
        {
            throw new InvalidOperationException("A root or current-directory path has no filename to replace.");
        }

        return Parent.JoinPath(name);
    }

    /// <summary>Replaces the stem while retaining the final suffix.</summary>
    /// <param name="stem">A nonempty new stem. The resulting filename must be one valid component.</param>
    /// <returns>A path with the final suffix retained and the stem replaced.</returns>
    /// <exception cref="ArgumentException">stem is empty or produces an invalid filename.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public PurePath WithStem(string stem)
    {
        ArgumentException.ThrowIfNullOrEmpty(stem);
        return WithName(stem + Suffix);
    }

    /// <summary>Replaces or removes the final suffix.</summary>
    /// <param name="suffix">An empty string to remove the suffix, or a dot-prefixed suffix. A single dot is allowed.</param>
    /// <returns>A path with its final suffix replaced.</returns>
    /// <exception cref="ArgumentException">suffix does not start with a dot or contains invalid filename characters.</exception>
    /// <exception cref="InvalidOperationException">This path has no filename.</exception>
    public PurePath WithSuffix(string suffix)
    {
        ArgumentNullException.ThrowIfNull(suffix);
        if (suffix.Length > 0 && suffix[0] != '.')
        {
            throw new ArgumentException("A suffix must begin with a dot or be empty.", nameof(suffix));
        }

        PathSyntax.ValidateName("name" + suffix, Flavor);
        return WithName(Stem           + suffix);
    }

    /// <summary>Tests lexical ancestry without resolving parent components or symbolic links.</summary>
    /// <param name="other">The possible ancestor, compared component by component using flavor-specific case rules.</param>
    /// <returns>True for this path itself or a lexical descendant with the same anchor and flavor.</returns>
    /// <remarks>
    ///     This does not establish filesystem containment. Resolve native paths first when links or parent components
    ///     matter.
    /// </remarks>
    public bool IsRelativeTo(PurePath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Flavor                 == other.Flavor                 && Syntax.Comparer.Equals(Anchor, other.Anchor) &&
               Syntax.Segments.Length >= other.Syntax.Segments.Length &&
               Syntax.Segments.Take(other.Syntax.Segments.Length).SequenceEqual(other.Syntax.Segments, Syntax.Comparer);
    }

    /// <summary>Returns a lexical relative path, optionally allowing traversal up from the base.</summary>
    /// <param name="other">The base path, with the same flavor and anchor.</param>
    /// <param name="walkUp">Whether to emit ".." components for a non-ancestor base. Defaults to false.</param>
    /// <returns>A relative path; identical paths produce ".".</returns>
    /// <exception cref="ArgumentException">
    ///     Flavors or anchors differ, the base is not an ancestor when walkUp is false, or the
    ///     unmatched base contains "..".
    /// </exception>
    public PurePath RelativeTo(PurePath other, bool walkUp = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameFlavor(other);
        if (!Syntax.Comparer.Equals(Anchor, other.Anchor))
        {
            throw new ArgumentException("Paths with different anchors cannot be made relative.", nameof(other));
        }

        var common = 0;
        while (common < Syntax.Segments.Length && common < other.Syntax.Segments.Length &&
               Syntax.Comparer.Equals(Syntax.Segments[common], other.Syntax.Segments[common]))
        {
            common++;
        }

        if (!walkUp && common != other.Syntax.Segments.Length ||
            walkUp  && other.Syntax.Segments.Skip(common).Contains(".."))
        {
            throw new ArgumentException("The path is not below the base, or the base contains unresolved parent components.", nameof(other));
        }

        return Create(PathSyntax.Compose("", "", Enumerable.Repeat("..", other.Syntax.Segments.Length - common).Concat(Syntax.Segments.Skip(common)), Syntax.Separator));
    }

    /// <summary>Attempts a lexical relative conversion.</summary>
    /// <param name="other">The base path; null is a programmer error and throws.</param>
    /// <param name="result">The relative path on success, otherwise null.</param>
    /// <param name="walkUp">Whether to allow parent hops from the base. Defaults to false.</param>
    /// <returns>False when the relative conversion is impossible under the requested rules.</returns>
    public bool TryRelativeTo(PurePath other, [NotNullWhen(true)] out PurePath? result, bool walkUp = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        try
        {
            result = RelativeTo(other, walkUp);
            return true;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }

    /// <summary>Formats Windows separators as slashes; POSIX filename characters are preserved.</summary>
    /// <returns>Path text using forward slashes for separators. Backslashes in POSIX filenames remain unchanged.</returns>
    public string AsPosix() => Flavor == PathFlavor.Windows ? Value.Replace('\\', '/') : Value;

    /// <summary>Matches trailing components, or the whole path for an anchored pattern. ** acts as *.</summary>
    /// <param name="pattern">A nonempty glob with *, ?, and bracket classes.</param>
    /// <param name="caseSensitive">Null uses this flavor's case rules.</param>
    /// <returns>Whether the path matches without filesystem access.</returns>
    public bool Match(string pattern, bool? caseSensitive = null) => new GlobPattern(pattern, Flavor, caseSensitive).Matches(Syntax, false);

    /// <summary>Matches the entire path. ** consumes zero or more complete components.</summary>
    /// <param name="pattern">A nonempty glob, anchored when matching an anchored path.</param>
    /// <param name="caseSensitive">Null uses this flavor's case rules.</param>
    /// <returns>Whether all components match without filesystem access.</returns>
    public bool FullMatch(string pattern, bool? caseSensitive = null) => new GlobPattern(pattern, Flavor, caseSensitive).Matches(Syntax, true);

    /// <summary>Compares path values using their flavor's rules.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>True for equal normalized paths of the same flavor, or two nulls.</returns>
    public static bool operator ==(PurePath? left, PurePath? right) => Equals(left, right);

    /// <summary>Compares path values using their flavor's rules.</summary>
    /// <param name="left">The first path, possibly null.</param>
    /// <param name="right">The second path, possibly null.</param>
    /// <returns>True when normalized path values or flavors differ.</returns>
    public static bool operator !=(PurePath? left, PurePath? right) => !Equals(left, right);

    /// <summary>Joins path text to a pure path.</summary>
    /// <param name="left">The starting path.</param>
    /// <param name="right">Path text to join, using the left path flavor.</param>
    /// <returns>The joined path.</returns>
    public static PurePath operator /(PurePath left, string right) => left.JoinPath(right);

    /// <summary>Joins paths of the same flavor.</summary>
    /// <param name="left">The starting path.</param>
    /// <param name="right">The path to join.</param>
    /// <returns>The joined path.</returns>
    /// <exception cref="ArgumentException">The path flavors differ.</exception>
    public static PurePath operator /(PurePath left, PurePath right)
    {
        left.EnsureSameFlavor(right);
        return left.JoinPath(right.Value);
    }

    private void EnsureSameFlavor(PurePath other)
    {
        if (Flavor != other.Flavor)
        {
            throw new ArgumentException("Windows and POSIX paths cannot be combined or ordered.", nameof(other));
        }
    }

    /// <summary>Creates a result with the same path flavor.</summary>
    /// <param name="value">Normalized result text.</param>
    /// <returns>A new pure path with this path flavor. Derived classes can retain their runtime type.</returns>
    protected virtual PurePath Create(string value) => new(value, Flavor);
}
