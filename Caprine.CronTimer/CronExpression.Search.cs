namespace Caprine.CronTimer;

public sealed partial class CronExpression
{
    private DateTimeOffset? FindOccurrence(DateTimeOffset bound, TimeZoneInfo zone, bool forward, bool inclusive)
    {
        var actualOffset = zone.GetUtcOffset(bound);
        var localTicks   = bound.UtcTicks + actualOffset.Ticks;
        if (localTicks < 0 && !forward || localTicks > DateTime.MaxValue.Ticks && forward)
        {
            return null;
        }

        var clipped = localTicks < 0 || localTicks > DateTime.MaxValue.Ticks;
        var local   = new DateTime(Math.Clamp(localTicks, 0, DateTime.MaxValue.Ticks));
        var mapping = ZoneMap.For(zone);
        var result  = FindMappedOccurrence(local, bound, mapping, forward, inclusive || clipped, inclusive);
        if (clipped || !mapping.GetOffsetBounds(local, out var earlierOffset, out var laterOffset))
        {
            return result;
        }

        // Wall-clock order and instant order differ inside a repeated interval. Search the
        // other traversal of that interval as well, including wall times behind the bound.
        if (forward && actualOffset == laterOffset || !forward && actualOffset == earlierOffset)
        {
            return result;
        }

        var edge  = FindIntervalEdge(local, mapping, true, !forward);
        var end   = FindIntervalEdge(local, mapping, true, forward);
        var other = FindMappedOccurrence(edge, bound, mapping, forward, true, inclusive, end);
        if (other is not null && (result is null || (forward ? other < result : other > result)))
        {
            result = other;
        }

        return result;
    }

    private DateTimeOffset? FindMappedOccurrence(DateTime       start,
                                                 DateTimeOffset bound,
                                                 ZoneMap        mapping,
                                                 bool           forward,
                                                 bool           includeStart,
                                                 bool           includeBound,
                                                 DateTime?      limit = null)
    {
        var candidate = FindLocalOccurrence(start, forward, includeStart);
        while (candidate is { } local)
        {
            if (limit is { } end && (forward ? local > end : local < end))
            {
                return null;
            }

            DateTimeOffset? best   = null;
            var             mapped = false;
            foreach (var offset in mapping.Offsets)
            {
                if (!mapping.TryMap(local, offset, out var instant))
                {
                    continue;
                }

                mapped = true;
                var comparison = instant.CompareTo(bound);
                if ((forward ? comparison > 0 : comparison < 0) || includeBound && comparison == 0)
                {
                    if (best is null || (forward ? instant < best : instant > best))
                    {
                        best = instant;
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }

            var nextStart = mapped ? local : FindIntervalEdge(local, mapping, false, forward);
            candidate = FindLocalOccurrence(nextStart, forward, false);
        }

        return null;
    }

    private DateTime? FindLocalOccurrence(DateTime start, bool forward, bool inclusive)
    {
        var rounded = RoundToSecond(start.Ticks, forward, inclusive);
        if (rounded is null)
        {
            return null;
        }

        var date = rounded.Value;
        search: while (true)
        {
            // Gregorian dates and weekdays repeat every 400 years. Without a year
            // restriction, a full cycle without a match means there can never be one.
            if (_years is null && Math.Abs(date.Year - start.Year) > 400)
            {
                return null;
            }

            if (_years is not null && !_years[date.Year])
            {
                var year = FindValue(_years, date.Year, forward);
                if (year < 1)
                {
                    return null;
                }

                date = MonthEdge(year, forward ? 1 : 12, forward);
            }

            if (!_months[date.Month])
            {
                var month = FindValue(_months, date.Month, forward);
                if (month > 0)
                {
                    date = MonthEdge(date.Year, month, forward);
                }
                else
                {
                    var year = date.Year + (forward ? 1 : -1);
                    if (year is < 1 or > 9999)
                    {
                        return null;
                    }

                    date = MonthEdge(year, forward ? 1 : 12, forward);
                }

                continue;
            }

            var currentMonth = date.Month;
            while (!MatchesDay(date.Year, date.Month, date.Day))
            {
                var nextDay = MovePastUnit(date, TimeSpan.TicksPerDay, forward);
                if (nextDay is null)
                {
                    return null;
                }

                date = nextDay.Value;
                if (date.Month != currentMonth)
                {
                    continue search;
                }
            }

            var hour = FindValue(_hours, date.Hour, forward);
            if (hour < 0)
            {
                var nextDay = MovePastUnit(date, TimeSpan.TicksPerDay, forward);
                if (nextDay is null)
                {
                    return null;
                }

                date = nextDay.Value;
                continue;
            }

            if (hour != date.Hour)
            {
                date = new DateTime(date.Year, date.Month, date.Day, hour, forward ? 0 : 59, forward ? 0 : 59);
            }

            var minute = FindValue(_minutes, date.Minute, forward);
            if (minute < 0)
            {
                var nextHour = MovePastUnit(date, TimeSpan.TicksPerHour, forward);
                if (nextHour is null)
                {
                    return null;
                }

                date = nextHour.Value;
                continue;
            }

            if (minute != date.Minute)
            {
                date = new DateTime(date.Year, date.Month, date.Day, date.Hour, minute, forward ? 0 : 59);
            }

            var second = FindValue(_seconds, date.Second, forward);
            if (second < 0)
            {
                var nextMinute = MovePastUnit(date, TimeSpan.TicksPerMinute, forward);
                if (nextMinute is null)
                {
                    return null;
                }

                date = nextMinute.Value;
                continue;
            }

            return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, second);
        }
    }

    private bool MatchesDay(int year, int month, int day)
    {
        var daysInMonth     = DateTime.DaysInMonth(year, month);
        var weekday         = (int)new DateTime(year, month, day).DayOfWeek;
        var matchesMonthDay = _daysOfMonth[day];
        foreach (var offset in _lastDayOffsets)
        {
            matchesMonthDay |= day == daysInMonth - offset;
        }

        if (_lastWeekday)
        {
            matchesMonthDay |= day == NearestWeekday(year, month, daysInMonth);
        }

        foreach (var requested in _nearestWeekdays)
        {
            if (requested <= daysInMonth)
            {
                matchesMonthDay |= day == NearestWeekday(year, month, requested);
            }
        }

        var matchesWeekday = _daysOfWeek[weekday];
        foreach (var requested in _lastWeekdays)
        {
            matchesWeekday |= weekday == requested && day + 7 > daysInMonth;
        }

        foreach (var (requested, ordinal) in _nthWeekdays)
        {
            matchesWeekday |= weekday == requested && (day - 1) / 7 + 1 == ordinal;
        }

        return !_dayOfMonthUnrestricted && !_dayOfWeekUnrestricted
            ? matchesMonthDay || matchesWeekday
            : matchesMonthDay && matchesWeekday;
    }

    private static int NearestWeekday(int year, int month, int day) => new DateTime(year, month, day).DayOfWeek switch
    {
        DayOfWeek.Saturday => day == 1 ? 3 : day                             - 1,
        DayOfWeek.Sunday   => day == DateTime.DaysInMonth(year, month) ? day - 2 : day + 1,
        _                  => day,
    };

    private static int FindValue(bool[] values, int start, bool forward)
    {
        if (forward)
        {
            var index = values.AsSpan(start).IndexOf(true);
            return index < 0 ? -1 : start + index;
        }

        return values.AsSpan(0, start + 1).LastIndexOf(true);
    }

    private static DateTime MonthEdge(int year, int month, bool forward) => forward
        ? new DateTime(year, month, 1)
        : new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);

    private static DateTime? MovePastUnit(DateTime date, long unit, bool forward)
    {
        var floor = date.Ticks / unit * unit;
        return RoundToSecond(forward ? floor + unit : floor - 1, forward, true);
    }

    private static DateTime? RoundToSecond(long ticks, bool forward, bool inclusive)
    {
        if (!inclusive)
        {
            ticks += forward ? 1 : -1;
        }

        if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var remainder = ticks % TimeSpan.TicksPerSecond;

        ticks -= remainder;

        if (forward && remainder != 0)
        {
            ticks += TimeSpan.TicksPerSecond;
        }

        return ticks > DateTime.MaxValue.Ticks ? null : new DateTime(ticks);
    }

    private static DateTime FindIntervalEdge(DateTime inside, ZoneMap mapping, bool repeated, bool forward)
    {
        // Return the last tick still inside the interval in the requested direction.
        // This handles transitions that aren't an hour long and boundaries with milliseconds.
        var inner = inside.Ticks;
        var outer = inner;
        while (true)
        {
            outer = Math.Clamp(outer + (forward ? TimeSpan.TicksPerHour : -TimeSpan.TicksPerHour), 0, DateTime.MaxValue.Ticks);
            var contains = repeated ? mapping.IsRepeated(new DateTime(outer)) : mapping.IsMissing(new DateTime(outer));
            if (!contains)
            {
                break;
            }

            inner = outer;
            if (outer == 0 || outer == DateTime.MaxValue.Ticks)
            {
                return new DateTime(outer);
            }
        }

        while (Math.Abs(outer - inner) > 1)
        {
            var middle = inner + (outer - inner) / 2;
            if (repeated ? mapping.IsRepeated(new DateTime(middle)) : mapping.IsMissing(new DateTime(middle)))
            {
                inner = middle;
            }
            else
            {
                outer = middle;
            }
        }

        return new DateTime(inner);
    }
}
