namespace Caprine.Durations.Tests;

internal static partial class GeneratedSamples
{
    private const string FixedExpression = "1w 4 days " + "4 hr and 100ms";

    [GeneratedDuration(FixedExpression)]
    internal static partial TimeSpan Delay();

    [GeneratedDuration("1 year 1 month")]
    internal static partial CalendarDuration Renewal();

    [GeneratedDuration("13mo")]
    internal static partial CalendarDuration ThirteenMonths();

    [GeneratedDuration("1microsecond 2µs 3μs")]
    internal static partial TimeSpan Microseconds();

    [GeneratedDuration("2months 3d")]
    internal static partial CalendarDuration DatePeriod();

    [GeneratedDuration("27h")]
    internal static partial CalendarDuration ClockPeriod();

    [GeneratedDuration("all ignored; 1ns 1monthology")]
    internal static partial TimeSpan Zero();

    [GeneratedDuration("-922337203685477580.8us")]
    internal static partial TimeSpan Minimum();

    [GeneratedDuration("1mo -1mo 1h")]
    internal static partial TimeSpan CancelledCalendar();
}
