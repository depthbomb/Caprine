using System.Globalization;
using System.Numerics;

namespace Caprine.Durations.Generators;

internal static partial class DurationParser
{
    internal const int MaximumExpressionLength = 32_768;

    private const int MaximumNumberLength = 1_024;

    private static readonly int[] DecimalPowers = [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];

    internal static bool TryParse(string?              expression,
                                  CancellationToken    ct,
                                  out ParsedDuration   duration,
                                  out DurationProblem  problem,
                                  DurationParseOptions options = DurationParseOptions.None)
    {
        duration = default;
        problem  = default;

        if ((options & ~DurationParseOptions.Strict) != 0)
        {
            problem = new DurationProblem("CAPD009", "The parsing options contain an undefined flag.");
            return false;
        }

        if (expression is null)
        {
            problem = new DurationProblem("CAPD002", "The duration expression cannot be null.");
            return false;
        }

        if (expression.Length > MaximumExpressionLength)
        {
            problem = new DurationProblem("CAPD006", "A duration expression cannot exceed 32,768 characters.");
            return false;
        }

        var years    = BigInteger.Zero;
        var months   = BigInteger.Zero;
        var ticks    = BigInteger.Zero;
        var position = 0;
        var matches  = 0;
        var strict   = (options & DurationParseOptions.Strict) != 0;

        while (position < expression.Length)
        {
            ct.ThrowIfCancellationRequested();

            if (strict)
            {
                if (!TrySkipSeparators(expression, ref position, matches != 0, out problem))
                {
                    return false;
                }

                if (position == expression.Length)
                {
                    break;
                }
            }

            if (!IsNumberStart(expression[position]))
            {
                if (strict)
                {
                    return InvalidSyntax(position, "Expected a duration quantity", out problem);
                }

                position++;
                continue;
            }

            var numberStart = position;
            ReadNumberCandidate(expression, ref position);

            var numberLength = position - numberStart;

            while (position < expression.Length && char.IsWhiteSpace(expression[position]))
            {
                position++;
            }

            var unitStart = position;
            while (position < expression.Length && IsUnitLetter(expression, position))
            {
                position += char.IsSurrogatePair(expression, position) ? 2 : 1;
            }

            var unit    = ReadUnit(expression, unitStart, position - unitStart);
            var factor  = GetTickFactor(unit);
            var isYear  = unit == DurationUnit.Year;
            var isMonth = unit == DurationUnit.Month;
            if (factor == 0 && !isYear && !isMonth)
            {
                if (strict)
                {
                    return InvalidSyntax(unitStart, "Expected a supported duration unit", out problem);
                }

                continue;
            }

            if (numberLength > MaximumNumberLength)
            {
                problem = new DurationProblem("CAPD006", "A recognized duration quantity cannot exceed 1,024 characters.");
                return false;
            }

            if (!TryReadQuantity(expression, numberStart, numberLength, out var coefficient, out var scale))
            {
                if (strict)
                {
                    return InvalidSyntax(numberStart, "Expected an ASCII decimal quantity without grouping or exponents", out problem);
                }

                continue;
            }

            var value = coefficient * (isYear || isMonth ? 1 : factor);
            if (scale != 0)
            {
                var divisor = BigInteger.Pow(10, scale);
                value = BigInteger.DivRem(value, divisor, out var remainder);
                if (!remainder.IsZero)
                {
                    problem = isYear || isMonth
                        ? new DurationProblem("CAPD003", "Months and years must be whole numbers.")
                        : new DurationProblem("CAPD004", "Each fixed quantity must represent an exact number of .NET ticks (0.1 microsecond).");
                    return false;
                }
            }

            if (isYear)
            {
                years += value;
            }
            else if (isMonth)
            {
                months += value;
            }
            else
            {
                ticks += value;
            }

            matches++;
        }

        if (strict && matches == 0)
        {
            return InvalidSyntax(0, "Expected at least one duration quantity; use 0s for zero", out problem);
        }

        if (years  < -10_000       ||
            years  > 10_000        ||
            months < -120_000      ||
            months > 120_000       ||
            ticks  < long.MinValue ||
            ticks  > long.MaxValue)
        {
            problem = new DurationProblem("CAPD005", "The accumulated duration exceeds the supported calendar or TimeSpan range.");
            return false;
        }

        duration = new ParsedDuration((int)years, (int)months, (long)ticks);

        return true;
    }

    private static bool TrySkipSeparators(string expression, ref int position, bool hasQuantity, out DurationProblem problem)
    {
        problem = default;
        SkipWhitespace(expression, ref position);
        if (!hasQuantity || position == expression.Length)
        {
            return true;
        }

        var hasConnector = false;
        if (expression[position] == ',')
        {
            hasConnector = true;
            position++;
            SkipWhitespace(expression, ref position);
        }

        var isAnd = position + 3                                                                          <= expression.Length &&
                    string.Compare(expression, position, "and", 0, 3, StringComparison.OrdinalIgnoreCase) == 0                 &&
                    (position + 3 == expression.Length || char.IsWhiteSpace(expression[position + 3]));
        if (isAnd)
        {
            hasConnector =  true;
            position     += 3;
            SkipWhitespace(expression, ref position);
        }

        if (hasConnector && position == expression.Length)
        {
            return InvalidSyntax(position, "Expected a duration quantity after the separator", out problem);
        }

        return true;
    }

    private static void SkipWhitespace(string expression, ref int position)
    {
        while (position < expression.Length && char.IsWhiteSpace(expression[position]))
        {
            position++;
        }
    }

    private static bool InvalidSyntax(int position, string message, out DurationProblem problem)
    {
        problem = new DurationProblem("CAPD008", message + " at character " + (position + 1).ToString(CultureInfo.InvariantCulture) + ".");
        return false;
    }

    private static void ReadNumberCandidate(string expression, ref int position)
    {
        while (position < expression.Length && expression[position] is '+' or '-')
        {
            position++;
        }

        while (position < expression.Length)
        {
            var current = expression[position];
            if (char.IsDigit(current) || current == '.')
            {
                position++;
                continue;
            }

            var followedByDigit = position + 1 < expression.Length && char.IsDigit(expression[position + 1]);
            if (current is ',' or '_' && followedByDigit)
            {
                position++;
                continue;
            }

            var isExponent = current is 'e' or 'E' && position + 1 < expression.Length && (char.IsDigit(expression[position + 1]) || expression[position + 1] is '+' or '-');
            if (isExponent)
            {
                position++;
                while (position < expression.Length && expression[position] is '+' or '-')
                {
                    position++;
                }

                continue;
            }

            break;
        }
    }

    private static bool TryReadQuantity(string text, int start, int length, out BigInteger coefficient, out int scale)
    {
        coefficient = BigInteger.Zero;
        scale       = 0;

        var end      = start + length;
        var negative = text[start] == '-';

        if (text[start] is '+' or '-')
        {
            start++;
        }

        var decimalPoint = false;
        var digitCount   = 0;
        var chunk        = 0;
        var chunkDigits  = 0;

        for (var index = start; index < end; index++)
        {
            var current = text[index];
            if (current is >= '0' and <= '9')
            {
                chunk = chunk * 10 + (current - '0');
                chunkDigits++;
                digitCount++;

                // Nine digits fit in an int. Accumulate larger values a chunk at a time to limit BigInteger allocations.
                if (chunkDigits == 9)
                {
                    coefficient = coefficient * 1_000_000_000 + chunk;
                    chunk       = 0;
                    chunkDigits = 0;
                }

                if (decimalPoint)
                {
                    scale++;
                }
            }
            else if (current == '.' && !decimalPoint)
            {
                decimalPoint = true;
            }
            else
            {
                return false;
            }
        }

        if (chunkDigits != 0)
        {
            coefficient = coefficient * DecimalPowers[chunkDigits] + chunk;
        }

        if (negative)
        {
            coefficient = -coefficient;
        }

        return digitCount != 0 && (!decimalPoint || scale != 0);
    }

    private static bool IsNumberStart(char value) => char.IsDigit(value) || value is '+' or '-' or '.';

    private static bool IsUnitLetter(string text, int position)
    {
        var current = text[position];
        if (current <= 127)
        {
            return current is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(text, position);
        return category is UnicodeCategory.UppercaseLetter or
                           UnicodeCategory.LowercaseLetter or
                           UnicodeCategory.TitlecaseLetter or
                           UnicodeCategory.ModifierLetter or
                           UnicodeCategory.OtherLetter or
                           UnicodeCategory.NonSpacingMark or
                           UnicodeCategory.SpacingCombiningMark or
                           UnicodeCategory.EnclosingMark;
    }
}
