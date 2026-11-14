using System.Globalization;
using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class CronSearchConsistencyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2026)]
    [InlineData(9600)]
    [InlineData(9999)]
    public void ImpossibleDatesStayEmptyAcrossCalendarCycles(int year)
    {
        foreach (var pattern in new[] { "0 0 31 FEB *", "0 0 30 FEB *", "0 0 31 APR,JUN,SEP,NOV *" })
        {
            var expression = new CronExpression(pattern);
            Assert.Null(expression.GetNextOccurrence(Utc(year, 6, 1), TimeZoneInfo.Utc));
            Assert.Null(expression.GetLastOccurrence(Utc(year, 6, 1), TimeZoneInfo.Utc));
        }
    }

    [Fact]
    public void ExplicitYearsCanBeMoreThanFourCenturiesAway()
    {
        var next     = new CronExpression("0 0 29 FEB * 8000", CronExpressionOptions.IncludeYear);
        var previous = new CronExpression("0 0 29 FEB * 400", CronExpressionOptions.IncludeYear);
        Assert.Equal(Utc(8000, 2, 29), next.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal(Utc(400, 2, 29), previous.GetLastOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/Dublin")]
    [InlineData("Australia/Lord_Howe")]
    public void RandomSchedulesAgreeWithIndependentMinuteScans(string zoneId)
    {
        var zone   = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var random = new Random(68471);
        var start  = Utc(2026, 10, 24);
        var end    = start.AddDays(10);
        for (var sample = 0; sample < 16; sample++)
        {
            var minute               = random.Next(60);
            var hour                 = random.Next(24);
            var day                  = random.Next(24, 32);
            var weekday              = random.Next(7);
            var unrestrictedMonthDay = sample % 2 == 0;
            var unrestrictedWeekday  = sample % 3 == 0;
            var expression = new CronExpression(string.Create(CultureInfo.InvariantCulture,
                $"{minute} {hour} {(unrestrictedMonthDay ? "*" : day)} * {(unrestrictedWeekday ? "*" : weekday)}"));
            var expected = new List<DateTimeOffset>();
            for (var instant = start.AddMinutes(1); instant < end; instant = instant.AddMinutes(1))
            {
                var local          = TimeZoneInfo.ConvertTime(instant, zone);
                var dayMatches     = unrestrictedMonthDay || local.Day            == day;
                var weekdayMatches = unrestrictedWeekday  || (int)local.DayOfWeek == weekday;
                var dateMatches = !unrestrictedMonthDay && !unrestrictedWeekday
                    ? dayMatches || weekdayMatches
                    : dayMatches && weekdayMatches;
                if (dateMatches && local.Hour == hour && local.Minute == minute)
                {
                    expected.Add(local);
                }
            }

            AssertOccurrences(expression, zone, start, end, expected);
        }
    }

    [Theory]
    [InlineData(45)]
    [InlineData(-30)]
    public void FractionalSecondTransitionsAgreeWithIndependentUtcScans(int daylightMinutes)
    {
        var time = new DateTime(1, 1, 1, 2, 0, 0, 500);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            TimeSpan.FromMinutes(daylightMinutes),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(time, 3, 10),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(time, 10, 10));
        var zone       = TimeZoneInfo.CreateCustomTimeZone("fractional", TimeSpan.Zero, "fractional", "standard", "daylight", [rule]);
        var expression = new CronExpression("*/7 * * * * *", CronExpressionOptions.IncludeSeconds);
        foreach (var month in new[] { 3, 10 })
        {
            var start    = Utc(2026, month, 10);
            var end      = start.AddHours(4);
            var expected = new List<DateTimeOffset>();
            for (var instant = start.AddSeconds(1); instant < end; instant = instant.AddSeconds(1))
            {
                var local = TimeZoneInfo.ConvertTime(instant, zone);
                if (local.Second % 7 == 0)
                {
                    expected.Add(local);
                }
            }

            AssertOccurrences(expression, zone, start, end, expected);
        }
    }

    private static void AssertOccurrences(CronExpression       expression,
                                          TimeZoneInfo         zone,
                                          DateTimeOffset       start,
                                          DateTimeOffset       end,
                                          List<DateTimeOffset> expected)
    {
        var anchor = start;
        foreach (var item in expected)
        {
            var next = expression.GetNextOccurrence(anchor, zone);
            Assert.NotNull(next);
            Assert.True(item.EqualsExact(next.Value), $"{expression}: next after {anchor:O}: expected {item:O}, got {next:O}.");
            Assert.True(expression.IsMatch(item, zone));
            anchor = next.Value;
        }

        Assert.True(expression.GetNextOccurrence(anchor, zone) >= end);
        anchor = end;
        foreach (var item in expected.AsEnumerable().Reverse())
        {
            var previous = expression.GetLastOccurrence(anchor, zone);
            Assert.NotNull(previous);
            Assert.True(item.EqualsExact(previous.Value), $"{expression}: previous before {anchor:O}: expected {item:O}, got {previous:O}.");
            anchor = previous.Value;
        }

        Assert.True(expression.GetLastOccurrence(anchor, zone) <= start);
    }
}
