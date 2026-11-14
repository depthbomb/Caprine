namespace Caprine.Durations;

public readonly partial record struct CalendarDuration
{
    /// <summary>Attempts to add this period, preserving the anchor's kind.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <param name="result">The resulting date and time, or default on failure.</param>
    /// <returns>False if an intermediate or final date is out of range.</returns>
    public bool TryAddTo(DateTime start, out DateTime result)
    {
        result = default;
        var calendar = start;
        if (HasCalendarComponents && !TryAddCalendar(start, 0, DateTime.MaxValue.Ticks, out calendar))
        {
            return false;
        }

        if (FixedTicks < -calendar.Ticks || FixedTicks > DateTime.MaxValue.Ticks - calendar.Ticks)
        {
            return false;
        }

        result = calendar.AddTicks(FixedTicks);
        return true;
    }

    /// <summary>Attempts to add this period, preserving the anchor's numeric UTC offset.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <param name="result">The resulting date and time, or default on failure.</param>
    /// <returns>False if an intermediate or final date is out of range.</returns>
    public bool TryAddTo(DateTimeOffset start, out DateTimeOffset result)
    {
        result = default;
        var offset       = start.Offset;
        var minimumTicks = Math.Max(0, offset.Ticks);
        var maximumTicks = Math.Min(DateTime.MaxValue.Ticks, DateTime.MaxValue.Ticks + offset.Ticks);
        var calendar     = start.DateTime;
        if (HasCalendarComponents && !TryAddCalendar(calendar, minimumTicks, maximumTicks, out calendar))
        {
            return false;
        }

        if (FixedTicks < minimumTicks - calendar.Ticks || FixedTicks > maximumTicks - calendar.Ticks)
        {
            return false;
        }

        result = new DateTimeOffset(calendar.AddTicks(FixedTicks), offset);
        return true;
    }

    /// <summary>Attempts to add this period to a date.</summary>
    /// <param name="start">The calendar anchor.</param>
    /// <param name="result">The resulting date, or default on failure.</param>
    /// <returns>False if the fixed portion contains a sub-day remainder or a date is out of range.</returns>
    public bool TryAddTo(DateOnly start, out DateOnly result)
    {
        result = default;

        if (FixedTicks % TimeSpan.TicksPerDay != 0)
        {
            return false;
        }

        if (HasCalendarComponents)
        {
            if (!TryAddCalendar(start.ToDateTime(TimeOnly.MinValue), 0, DateTime.MaxValue.Ticks, out var calendar))
            {
                return false;
            }

            start = DateOnly.FromDateTime(calendar);
        }

        var dayNumber = start.DayNumber + FixedTicks / TimeSpan.TicksPerDay;
        if (dayNumber < 0 || dayNumber > DateOnly.MaxValue.DayNumber)
        {
            return false;
        }

        result = DateOnly.FromDayNumber((int)dayNumber);
        return true;
    }

    /// <summary>Attempts to add a fixed-only period to a time of day.</summary>
    /// <param name="start">The starting time.</param>
    /// <param name="result">The resulting time, or default on failure.</param>
    /// <returns>False if calendar components are present.</returns>
    public bool TryAddTo(TimeOnly start, out TimeOnly result) => TryAddTo(start, out result, out _);

    /// <summary>Attempts to add a fixed-only period and reports crossed days.</summary>
    /// <param name="start">The starting time.</param>
    /// <param name="result">The resulting time, or default on failure.</param>
    /// <param name="wrappedDays">The signed number of crossed days, or zero on failure.</param>
    /// <returns>False if calendar components are present.</returns>
    public bool TryAddTo(TimeOnly start, out TimeOnly result, out int wrappedDays)
    {
        result      = default;
        wrappedDays = 0;

        if (HasCalendarComponents)
        {
            return false;
        }

        result = AddTo(start, out wrappedDays);
        return true;
    }

    /// <summary>Attempts to obtain the clock difference after applying this period to an anchor.</summary>
    /// <remarks>Use UTC anchors for elapsed-time arithmetic; local date arithmetic ignores DST rules.</remarks>
    /// <param name="start">The starting date and time.</param>
    /// <param name="duration">The resulting interval, or zero on failure.</param>
    /// <returns>False if an intermediate or final date is out of range.</returns>
    public bool TryGetTimeSpan(DateTime start, out TimeSpan duration)
    {
        var success = TryAddTo(start, out var result);
        duration = success ? result - start : TimeSpan.Zero;

        return success;
    }

    /// <summary>Attempts to obtain the elapsed interval using the anchor's fixed offset.</summary>
    /// <param name="start">The starting date and time.</param>
    /// <param name="duration">The resulting interval, or zero on failure.</param>
    /// <returns>False if an intermediate or final date is out of range.</returns>
    public bool TryGetTimeSpan(DateTimeOffset start, out TimeSpan duration)
    {
        var success = TryAddTo(start, out var result);
        duration = success ? result - start : TimeSpan.Zero;

        return success;
    }

    private bool TryAddCalendar(DateTime start, long minimumTicks, long maximumTicks, out DateTime result)
    {
        result = start;

        // Check each calendar stage before calling the BCL, including years that later months would cancel.
        var year = start.Year + Years;
        if (year is < 1 or > 9999)
        {
            return false;
        }

        // The month is only needed when the adjustment could cross either end of the supported years.
        if (Months < -(year - 1) * 12 || Months > (9999 - year) * 12)
        {
            var monthIndex = (year - 1) * 12 + start.Month - 1 + Months;
            if (monthIndex is < 0 or >= 9999 * 12)
            {
                return false;
            }
        }

        if (Years != 0)
        {
            result = result.AddYears(Years);
            if (result.Ticks < minimumTicks || result.Ticks > maximumTicks)
            {
                return false;
            }
        }

        if (Months != 0)
        {
            result = result.AddMonths(Months);
            if (result.Ticks < minimumTicks || result.Ticks > maximumTicks)
            {
                return false;
            }
        }

        return true;
    }
}
