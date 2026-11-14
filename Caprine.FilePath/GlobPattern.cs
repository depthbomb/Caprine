namespace Caprine.FilePath;

internal sealed class GlobPattern
{
    internal int  Depth     => _parts.Length;
    internal bool Recursive => Array.IndexOf(_parts, "**") >= 0;
    internal bool Rooted    => _anchor.Length              > 0;

    private readonly string[]   _parts;
    private readonly string     _anchor;
    private readonly PathFlavor _flavor;
    private readonly bool       _caseSensitive;

    internal GlobPattern(string pattern, PathFlavor flavor, bool? caseSensitive)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        var syntax = new PathSyntax(pattern, flavor);
        _parts         = [.. syntax.Segments];
        _anchor        = syntax.Anchor;
        _flavor        = syntax.Flavor;
        _caseSensitive = caseSensitive ?? _flavor != PathFlavor.Windows;
    }

    internal bool Matches(PathSyntax syntax, bool full)
    {
        if (_anchor.Length > 0 && !Equal(_anchor, syntax.Anchor))
        {
            return false;
        }

        if (full && _anchor.Length == 0 && syntax.Anchor.Length > 0)
        {
            return false;
        }

        return MatchesParts(syntax.Segments.AsSpan(), full);
    }

    internal bool MatchesParts(ReadOnlySpan<string> parts, bool full)
    {
        if (!full)
        {
            if (parts.Length < _parts.Length || _anchor.Length > 0 && parts.Length != _parts.Length)
            {
                return false;
            }

            for (var i = 0; i < _parts.Length; i++)
            {
                if (!MatchSegment(parts[parts.Length - _parts.Length + i], _parts[i]))
                {
                    return false;
                }
            }

            return true;
        }

        var length  = parts.Length + 1;
        var buffers = length <= 128 ? stackalloc bool[length * 2] : new bool[length * 2];
        buffers.Clear();
        var previous = buffers[..length];
        var next     = buffers[length..];
        previous[0] = true;
        foreach (var pattern in _parts)
        {
            next.Clear();
            if (pattern == "**")
            {
                next[0] = previous[0];
                for (var i = 1; i <= parts.Length; i++)
                {
                    next[i] = previous[i] || next[i - 1];
                }
            }
            else
            {
                for (var i = 1; i <= parts.Length; i++)
                {
                    next[i] = previous[i - 1] && MatchSegment(parts[i - 1], pattern);
                }
            }

            var swap = previous;
            previous = next;
            next     = swap;
        }

        return previous[parts.Length];
    }

    private bool Equal(string left, string right) => string.Equals(left, right, _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private char Fold(char value) => _caseSensitive ? value : char.ToUpperInvariant(value);

    private bool MatchSegment(string text, string pattern)
    {
        if (pattern is "*" or "**")
        {
            return true;
        }

        if (pattern.AsSpan().IndexOfAny('*', '?', '[') < 0)
        {
            return Equal(text, pattern);
        }

        var length  = text.Length + 1;
        var buffers = length <= 256 ? stackalloc bool[length * 2] : new bool[length * 2];
        buffers.Clear();
        var previous = buffers[..length];
        var next     = buffers[length..];
        previous[0] = true;
        for (var p = 0; p < pattern.Length; p++)
        {
            var token = pattern[p];
            var end   = token == '[' ? ClassEnd(pattern, p) : -1;
            next.Clear();
            if (token == '*')
            {
                next[0] = previous[0];
                for (var i = 1; i <= text.Length; i++)
                {
                    next[i] = previous[i] || next[i - 1];
                }
            }
            else
            {
                for (var i = 1; i <= text.Length; i++)
                {
                    next[i] = previous[i - 1] && (token == '?' || (end > p
                        ? InClass(text[i - 1], pattern.AsSpan(p + 1, end - p - 1))
                        : Fold(text[i - 1]) == Fold(token)));
                }
            }

            var swap = previous;
            previous = next;
            next     = swap;
            if (end > p)
            {
                p = end;
            }
        }

        return previous[text.Length];
    }

    private bool InClass(char value, ReadOnlySpan<char> pattern)
    {
        var invert = pattern[0] == '!';
        if (invert)
        {
            pattern = pattern[1..];
        }

        var matched = false;
        value = Fold(value);
        for (var i = 0; i < pattern.Length; i++)
        {
            if (i + 2 < pattern.Length && pattern[i + 1] == '-')
            {
                matched |= value >= Fold(pattern[i]) && value <= Fold(pattern[i + 2]);
                i       += 2;
            }
            else
            {
                matched |= value == Fold(pattern[i]);
            }
        }

        return matched != invert;
    }

    private static int ClassEnd(string pattern, int start)
    {
        var i = start + 1;
        if (i < pattern.Length && pattern[i] == '!')
        {
            i++;
        }

        if (i < pattern.Length && pattern[i] == ']')
        {
            i++;
        }

        return pattern.IndexOf(']', i);
    }
}
