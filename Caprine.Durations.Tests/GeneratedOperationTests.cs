namespace Caprine.Durations.Tests;

public sealed class GeneratedOperationTests
{
    [Fact]
    public void PropertiesReturnExactValuesIncludingTickLimits()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), GeneratedSamples.RequestTimeout);
        Assert.Equal(new CalendarDuration(1, 1, 2), GeneratedSamples.CalendarProperty);
        Assert.Equal(TimeSpan.MaxValue, GeneratedSamples.MaximumTicks);
        Assert.Equal(TimeSpan.MinValue, GeneratedSamples.MinimumTicks);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void GeneratedCalendarOperationsPreserveOrderKindAndTicks(DateTimeKind kind)
    {
        var start    = new DateTime(2024, 2, 29, 12, 30, 0, kind).AddTicks(7);
        var expected = new DateTime(2025, 3, 28, 12, 30, 0, kind).AddTicks(7);
        Assert.Equal(expected, GeneratedSamples.Renewal(start));
        Assert.Equal(kind, GeneratedSamples.Renewal(start).Kind);
        Assert.Equal(start.Ticks + 1, GeneratedSamples.AddTick(start).Ticks);
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSamples.AddTick(DateTime.MaxValue));
    }

    [Fact]
    public void GeneratedDateAndOffsetOperationsKeepExistingCalendarSemantics()
    {
        var offset = TimeSpan.FromHours(-6);
        var start  = new DateTimeOffset(2024, 2, 29, 12, 30, 0, offset);
        var result = GeneratedSamples.Renewal(start);
        Assert.Equal(new DateTimeOffset(2025, 3, 28, 12, 30, 0, offset), result);
        Assert.Equal(offset, result.Offset);
        Assert.Equal(new DateOnly(2025, 3, 28), GeneratedSamples.Renewal(new DateOnly(2024, 2, 29)));
        Assert.Equal(new DateOnly(2024, 2, 29), GeneratedSamples.AddDay(new DateOnly(2024, 2, 28)));
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2025, 4, 30)]
    [InlineData(2025, 5, 31)]
    public void GeneratedIntervalsDependOnTheAnchor(int year, int month, int days)
    {
        var start       = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var offsetStart = new DateTimeOffset(start);
        var expected    = TimeSpan.FromDays(days);
        Assert.Equal(expected, GeneratedSamples.BillingPeriodLength(start));
        Assert.Equal(expected, GeneratedSamples.BillingPeriodLength(offsetStart));
        Assert.True(GeneratedSamples.TryBillingPeriodLength(start, out var dateInterval));
        Assert.True(GeneratedSamples.TryBillingPeriodLength(offsetStart, out var offsetInterval));
        Assert.Equal(expected, dateInterval);
        Assert.Equal(expected, offsetInterval);
    }

    [Fact]
    public void GeneratedTryOperationsReturnValuesOrResetOutputsOnOverflow()
    {
        var start       = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var offsetStart = new DateTimeOffset(start);
        var dateStart   = DateOnly.FromDateTime(start);
        Assert.True(GeneratedSamples.TryAdvance(start, out DateTime date));
        Assert.True(GeneratedSamples.TryAdvance(start, out TimeSpan interval));
        Assert.True(GeneratedSamples.TryAdvance(offsetStart, out var offset));
        Assert.True(GeneratedSamples.TryAdvance(dateStart, out var dateOnly));
        Assert.Equal(new DateTime(2025, 2, 1), date);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(TimeSpan.FromDays(1), interval);
        Assert.Equal(new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero), offset);
        Assert.Equal(new DateOnly(2025, 2, 1), dateOnly);

        Assert.False(GeneratedSamples.TryAdvance(DateTime.MaxValue, out date));
        Assert.False(GeneratedSamples.TryAdvance(DateTime.MaxValue, out interval));
        Assert.False(GeneratedSamples.TryAdvance(DateTimeOffset.MaxValue, out offset));
        Assert.False(GeneratedSamples.TryAdvance(DateOnly.MaxValue, out dateOnly));
        Assert.Equal(default, date);
        Assert.Equal(default, interval);
        Assert.Equal(default, offset);
        Assert.Equal(default, dateOnly);

        Assert.False(GeneratedSamples.TryBillingPeriodLength(DateTime.MaxValue, out interval));
        Assert.Equal(TimeSpan.Zero, interval);
        Assert.False(GeneratedSamples.TryBillingPeriodLength(DateTimeOffset.MaxValue, out interval));
        Assert.Equal(TimeSpan.Zero, interval);
    }

    [Fact]
    public void GeneratedClockOperationsReportPositiveAndNegativeWrapping()
    {
        var start = new TimeOnly(23, 0);
        Assert.Equal(new TimeOnly(2, 0), GeneratedSamples.AdvanceClock(start));
        Assert.Equal(new TimeOnly(2, 0), GeneratedSamples.AdvanceClock(start, out var days));
        Assert.Equal(2, days);
        Assert.True(GeneratedSamples.TryAdvanceClock(start, out var result));
        Assert.Equal(new TimeOnly(2, 0), result);
        Assert.True(GeneratedSamples.TryAdvanceClock(start, out result, out days));
        Assert.Equal(new TimeOnly(2, 0), result);
        Assert.Equal(2, days);
        Assert.True(GeneratedSamples.TryRetreatClock(start, out result, out days));
        Assert.Equal(new TimeOnly(20, 0), result);
        Assert.Equal(-1, days);
    }
}
