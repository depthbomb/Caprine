using System.Collections.Immutable;

namespace Caprine.FilePath;

internal sealed class PathSyntax
{
    internal string Value { get; }
    internal string Drive { get; }
    internal string Root { get; }
    internal string Anchor => Drive + Root;
    internal PathFlavor Flavor { get; }
    internal ImmutableArray<string> Segments { get; }
    internal char Separator => Flavor == PathFlavor.Windows ? '\\' : '/';
    internal StringComparer Comparer => Flavor == PathFlavor.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    internal bool IsAbsolute => Root.Length > 0 && (Flavor == PathFlavor.Posix || Drive.Length > 0);

    internal PathSyntax(string path, PathFlavor flavor)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Contains('\0'))
        {
            throw new ArgumentException("A path cannot contain a null character.", nameof(path));
        }

        Flavor = flavor switch
        {
            PathFlavor.Native => OperatingSystem.IsWindows() ? PathFlavor.Windows : PathFlavor.Posix,
            PathFlavor.Windows or PathFlavor.Posix => flavor,
            _ => throw new ArgumentOutOfRangeException(nameof(flavor))
        };

        var text = Flavor == PathFlavor.Windows ? path.Replace('/', '\\') : path;
        (Drive, Root, var start) = ReadAnchor(text, Flavor);
        var tail = text.AsSpan(start);
        var segments = ImmutableArray.CreateBuilder<string>(tail.IsEmpty ? 0 : tail.Count(Separator) + 1);
        var componentLength = 0;
        foreach (var range in tail.Split(Separator))
        {
            var segment = tail[range];
            if (!segment.IsEmpty && segment is not ".")
            {
                segments.Add(segment.ToString());
                componentLength += segment.Length;
            }
        }

        Segments = segments.DrainToImmutable();
        var normalizedLength = Drive.Length + Root.Length + componentLength + Math.Max(0, Segments.Length - 1);
        Value = normalizedLength == text.Length && text.Length > 0 ? text : Compose(Drive, Root, Segments, Separator);
    }

    internal static string Compose(string drive, string root, IEnumerable<string> segments, char separator)
    {
        var value = drive + root + string.Join(separator, segments);

        return value.Length == 0 ? "." : value;
    }

    internal static void ValidateName(string name, PathFlavor flavor)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name is "." or ".." || name.Contains('\0') || name.Contains('/') ||
            (flavor == PathFlavor.Windows && (name.AsSpan().IndexOfAny("\\:<>\"|?*".AsSpan()) >= 0 || name.AsSpan().IndexOfAnyInRange('\u0001', '\u001f') >= 0)))
        {
            throw new ArgumentException("A name must be a single filename component.", nameof(name));
        }
    }

    private static (string Drive, string Root, int Start) ReadAnchor(string text, PathFlavor flavor)
    {
        if (flavor == PathFlavor.Posix)
        {
            var count = 0;
            while (count < text.Length && text[count] == '/')
            {
                count++;
            }

            return ("", count == 2 ? "//" : count > 0 ? "/" : "", count);
        }

        var prefix = text.StartsWith("\\\\?\\", StringComparison.Ordinal) || text.StartsWith("\\\\.\\", StringComparison.Ordinal) ? 4 : 0;
        var uncStart = prefix > 0 && text.AsSpan(prefix).StartsWith("UNC\\", StringComparison.OrdinalIgnoreCase) ? 8 : prefix == 0 && text.StartsWith("\\\\", StringComparison.Ordinal) ? 2 : -1;
        if (uncStart >= 0)
        {
            var serverEnd = text.IndexOf('\\', uncStart);
            var shareStart = serverEnd + 1;
            if (serverEnd > uncStart && shareStart < text.Length && text[shareStart] != '\\')
            {
                var shareEnd = text.IndexOf('\\', shareStart);
                if (shareEnd < 0)
                {
                    return (text, "\\", text.Length);
                }

                return (text[..shareEnd], "\\", shareEnd + 1);
            }
        }

        if (text.Length >= prefix + 2 && char.IsAsciiLetter(text[prefix]) && text[prefix + 1] == ':')
        {
            var rooted = text.Length > prefix + 2 && text[prefix + 2] == '\\';

            return (text[..(prefix + 2)], rooted ? "\\" : "", prefix + (rooted ? 3 : 2));
        }

        if (prefix > 0)
        {
            var end = text.IndexOf('\\', prefix);

            return end < 0 ? (text, "", text.Length) : (text[..end], "\\", end + 1);
        }

        return text.StartsWith('\\') ? ("", "\\", 1) : ("", "", 0);
    }
}
