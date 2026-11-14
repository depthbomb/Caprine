namespace Caprine.Durations.Generators;

internal static partial class DurationParser
{
    private static DurationUnit ReadUnit(string text, int start, int length) => length switch
    {
        1 => char.ToLowerInvariant(text[start]) switch
        {
            's' => DurationUnit.Second,
            'm' => DurationUnit.Minute,
            'h' => DurationUnit.Hour,
            'd' => DurationUnit.Day,
            'w' => DurationUnit.Week,
            'y' => DurationUnit.Year,
            _   => DurationUnit.None,
        },
        2 when UnitEquals(text, start, "us") || UnitEquals(text, start, "µs") || UnitEquals(text, start, "μs") => DurationUnit.Microsecond,
        2 when UnitEquals(text, start, "ms")                                                                   => DurationUnit.Millisecond,
        2 when UnitEquals(text, start, "hr")                                                                   => DurationUnit.Hour,
        2 when UnitEquals(text, start, "wk")                                                                   => DurationUnit.Week,
        2 when UnitEquals(text, start, "mo")                                                                   => DurationUnit.Month,
        2 when UnitEquals(text, start, "yr")                                                                   => DurationUnit.Year,
        3 when UnitEquals(text, start, "sec")                                                                  => DurationUnit.Second,
        3 when UnitEquals(text, start, "min")                                                                  => DurationUnit.Minute,
        3 when UnitEquals(text, start, "hrs")                                                                  => DurationUnit.Hour,
        3 when UnitEquals(text, start, "day")                                                                  => DurationUnit.Day,
        3 when UnitEquals(text, start, "wks")                                                                  => DurationUnit.Week,
        3 when UnitEquals(text, start, "mon")                                                                  => DurationUnit.Month,
        3 when UnitEquals(text, start, "yrs")                                                                  => DurationUnit.Year,
        4 when UnitEquals(text, start, "tick")                                                                 => DurationUnit.Tick,
        4 when UnitEquals(text, start, "msec")                                                                 => DurationUnit.Millisecond,
        4 when UnitEquals(text, start, "secs")                                                                 => DurationUnit.Second,
        4 when UnitEquals(text, start, "mins")                                                                 => DurationUnit.Minute,
        4 when UnitEquals(text, start, "hour")                                                                 => DurationUnit.Hour,
        4 when UnitEquals(text, start, "days")                                                                 => DurationUnit.Day,
        4 when UnitEquals(text, start, "week")                                                                 => DurationUnit.Week,
        4 when UnitEquals(text, start, "mons")                                                                 => DurationUnit.Month,
        4 when UnitEquals(text, start, "year")                                                                 => DurationUnit.Year,
        5 when UnitEquals(text, start, "ticks")                                                                => DurationUnit.Tick,
        5 when UnitEquals(text, start, "msecs")                                                                => DurationUnit.Millisecond,
        5 when UnitEquals(text, start, "hours")                                                                => DurationUnit.Hour,
        5 when UnitEquals(text, start, "weeks")                                                                => DurationUnit.Week,
        5 when UnitEquals(text, start, "month")                                                                => DurationUnit.Month,
        5 when UnitEquals(text, start, "years")                                                                => DurationUnit.Year,
        6 when UnitEquals(text, start, "second")                                                               => DurationUnit.Second,
        6 when UnitEquals(text, start, "minute")                                                               => DurationUnit.Minute,
        6 when UnitEquals(text, start, "months")                                                               => DurationUnit.Month,
        7 when UnitEquals(text, start, "seconds")                                                              => DurationUnit.Second,
        7 when UnitEquals(text, start, "minutes")                                                              => DurationUnit.Minute,
        11 when UnitEquals(text, start, "microsecond")                                                         => DurationUnit.Microsecond,
        11 when UnitEquals(text, start, "millisecond")                                                         => DurationUnit.Millisecond,
        12 when UnitEquals(text, start, "microseconds")                                                        => DurationUnit.Microsecond,
        12 when UnitEquals(text, start, "milliseconds")                                                        => DurationUnit.Millisecond,
        _                                                                                                      => DurationUnit.None,
    };

    private static bool UnitEquals(string text, int start, string unit)
    {
        return !unit.Where((t, index) => char.ToLowerInvariant(text[start + index]) != t).Any();
    }

    private static long GetTickFactor(DurationUnit unit) => unit switch
    {
        DurationUnit.Tick        => 1,
        DurationUnit.Microsecond => 10,
        DurationUnit.Millisecond => 10_000,
        DurationUnit.Second      => 10_000_000,
        DurationUnit.Minute      => 600_000_000,
        DurationUnit.Hour        => 36_000_000_000,
        DurationUnit.Day         => 864_000_000_000,
        DurationUnit.Week        => 6_048_000_000_000,
        _                        => 0,
    };

    private enum DurationUnit
    {
        None,
        Tick,
        Microsecond,
        Millisecond,
        Second,
        Minute,
        Hour,
        Day,
        Week,
        Month,
        Year,
    }
}
