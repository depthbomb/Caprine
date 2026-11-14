namespace Caprine.Durations.Tests;

public sealed class CalendarDurationTryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void TryMethodsHandleBothDateBoundaries(int sign)
    {
        var period   = new CalendarDuration(0, 0, sign * TimeSpan.TicksPerDay);
        var dateTime = sign > 0 ? DateTime.MaxValue : DateTime.MinValue;
        var offset   = sign > 0 ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue;
        var date     = sign > 0 ? DateOnly.MaxValue : DateOnly.MinValue;
        Assert.False(period.TryAddTo(dateTime, out var dateTimeResult));
        Assert.False(period.TryAddTo(offset, out var offsetResult));
        Assert.False(period.TryAddTo(date, out var dateResult));
        Assert.False(period.TryGetTimeSpan(dateTime, out var interval));
        Assert.False(period.TryGetTimeSpan(offset, out var offsetInterval));
        Assert.Equal(default, dateTimeResult);
        Assert.Equal(default, offsetResult);
        Assert.Equal(default, dateResult);
        Assert.Equal(default, interval);
        Assert.Equal(default, offsetInterval);
    }

    [Fact]
    public void TryMethodsRejectIncompatibleRuntimeValues()
    {
        Assert.False(new CalendarDuration(0, 0, 1).TryAddTo(new DateOnly(2025, 1, 1), out var date));
        Assert.Equal(default, date);
        var calendar = new CalendarDuration(0, 1, 0);
        Assert.False(calendar.TryAddTo(new TimeOnly(12, 0), out var time, out var days));
        Assert.Equal(default, time);
        Assert.Equal(0, days);
        Assert.False(calendar.TryAddTo(new TimeOnly(12, 0), out time));
        Assert.Equal(default, time);
    }

    [Fact]
    public void TryMethodsRespectIntermediateOverflowAndUtcOffsetLimits()
    {
        var cancelling = new CalendarDuration(1, -12, 0);
        Assert.False(cancelling.TryAddTo(DateTime.MaxValue, out _));
        Assert.False(cancelling.TryAddTo(DateTimeOffset.MaxValue, out _));
        Assert.False(cancelling.TryAddTo(DateOnly.MaxValue, out _));

        var nearUtcMaximum = new DateTimeOffset(9999, 12, 31, 10, 0, 0, TimeSpan.FromHours(-12));
        var hours          = new CalendarDuration(0, 0, TimeSpan.TicksPerHour * 3);
        Assert.False(hours.TryAddTo(nearUtcMaximum, out var result));
        Assert.Equal(default, result);
        Assert.False(hours.TryGetTimeSpan(nearUtcMaximum, out var interval));
        Assert.Equal(default, interval);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void ClockOperationsSupportTheEntireFixedRange(long ticks)
    {
        var duration     = new CalendarDuration(0, 0, ticks);
        var remainder    = ticks % TimeSpan.TicksPerDay;
        var expectedDays = (int)(ticks / TimeSpan.TicksPerDay);
        if (remainder < 0)
        {
            remainder += TimeSpan.TicksPerDay;
            expectedDays--;
        }

        Assert.True(duration.TryAddTo(TimeOnly.MinValue, out var result, out var days));
        Assert.Equal(new TimeOnly(remainder), result);
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void AttributeRetainsAndValidatesOptions()
    {
        var attribute = new GeneratedDurationAttribute("1tick", DurationParseOptions.Strict);
        Assert.Equal("1tick", attribute.Expression);
        Assert.Equal(DurationParseOptions.Strict, attribute.Options);
        Assert.Equal(DurationParseOptions.None, new GeneratedDurationAttribute("ignored").Options);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratedDurationAttribute("1s", (DurationParseOptions)2));
        Assert.Throws<ArgumentNullException>(() => new GeneratedDurationAttribute(null!));
    }
}
