using System.Globalization;
using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class CronTimeZoneTests
{
    [Theory]
    [InlineData("50 1 * * *", "2026-11-01T01:45:00-05:00", "2026-11-01T01:50:00-05:00", true)]
    [InlineData("30 1 * * *", "2026-11-01T01:45:00-04:00", "2026-11-01T01:30:00-04:00", false)]
    [InlineData("30 1 * * *", "2026-11-01T01:45:00-04:00", "2026-11-01T01:30:00-05:00", true)]
    [InlineData("30 1 * * *", "2026-11-01T01:15:00-05:00", "2026-11-01T01:30:00-04:00", false)]
    [InlineData("30 1 * * *", "2026-11-01T01:30:00-04:00", "2026-11-01T01:30:00-05:00", true)]
    [InlineData("30 1 * * *", "2026-11-01T01:30:00-05:00", "2026-11-01T01:30:00-04:00", false)]
    [InlineData("30 2,3 * * *", "2026-03-08T01:59:00-05:00", "2026-03-08T03:30:00-04:00", true)]
    [InlineData("30 1,2 * * *", "2026-03-08T03:01:00-04:00", "2026-03-08T01:30:00-05:00", false)]
    [InlineData("30 2 * * *", "2026-03-08T01:59:00-05:00", "2026-03-09T02:30:00-04:00", true)]
    [InlineData("30 2 * * *", "2026-03-08T03:01:00-04:00", "2026-03-07T02:30:00-05:00", false)]
    public void NewYorkTransitionsRespectInstantOrdering(string text, string start, string expected, bool forward)
    {
        var zone       = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var expression = new CronExpression(text);
        var anchor     = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture);
        var occurrence = forward ? expression.GetNextOccurrence(anchor, zone) : expression.GetLastOccurrence(anchor, zone);
        Assert.NotNull(occurrence);
        Assert.True(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture).EqualsExact(occurrence.Value));
    }

    [Theory]
    [InlineData("America/New_York", 2026, 3, 8)]
    [InlineData("America/New_York", 2026, 11, 1)]
    [InlineData("Australia/Lord_Howe", 2026, 4, 4)]
    [InlineData("Australia/Lord_Howe", 2026, 10, 3)]
    [InlineData("Europe/Dublin", 2026, 10, 25)]
    [InlineData("Europe/Dublin", 2026, 3, 29)]
    [InlineData("Pacific/Apia", 2011, 12, 29)]
    [InlineData("Pacific/Kwajalein", 1993, 8, 20)]
    [InlineData("Pacific/Chatham", 2026, 4, 4)]
    public void SearchMatchesIndependentUtcScanAcrossTransitions(string zoneId, int year, int month, int day)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        CheckUtcScan(zone, Utc(year, month, day), TimeSpan.FromDays(2));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(-30)]
    public void CustomNonHourAndNegativeDaylightAdjustmentsWork(int minutes)
    {
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 10);
        var end   = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 10);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            TimeSpan.FromMinutes(minutes), start, end);
        var zone = TimeZoneInfo.CreateCustomTimeZone("custom", TimeSpan.FromMinutes(345), "custom", "standard", "daylight", [rule]);
        CheckUtcScan(zone, Utc(2026, 3, 9), TimeSpan.FromDays(2));
        CheckUtcScan(zone, Utc(2026, 10, 9), TimeSpan.FromDays(2));
    }

    [Fact]
    public void EverySecondTraversesTheFoldWithoutSkippingOrRepeatingInstants()
    {
        var zone       = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var expression = new CronExpression("* * * * * *", CronExpressionOptions.IncludeSeconds);
        var anchor     = Utc(2026, 11, 1, 5, 59, 58);
        for (var index = 0; index < 5; index++)
        {
            var next = expression.GetNextOccurrence(anchor, zone)!.Value;
            Assert.Equal(anchor.AddSeconds(1), next);
            Assert.Equal(anchor, expression.GetLastOccurrence(next, zone));
            anchor = next;
        }
    }

    private static void CheckUtcScan(TimeZoneInfo zone, DateTimeOffset start, TimeSpan length)
    {
        // Expected results come from walking instants and checking wall-clock fields directly.
        // This does not use CronExpression.IsMatch or either occurrence-search implementation.
        var end      = start + length;
        var expected = new List<DateTimeOffset>();
        for (var instant = start.AddMinutes(1); instant < end; instant = instant.AddMinutes(1))
        {
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            if (local.Hour is >= 1 and <= 3 && local.Minute % 15 == 0)
            {
                expected.Add(local);
            }
        }

        var expression = new CronExpression("*/15 1-3 * * *");
        var anchor     = start;
        foreach (var item in expected)
        {
            var next = expression.GetNextOccurrence(anchor, zone);
            Assert.NotNull(next);
            Assert.True(item.EqualsExact(next.Value), $"Next after {anchor:O} in {zone.Id}: expected {item:O}, got {next:O}");
            anchor = next.Value;
        }

        Assert.True(expression.GetNextOccurrence(anchor, zone) >= end);

        anchor = end;
        foreach (var item in expected.AsEnumerable().Reverse())
        {
            var previous = expression.GetLastOccurrence(anchor, zone);
            Assert.NotNull(previous);
            Assert.True(item.EqualsExact(previous.Value), $"Previous before {anchor:O} in {zone.Id}: expected {item:O}, got {previous:O}");
            anchor = previous.Value;
        }

        Assert.True(expression.GetLastOccurrence(anchor, zone) <= start);
    }
}
