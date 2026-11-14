using System.Globalization;

namespace Caprine.CronTimer;

internal sealed partial class CronExpressionData
{
    private static readonly string[] MonthNames   = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] WeekdayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    private void ParseFields(string expression)
    {
        var text = expression.Trim(' ', '\t');
        if (text.Length != 0 && text[0] == '@')
        {
            if ((Options & CronExpressionOptions.AllowNicknames) == 0)
            {
                throw new FormatException("Nicknames require AllowNicknames.");
            }

            text = ExpandNickname(text);
        }

        var fields   = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var seconds  = (Options & CronExpressionOptions.IncludeSeconds) != 0;
        var year     = (Options & CronExpressionOptions.IncludeYear)    != 0;
        var expected = 5 + (seconds ? 1 : 0) + (year ? 1 : 0);
        if (fields.Length != expected)
        {
            throw new FormatException($"Expected {expected} fields for the selected options.");
        }

        var index = 0;
        if (seconds)
        {
            ParseField(fields[index++], Seconds, 0, 59);
        }
        else
        {
            Seconds[0] = true;
        }

        ParseField(fields[index++], Minutes, 0, 59);
        ParseField(fields[index++], Hours, 0, 23);
        ParseDayOfMonth(fields[index++]);
        ParseField(fields[index++], Months, 1, 12, Names.Month);
        ParseDayOfWeek(fields[index++]);

        if (year && fields[index] != "*")
        {
            Years = new bool[10000];
            ParseField(fields[index], Years, 1, 9999);
        }
    }

    private void ParseDayOfMonth(string field)
    {
        var modifiers = (Options & CronExpressionOptions.AllowCalendarModifiers) != 0;
        DayOfMonthUnrestricted = StartsWithWildcard(field, modifiers);
        foreach (var part in new Parts(field.AsSpan()))
        {
            if (modifiers && part is "?")
            {
                Fill(DaysOfMonth, 1, 31);
            }
            else if (modifiers && part.Length == 1 && EqualsRuleCharacter(part[0], 'L'))
            {
                LastDayOffsets.Add(0);
            }
            else if (modifiers && part.StartsWith("L-", StringComparison.OrdinalIgnoreCase))
            {
                LastDayOffsets.Add(ReadValue(part.Slice(2), 0, 30));
            }
            else if (modifiers && (part.Equals("LW", StringComparison.OrdinalIgnoreCase) || part.Equals("WL", StringComparison.OrdinalIgnoreCase)))
            {
                LastWeekday = true;
            }
            else if (modifiers && !part.IsEmpty && EqualsRuleCharacter(part[part.Length - 1], 'W'))
            {
                NearestWeekdays.Add(ReadValue(part.Slice(0, part.Length - 1), 1, 31));
            }
            else
            {
                ParsePart(part, DaysOfMonth, 1, 31, Names.None);
            }
        }
    }

    private void ParseDayOfWeek(string field)
    {
        var modifiers = (Options & CronExpressionOptions.AllowCalendarModifiers) != 0;
        DayOfWeekUnrestricted = StartsWithWildcard(field, modifiers);
        var numeric = new bool[8];
        foreach (var part in new Parts(field.AsSpan()))
        {
            if (modifiers && part is "?")
            {
                Fill(numeric, 0, numeric.Length);
            }
            else if (modifiers && !part.IsEmpty && EqualsRuleCharacter(part[part.Length - 1], 'L'))
            {
                var day = part.Length == 1 ? 6 : ReadValue(part.Slice(0, part.Length - 1), 0, 7, Names.Weekday);
                LastWeekdays.Add(day % 7);
            }
            else if (modifiers && part.IndexOf('#') is var hash && hash >= 0)
            {
                var day     = ReadValue(part.Slice(0, hash), 0, 7, Names.Weekday) % 7;
                var ordinal = ReadValue(part.Slice(hash + 1), 1, 5);
                NthWeekdays.Add((day, ordinal));
            }
            else
            {
                ParsePart(part, numeric, 0, 7, Names.Weekday);
            }
        }

        for (var day = 0; day <= 7; day++)
        {
            DaysOfWeek[day % 7] |= numeric[day];
        }
    }

    private string ExpandNickname(string nickname)
    {
        var core = nickname.ToLowerInvariant() switch
        {
            "@yearly" or "@annually"                                               => "0 0 1 1 *",
            "@monthly"                                                             => "0 0 1 * *",
            "@weekly"                                                              => "0 0 * * 0",
            "@daily" or "@midnight"                                                => "0 0 * * *",
            "@hourly"                                                              => "0 * * * *",
            "@minutely"                                                            => "* * * * *",
            "@secondly" when (Options & CronExpressionOptions.IncludeSeconds) != 0 => "* * * * *",
            _                                                                      => throw new FormatException($"Unsupported nickname: '{nickname}'."),
        };
        if ((Options & CronExpressionOptions.IncludeSeconds) != 0)
        {
            core = (nickname.Equals("@secondly", StringComparison.OrdinalIgnoreCase) ? "* " : "0 ") + core;
        }

        if ((Options & CronExpressionOptions.IncludeYear) != 0)
        {
            core += " *";
        }

        return core;
    }

    private static bool StartsWithWildcard(string field, bool modifiers)
    {
        var comma = field.IndexOf(',');
        var first = field.AsSpan(0, comma < 0 ? field.Length : comma);
        return first is "*" || modifiers && first is "?";
    }

    private static void ParseField(string field, bool[] target, int minimum, int maximum, Names names = Names.None)
    {
        foreach (var part in new Parts(field.AsSpan()))
        {
            ParsePart(part, target, minimum, maximum, names);
        }
    }

    private static void ParsePart(ReadOnlySpan<char> part, bool[] target, int minimum, int maximum, Names names)
    {
        var slash = part.IndexOf('/');
        var step  = slash < 0 ? 1 : ReadStep(part.Slice(slash + 1));
        var body  = slash < 0 ? part : part.Slice(0, slash);
        var dash  = body.IndexOf('-');
        int start;
        int end;
        if (body is "*")
        {
            start = minimum;
            end   = maximum;
        }
        else if (dash >= 0)
        {
            start = ReadValue(body.Slice(0, dash), minimum, maximum, names);
            end   = ReadValue(body.Slice(dash + 1), minimum, maximum, names);
            if (start > end)
            {
                throw new FormatException("A range must be in ascending order.");
            }
        }
        else
        {
            if (slash >= 0)
            {
                throw new FormatException("A step must follow '*' or a complete range.");
            }

            start = ReadValue(body, minimum, maximum, names);
            end   = start;
        }

        for (var value = start;; value += step)
        {
            target[value] = true;
            if (step > end - value)
            {
                break;
            }
        }
    }

    private static int ReadValue(ReadOnlySpan<char> text, int minimum, int maximum, Names names = Names.None)
    {
        if (names       != Names.None &&
            text.Length == 3          &&
            IsAsciiLetter(text[0])    &&
            IsAsciiLetter(text[1])    &&
            IsAsciiLetter(text[2]))
        {
            var candidates = names == Names.Month ? MonthNames : WeekdayNames;
            for (var index = 0; index < candidates.Length; index++)
            {
                if (text.Equals(candidates[index], StringComparison.OrdinalIgnoreCase))
                {
                    return names == Names.Month ? index + 1 : index;
                }
            }
        }

        if (!ContainsOnlyDigits(text)           ||
            !TryReadNumber(text, out var value) ||
            value < minimum                     ||
            value > maximum)
        {
            throw new FormatException($"Expected a value from {minimum} through {maximum}, found '{text.ToString()}'.");
        }

        return value;
    }

    private static int ReadStep(ReadOnlySpan<char> text)
    {
        if (!ContainsOnlyDigits(text))
        {
            throw new FormatException("A step must be a positive integer.");
        }

        var step = 0;
        foreach (var digit in text)
        {
            // All fields are smaller than Int32.MaxValue. Larger steps select only the
            // range's first value, so saturating preserves their mathematical meaning.
            step = (int)Math.Min((long)step * 10 + digit - '0', int.MaxValue);
        }

        if (step == 0)
        {
            throw new FormatException("A step must be greater than zero.");
        }

        return step;
    }

    private static bool ContainsOnlyDigits(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        foreach (var value in text)
        {
            if (value is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualsRuleCharacter(char value, char expected)
    {
        #if NET11_0_OR_GREATER
        return value.Equals(expected, StringComparison.OrdinalIgnoreCase);
        #else
        return char.ToUpperInvariant(value) == expected;
        #endif
    }


    private static bool TryReadNumber(ReadOnlySpan<char> text, out int value)
    {
        #if CRON_GENERATOR
        return int.TryParse(text.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        #else
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        #endif
    }

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static void Fill(bool[] values, int start, int count)
    {
        for (var index = start; index < start + count; index++)
        {
            values[index] = true;
        }
    }

    private ref struct Parts
    {
        public  ReadOnlySpan<char> Current { get; private set; }
        private ReadOnlySpan<char> _remaining;
        private bool               _finished;

        internal Parts(ReadOnlySpan<char> text)
        {
            _remaining = text;
            _finished  = false;
            Current    = default;
        }

        public Parts GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_finished)
            {
                return false;
            }

            var separator = _remaining.IndexOf(',');
            if (separator < 0)
            {
                Current   = _remaining;
                _finished = true;
            }
            else
            {
                Current    = _remaining.Slice(0, separator);
                _remaining = _remaining.Slice(separator + 1);
            }

            return true;
        }
    }

    private enum Names
    {
        None,
        Month,
        Weekday,
    }
}
