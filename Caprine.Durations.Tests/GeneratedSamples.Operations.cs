namespace Caprine.Durations.Tests;

internal static partial class GeneratedSamples
{
    [GeneratedDuration("30s", DurationParseOptions.Strict)]
    internal static partial TimeSpan RequestTimeout { get; }

    [GeneratedDuration("1y, 1mo and 2ticks", DurationParseOptions.Strict)]
    internal static partial CalendarDuration CalendarProperty { get; }

    [GeneratedDuration("9223372036854775807ticks", DurationParseOptions.Strict)]
    internal static partial TimeSpan MaximumTicks { get; }

    [GeneratedDuration("-9223372036854775808ticks", DurationParseOptions.Strict)]
    internal static partial TimeSpan MinimumTicks { get; }

    [GeneratedDuration("1y 1mo")]
    internal static partial DateTime Renewal(DateTime start);

    [GeneratedDuration("1y 1mo")]
    internal static partial DateTimeOffset Renewal(DateTimeOffset start);

    [GeneratedDuration("1y 1mo")]
    internal static partial DateOnly Renewal(DateOnly start);

    [GeneratedDuration("1mo", DurationParseOptions.Strict)]
    internal static partial TimeSpan BillingPeriodLength(DateTime start);

    [GeneratedDuration("1mo", DurationParseOptions.Strict)]
    internal static partial TimeSpan BillingPeriodLength(DateTimeOffset start);

    [GeneratedDuration("1mo")]
    internal static partial bool TryBillingPeriodLength(DateTime start, out TimeSpan duration);

    [GeneratedDuration("1mo")]
    internal static partial bool TryBillingPeriodLength(DateTimeOffset start, out TimeSpan duration);

    [GeneratedDuration("1day")]
    internal static partial bool TryAdvance(DateTime start, out DateTime result);

    [GeneratedDuration("1day")]
    internal static partial bool TryAdvance(DateTime start, out TimeSpan duration);

    [GeneratedDuration("1day")]
    internal static partial bool TryAdvance(DateTimeOffset start, out DateTimeOffset result);

    [GeneratedDuration("1day")]
    internal static partial bool TryAdvance(DateOnly start, out DateOnly result);

    [GeneratedDuration("27h")]
    internal static partial TimeOnly AdvanceClock(TimeOnly start);

    [GeneratedDuration("27h")]
    internal static partial TimeOnly AdvanceClock(TimeOnly start, out int wrappedDays);

    [GeneratedDuration("27h")]
    internal static partial bool TryAdvanceClock(TimeOnly start, out TimeOnly result);

    [GeneratedDuration("27h")]
    internal static partial bool TryAdvanceClock(TimeOnly start, out TimeOnly result, out int wrappedDays);

    [GeneratedDuration("-27h")]
    internal static partial bool TryRetreatClock(TimeOnly start, out TimeOnly result, out int wrappedDays);

    [GeneratedDuration("1tick", DurationParseOptions.Strict)]
    internal static partial DateTime AddTick(DateTime start);

    [GeneratedDuration("12h 12h", DurationParseOptions.Strict)]
    internal static partial DateOnly AddDay(DateOnly start);
}
