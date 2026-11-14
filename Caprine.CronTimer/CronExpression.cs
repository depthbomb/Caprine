using System.Diagnostics.CodeAnalysis;

namespace Caprine.CronTimer;

/// <summary>An immutable cron expression using OCPS 1.0 by default.</summary>
/// <remarks>
///     The default syntax has minute, hour, day-of-month, month, and day-of-week fields.
///     Extension flags opt into additional syntax. Searches use the supplied time zone, skip
///     nonexistent local times, and treat the two instants in a repeated local time as separate occurrences.
/// </remarks>
public sealed partial class CronExpression
{
    /// <summary>Gets the original expression text.</summary>
    public string Expression { get; }

    /// <summary>Gets the explicitly enabled syntax extensions.</summary>
    public CronExpressionOptions Options { get; }

    private readonly bool                         _dayOfMonthUnrestricted;
    private readonly bool                         _dayOfWeekUnrestricted;
    private readonly bool                         _lastWeekday;
    private readonly bool[]?                      _years;
    private readonly bool[]                       _seconds;
    private readonly bool[]                       _minutes;
    private readonly bool[]                       _hours;
    private readonly bool[]                       _months;
    private readonly bool[]                       _daysOfMonth;
    private readonly bool[]                       _daysOfWeek;
    private readonly List<int>                    _lastDayOffsets;
    private readonly List<int>                    _nearestWeekdays;
    private readonly List<int>                    _lastWeekdays;
    private readonly List<(int Day, int Ordinal)> _nthWeekdays;

    /// <summary>Parses a cron expression.</summary>
    /// <param name="expression">
    ///     An expression separated by ASCII spaces or tabs. Leading and trailing spaces and tabs are
    ///     ignored.
    /// </param>
    /// <param name="options">Explicit extensions; the default accepts only OCPS 1.0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options" /> contains an undefined flag.</exception>
    /// <exception cref="FormatException">The syntax, field count, or a field value is invalid.</exception>
    public CronExpression(string expression, CronExpressionOptions options = CronExpressionOptions.None)
        : this(new CronExpressionData(expression, options)) { }

    private CronExpression(CronExpressionData data)
    {
        Expression              = data.Expression;
        Options                 = data.Options;
        _dayOfMonthUnrestricted = data.DayOfMonthUnrestricted;
        _dayOfWeekUnrestricted  = data.DayOfWeekUnrestricted;
        _lastWeekday            = data.LastWeekday;
        _years                  = data.Years;
        _seconds                = data.Seconds;
        _minutes                = data.Minutes;
        _hours                  = data.Hours;
        _months                 = data.Months;
        _daysOfMonth            = data.DaysOfMonth;
        _daysOfWeek             = data.DaysOfWeek;
        _lastDayOffsets         = data.LastDayOffsets;
        _nearestWeekdays        = data.NearestWeekdays;
        _lastWeekdays           = data.LastWeekdays;
        _nthWeekdays            = data.NthWeekdays;
    }

    /// <summary>Returns the original expression text.</summary>
    /// <returns>The value of <see cref="Expression" />.</returns>
    public override string ToString() => Expression;

    /// <summary>Finds the first occurrence strictly after an instant.</summary>
    /// <param name="baseTime">The exclusive lower bound, compared as an instant.</param>
    /// <param name="timeZone">The evaluation time zone; null uses <see cref="TimeZoneInfo.Local" />.</param>
    /// <returns>The next occurrence with the time zone's UTC offset, or null when none is representable.</returns>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset baseTime, TimeZoneInfo? timeZone = null)
        => FindOccurrence(baseTime, timeZone ?? TimeZoneInfo.Local, true, false);

    /// <summary>Finds the last occurrence strictly before an instant.</summary>
    /// <param name="baseTime">The exclusive upper bound, compared as an instant.</param>
    /// <param name="timeZone">The evaluation time zone; null uses <see cref="TimeZoneInfo.Local" />.</param>
    /// <returns>The previous occurrence with the time zone's UTC offset, or null when none is representable.</returns>
    public DateTimeOffset? GetLastOccurrence(DateTimeOffset baseTime, TimeZoneInfo? timeZone = null)
        => FindOccurrence(baseTime, timeZone ?? TimeZoneInfo.Local, false, false);

    /// <summary>Checks whether an exact instant is an occurrence of this schedule.</summary>
    /// <param name="instant">The instant to check. Fractions of a second never match.</param>
    /// <param name="timeZone">The evaluation time zone; null uses <see cref="TimeZoneInfo.Local" />.</param>
    /// <returns>True when all fields match under the day-field combination rules.</returns>
    public bool IsMatch(DateTimeOffset instant, TimeZoneInfo? timeZone = null)
    {
        if (instant.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            return false;
        }

        var zone  = timeZone ?? TimeZoneInfo.Local;
        var ticks = instant.UtcTicks + zone.GetUtcOffset(instant).Ticks;
        if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        var local = new DateTime(ticks);
        return (_years is null || _years[local.Year])         &&
               _months[local.Month]                           &&
               MatchesDay(local.Year, local.Month, local.Day) &&
               _hours[local.Hour]                             &&
               _minutes[local.Minute]                         &&
               _seconds[local.Second];
    }

    /// <summary>Attempts to parse an expression without throwing for malformed input.</summary>
    /// <param name="expression">Expression text; null is treated as invalid input.</param>
    /// <param name="result">The parsed expression on success; otherwise null.</param>
    /// <param name="options">Explicit syntax extensions.</param>
    /// <returns>True when parsing succeeds.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options" /> contains an undefined flag.</exception>
    public static bool TryParse(string?                                 expression,
                                [NotNullWhen(true)] out CronExpression? result,
                                CronExpressionOptions                   options = CronExpressionOptions.None)
    {
        ValidateOptions(options);

        result = null;
        if (expression is null)
        {
            return false;
        }

        try
        {
            result = new CronExpression(expression, options);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal DateTimeOffset? GetOccurrenceAtOrBefore(DateTimeOffset instant, TimeZoneInfo timeZone)
        => FindOccurrence(instant, timeZone, false, true);

    private static void ValidateOptions(CronExpressionOptions options)
    {
        CronExpressionData.ValidateOptions(options);
    }
}
