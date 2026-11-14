namespace Caprine.Durations;

/// <summary>A calendar period with separate years, months, and an exact fixed interval.</summary>
/// <remarks>
///     Application order is years, months, then fixed ticks. Days are 24 hours and weeks are seven days.
///     Calendar addition follows BCL month-end clamping and does not apply named time-zone rules.
///     Negating a period does not necessarily reverse its application.
/// </remarks>
public readonly partial record struct CalendarDuration
{
    /// <summary>Gets the number of calendar years.</summary>
    public int Years { get; }

    /// <summary>Gets the number of calendar months, without normalizing them into years.</summary>
    public int Months { get; }

    /// <summary>Gets the fixed portion in 100-nanosecond ticks.</summary>
    public long FixedTicks { get; }

    /// <summary>Gets the fixed portion as a time interval.</summary>
    public TimeSpan FixedDuration => new(FixedTicks);

    /// <summary>Gets whether the value requires calendar arithmetic.</summary>
    public bool HasCalendarComponents => Years != 0 || Months != 0;

    /// <summary>Gets the zero duration.</summary>
    public static CalendarDuration Zero => default;

    /// <summary>Initializes a period without normalizing its calendar components.</summary>
    /// <param name="years">Calendar years, from -10,000 through 10,000.</param>
    /// <param name="months">Calendar months, from -120,000 through 120,000.</param>
    /// <param name="fixedTicks">The exact fixed interval in ticks.</param>
    /// <exception cref="ArgumentOutOfRangeException">A calendar component exceeds BCL limits.</exception>
    public CalendarDuration(int years, int months, long fixedTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(years, -10_000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(years, 10_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(months, -120_000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(months, 120_000);

        Years      = years;
        Months     = months;
        FixedTicks = fixedTicks;
    }

    /// <summary>Adds this period to a date and time, preserving its kind.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <returns>The resulting date and time.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An intermediate or final date is out of range.</exception>
    public DateTime AddTo(DateTime start)
    {
        if (Years != 0)
        {
            start = start.AddYears(Years);
        }

        if (Months != 0)
        {
            start = start.AddMonths(Months);
        }

        return start.AddTicks(FixedTicks);
    }

    /// <summary>Adds this period to a date and time, preserving its numeric UTC offset.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <returns>The resulting date and time with the original offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An intermediate or final date is out of range.</exception>
    public DateTimeOffset AddTo(DateTimeOffset start)
    {
        if (Years != 0)
        {
            start = start.AddYears(Years);
        }

        if (Months != 0)
        {
            start = start.AddMonths(Months);
        }

        return start.AddTicks(FixedTicks);
    }

    /// <summary>Adds this period to a date, requiring a whole-day fixed portion.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <returns>The resulting date.</returns>
    /// <exception cref="InvalidOperationException">The fixed portion contains a sub-day remainder.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An intermediate or final date is out of range.</exception>
    public DateOnly AddTo(DateOnly start)
    {
        if (FixedTicks % TimeSpan.TicksPerDay != 0)
        {
            throw new InvalidOperationException("A DateOnly requires a whole number of fixed days.");
        }

        if (Years != 0)
        {
            start = start.AddYears(Years);
        }

        if (Months != 0)
        {
            start = start.AddMonths(Months);
        }

        return start.AddDays((int)(FixedTicks / TimeSpan.TicksPerDay));
    }

    /// <summary>Adds a fixed duration to a time of day, wrapping across midnight.</summary>
    /// <param name="start">The starting time.</param>
    /// <returns>The resulting time of day.</returns>
    /// <exception cref="InvalidOperationException">Calendar components require a date.</exception>
    public TimeOnly AddTo(TimeOnly start) => start.Add(ToTimeSpan());

    /// <summary>Adds a fixed duration to a time of day and reports crossed days.</summary>
    /// <param name="start">The starting time.</param>
    /// <param name="wrappedDays">The signed number of crossed days.</param>
    /// <returns>The resulting time of day.</returns>
    /// <exception cref="InvalidOperationException">Calendar components require a date.</exception>
    public TimeOnly AddTo(TimeOnly start, out int wrappedDays) => start.Add(ToTimeSpan(), out wrappedDays);

    /// <summary>Converts a fixed-only value into a time interval.</summary>
    /// <returns>The exact fixed interval.</returns>
    /// <exception cref="InvalidOperationException">A calendar anchor is required.</exception>
    public TimeSpan ToTimeSpan()
    {
        if (HasCalendarComponents)
        {
            throw new InvalidOperationException("Months and years require a starting date.");
        }

        return FixedDuration;
    }

    /// <summary>Gets the clock difference after applying this period to an anchor.</summary>
    /// <remarks>Use UTC anchors for elapsed-time arithmetic; local date arithmetic ignores DST rules.</remarks>
    /// <param name="start">The starting date and time.</param>
    /// <returns>The resulting clock difference.</returns>
    public TimeSpan ToTimeSpan(DateTime start) => AddTo(start) - start;

    /// <summary>Gets the elapsed interval after applying this period with the anchor's fixed offset.</summary>
    /// <param name="start">The starting date and time.</param>
    /// <returns>The interval between the two instants.</returns>
    public TimeSpan ToTimeSpan(DateTimeOffset start) => AddTo(start) - start;

    /// <summary>Attempts to obtain an interval without a calendar anchor.</summary>
    /// <param name="duration">The fixed interval, or zero if calendar components are present.</param>
    /// <returns>Whether the value has no calendar components.</returns>
    public bool TryGetTimeSpan(out TimeSpan duration)
    {
        duration = HasCalendarComponents ? TimeSpan.Zero : FixedDuration;

        return !HasCalendarComponents;
    }
}
