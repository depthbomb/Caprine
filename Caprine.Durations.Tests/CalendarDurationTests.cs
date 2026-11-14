namespace Caprine.Durations.Tests;

public sealed class CalendarDurationTests
{
    [Fact]
    public void GeneratedValuesUseExactTicks()
    {
        Assert.Equal(new TimeSpan(11, 4, 0, 0, 100), GeneratedSamples.Delay());
        Assert.Equal(60, GeneratedSamples.Microseconds().Ticks);
        Assert.Equal(TimeSpan.Zero, GeneratedSamples.Zero());
        Assert.Equal(TimeSpan.MinValue, GeneratedSamples.Minimum());
        Assert.Equal(TimeSpan.FromHours(1), GeneratedSamples.CancelledCalendar());
    }

    [Fact]
    public void AppliesYearsThenMonthsAndPreservesKind()
    {
        var leapDay = new DateTime(2024, 2, 29, 12, 30, 0, DateTimeKind.Utc);
        var result  = leapDay.Add(GeneratedSamples.Renewal());
        Assert.Equal(new DateTime(2025, 3, 28, 12, 30, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2025, 3, 29, 12, 30, 0, DateTimeKind.Utc), GeneratedSamples.ThirteenMonths().AddTo(leapDay));
    }

    [Fact]
    public void UsesClampingAndKeepsComponentsDistinct()
    {
        var start     = new DateTime(2025, 1, 31);
        var oneMonth  = new CalendarDuration(0, 1, 0);
        var twoMonths = new CalendarDuration(0, 2, 0);
        Assert.Equal(new DateTime(2025, 3, 31), twoMonths.AddTo(start));
        Assert.Equal(new DateTime(2025, 3, 28), oneMonth.AddTo(oneMonth.AddTo(start)));
        Assert.NotEqual(new CalendarDuration(1, 0, 0), new CalendarDuration(0, 12, 0));
        Assert.Equal(new CalendarDuration(1, 1, 0), GeneratedSamples.Renewal());
    }

    [Fact]
    public void PreservesDateTimeOffsetAndSupportsAnchoredIntervals()
    {
        var start  = new DateTimeOffset(2025, 1, 31, 23, 0, 0, TimeSpan.FromHours(-6));
        var period = new CalendarDuration(0, 1, 10);
        var end    = start.Add(period);
        Assert.Equal(start.Offset, end.Offset);
        Assert.Equal(new DateTimeOffset(2025, 2, 28, 23, 0, 0, start.Offset).AddTicks(10), end);
        Assert.Equal(TimeSpan.FromDays(28).Add(TimeSpan.FromTicks(10)), period.ToTimeSpan(start));
        Assert.Equal(period.AddTo(start.UtcDateTime) - start.UtcDateTime, period.ToTimeSpan(start.UtcDateTime));
    }

    [Fact]
    public void RequiresAnchorForCalendarComponents()
    {
        var period = GeneratedSamples.Renewal();
        Assert.False(period.TryGetTimeSpan(out var missing));
        Assert.Equal(TimeSpan.Zero, missing);
        Assert.Throws<InvalidOperationException>(() => period.ToTimeSpan());
        Assert.True(GeneratedSamples.ClockPeriod().TryGetTimeSpan(out var fixedValue));
        Assert.Equal(TimeSpan.FromHours(27), fixedValue);
    }

    [Fact]
    public void DateOnlyRejectsLossOfTimePrecision()
    {
        var start = new DateOnly(2025, 1, 31);
        Assert.Equal(new DateOnly(2025, 4, 3), start.Add(GeneratedSamples.DatePeriod()));
        Assert.Throws<InvalidOperationException>(() => new CalendarDuration(0, 0, 1).AddTo(start));
        Assert.Throws<InvalidOperationException>(() => new CalendarDuration(0, 0, -1).AddTo(start));
    }

    [Fact]
    public void TimeOnlyReportsPositiveAndNegativeDayWrapping()
    {
        var start = new TimeOnly(23, 0);
        Assert.Equal(new TimeOnly(2, 0), start.Add(GeneratedSamples.ClockPeriod(), out var days));
        Assert.Equal(2, days);
        Assert.Equal(new TimeOnly(2, 0), GeneratedSamples.ClockPeriod().AddTo(start));
        var negative = new CalendarDuration(0, 0, -TimeSpan.TicksPerDay - TimeSpan.TicksPerHour);
        Assert.Equal(new TimeOnly(22, 0), negative.AddTo(start, out var previousDays));
        Assert.Equal(-1, previousDays);
        Assert.Throws<InvalidOperationException>(() => GeneratedSamples.Renewal().AddTo(start));
    }

    [Fact]
    public void ValidatesCalendarLimitsAndAnchorDependentOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarDuration(10_001, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarDuration(0, -120_001, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarDuration(0, 0, 1).AddTo(DateTime.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarDuration(0, 0, -1).AddTo(DateTimeOffset.MinValue));
        Assert.Equal(default, CalendarDuration.Zero);
    }
}
