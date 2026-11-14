using System.Diagnostics.CodeAnalysis;

namespace Caprine.Durations;

/// <summary>Generates a static partial duration value or date operation from a duration literal.</summary>
/// <remarks>
///     Parameterless methods and get-only properties return <see cref="TimeSpan" /> or <see cref="CalendarDuration" />.
///     Methods taking a DateTime, DateTimeOffset, DateOnly, or TimeOnly return the same type after adding the duration.
///     DateTime and DateTimeOffset anchors also support a TimeSpan result representing the difference after addition.
///     A bool return with an anchor and an out result requests a nonthrowing operation; failure sets the result to
///     default.
///     TimeOnly operations may append an out int parameter to report crossed days, after the out result for bool methods.
///     All members must be static and partial, and methods must be non-generic with no optional or ref/in parameters.
///     All containing types must be non-file-local partial classes, structs, or records.
///     DateOnly requires a whole-day fixed portion; TimeOnly rejects calendar components.
///     Calendar arithmetic applies years, months, then fixed ticks and retains the anchor's kind or numeric offset.
///     It does not apply named time-zone rules. Intermediate and final dates must be in range.
///     Expressions accept signed decimal quantities, optional whitespace, and case-insensitive English
///     units from microseconds through years, plus tick and ticks. Use m for minutes and mo for months.
///     Months and years must be integral. By default, unknown text and units are ignored and no matches produces zero.
///     <see cref="DurationParseOptions.Strict" /> rejects malformed expressions instead.
///     Recognized units must end before another letter. Fixed quantities must be exactly representable
///     in ticks. Numbers use ASCII decimal notation without grouping separators or exponents.
///     Calendar components require a CalendarDuration value or a date anchor.
/// </remarks>
/// <example>
///     <code>
/// public static partial class Durations
/// {
///     [GeneratedDuration("1w 4 days 4 hr and 100ms")]
///     public static partial TimeSpan Delay();
///
///     [GeneratedDuration("30s", DurationParseOptions.Strict)]
///     public static partial TimeSpan RequestTimeout { get; }
///
///     [GeneratedDuration("1mo")]
///     public static partial TimeSpan BillingPeriodLength(DateTimeOffset start);
///
///     [GeneratedDuration("1mo 3d")]
///     public static partial DateTimeOffset RenewalDate(DateTimeOffset start);
///
///     [GeneratedDuration("1mo 3d")]
///     public static partial bool TryRenewalDate(DateTimeOffset start, out DateTimeOffset result);
///
///     [GeneratedDuration("27h")]
///     public static partial TimeOnly AdvanceClock(TimeOnly start, out int wrappedDays);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
public sealed class GeneratedDurationAttribute : Attribute
{
    /// <summary>Gets the compile-time duration expression.</summary>
    public string Expression { get; }

    /// <summary>Gets the compile-time parsing options.</summary>
    public DurationParseOptions Options { get; }

    /// <summary>Initializes a duration declaration.</summary>
    /// <param name="expression">A string containing signed quantities and time units.</param>
    /// <param name="options">Whether to require a complete, valid duration expression.</param>
    /// <exception cref="ArgumentNullException">The expression is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options contain an undefined flag.</exception>
    public GeneratedDurationAttribute([StringSyntax("Caprine.Duration")] string expression, DurationParseOptions options = DurationParseOptions.None)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if ((options & ~DurationParseOptions.Strict) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, "The parsing options contain an undefined flag.");
        }

        Expression = expression;
        Options    = options;
    }
}
