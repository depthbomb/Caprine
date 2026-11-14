namespace Caprine.Durations.Tests;

public sealed class CalendarDurationConformanceTests
{
    [Fact]
    public void TryOperationsMatchBclArithmeticAcrossRandomValuesAndBoundaries()
    {
        var    random       = new Random(781923);
        long[] boundaries   = [0, 1, DateTime.MaxValue.Ticks - 1, DateTime.MaxValue.Ticks];
        long[] tickExtremes = [0, 1, -1, long.MinValue, long.MaxValue, TimeSpan.TicksPerDay, -TimeSpan.TicksPerDay];
        for (var index = 0; index < 12_000; index++)
        {
            var years  = index % 3 == 0 ? random.Next(-10_000, 10_001) : random.Next(-5, 6);
            var months = index % 3 == 0 ? random.Next(-120_000, 120_001) : random.Next(-36, 37);
            if (index % 4 == 0)
            {
                years  = 0;
                months = 0;
            }

            var ticks      = index % 2 == 0 ? tickExtremes[index % tickExtremes.Length] : random.Next(-100, 101) * TimeSpan.TicksPerDay;
            var duration   = new CalendarDuration(years, months, ticks);
            var startTicks = index % 5 == 0 ? boundaries[index % boundaries.Length] : random.NextInt64(DateTime.MaxValue.Ticks + 1);
            var start      = new DateTime(startTicks, (DateTimeKind)(index % 3));
            var expected   = TryBcl(() => start.AddYears(years).AddMonths(months).AddTicks(ticks), out var expectedDate);
            Assert.Equal(expected, duration.TryAddTo(start, out var actualDate));
            Assert.Equal(expectedDate.ToBinary(), actualDate.ToBinary());
            Assert.Equal(expected, duration.TryGetTimeSpan(start, out var interval));
            Assert.Equal(expected ? expectedDate - start : TimeSpan.Zero, interval);
            if (expected)
            {
                Assert.Equal(expectedDate.ToBinary(), duration.AddTo(start).ToBinary());
            }

            var date        = DateOnly.FromDateTime(start);
            var expectedDay = default(DateOnly);
            var expectedDaySuccess = ticks % TimeSpan.TicksPerDay == 0 &&
                                     TryBcl(() => date.AddYears(years).AddMonths(months).AddDays((int)(ticks / TimeSpan.TicksPerDay)), out expectedDay);
            Assert.Equal(expectedDaySuccess, duration.TryAddTo(date, out var actualDay));
            Assert.Equal(expectedDay, actualDay);
            if (expectedDaySuccess)
            {
                Assert.Equal(expectedDay, duration.AddTo(date));
            }

            var offset   = TimeSpan.FromMinutes(random.Next(-840, 841));
            var utcTicks = startTicks - offset.Ticks;
            if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks)
            {
                continue;
            }

            var offsetStart           = new DateTimeOffset(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), offset);
            var expectedOffsetSuccess = TryBcl(() => offsetStart.AddYears(years).AddMonths(months).AddTicks(ticks), out var expectedOffset);
            Assert.Equal(expectedOffsetSuccess, duration.TryAddTo(offsetStart, out var actualOffset));
            Assert.Equal(expectedOffset, actualOffset);
            Assert.Equal(expectedOffset.Offset, actualOffset.Offset);
            Assert.Equal(expectedOffsetSuccess, duration.TryGetTimeSpan(offsetStart, out interval));
            Assert.Equal(expectedOffsetSuccess ? expectedOffset - offsetStart : TimeSpan.Zero, interval);
            if (expectedOffsetSuccess)
            {
                Assert.Equal(expectedOffset, duration.AddTo(offsetStart));
            }
        }
    }

    [Theory]
    [InlineData(2, 1, 1, 0, 30, 60, -1, 1, 0)]
    [InlineData(9998, 12, 31, 23, 30, -60, 1, -1, 0)]
    [InlineData(1, 2, 1, 0, 30, 60, 0, -1, 1)]
    [InlineData(9999, 10, 31, 23, 30, -60, 0, 2, -1)]
    public void OffsetLimitsApplyToIntermediateDatesEvenWhenLaterComponentsWouldRepairThem(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int offsetMinutes,
        int years,
        int months,
        int days)
    {
        var start    = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromMinutes(offsetMinutes));
        var duration = new CalendarDuration(years, months, days * TimeSpan.TicksPerDay);
        Assert.Throws<ArgumentOutOfRangeException>(() => start.AddYears(years).AddMonths(months).AddDays(days));
        Assert.Throws<ArgumentOutOfRangeException>(() => duration.AddTo(start));
        Assert.False(duration.TryAddTo(start, out var result));
        Assert.Equal(default, result);
        Assert.False(duration.TryGetTimeSpan(start, out var interval));
        Assert.Equal(TimeSpan.Zero, interval);
    }

    private static bool TryBcl<T>(Func<T> operation, out T result) where T : struct
    {
        try
        {
            result = operation();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }
}
