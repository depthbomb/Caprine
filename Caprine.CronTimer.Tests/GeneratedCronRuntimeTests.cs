using Microsoft.Extensions.Time.Testing;
using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class GeneratedCronRuntimeTests
{
    [Theory]
    [MemberData(nameof(Expressions))]
    public void GeneratedExpressionsMatchRuntimeParsing(CronExpression generated)
    {
        var parsed = new CronExpression(generated.Expression, generated.Options);
        var anchors = new[]
        {
            Utc(2026, 1, 1), Utc(2026, 6, 8, 13), Utc(2026, 3, 8, 7),
            Utc(2026, 11, 1, 5, 45), Utc(2026, 11, 1, 6, 15), Utc(2028, 2, 29, 3),
            Utc(2120, 1, 1), DateTimeOffset.MinValue, DateTimeOffset.MaxValue,
        };
        foreach (var zone in new[] { TimeZoneInfo.Utc, TimeZoneInfo.FindSystemTimeZoneById("America/New_York") })
        {
            foreach (var anchor in anchors)
            {
                Assert.Equal(parsed.GetNextOccurrence(anchor, zone), generated.GetNextOccurrence(anchor, zone));
                Assert.Equal(parsed.GetLastOccurrence(anchor, zone), generated.GetLastOccurrence(anchor, zone));
                Assert.Equal(parsed.IsMatch(anchor, zone), generated.IsMatch(anchor, zone));
            }
        }
    }

    [Fact]
    public void GeneratedMembersReturnCachedInstances()
    {
        Assert.Same(GeneratedSamples.EveryFiveMinutes(), GeneratedSamples.EveryFiveMinutes());
        Assert.Same(GeneratedSamples.LeadingWildcard, GeneratedSamples.LeadingWildcard);
        Parallel.For(0, 100, _ => Assert.Same(GeneratedSamples.AllFields(), GeneratedSamples.AllFields()));
    }

    [Fact]
    public async Task TimersReuseGeneratedExpressionsAndKeepIndependentMutableSchedules()
    {
        var       expression = GeneratedSamples.Secondly;
        var       clock      = new FakeTimeProvider(Utc(2026, 1, 1));
        using var first      = new CronTimer(expression, clock, TimeZoneInfo.Utc);
        using var second     = new CronTimer(expression, clock);
        Assert.Equal(expression.Options, first.Options);
        first.Expression = "*/2 * * * * *";
        var firstWait  = first.WaitForNextTickAsync();
        var secondWait = second.WaitForNextTickAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await secondWait);
        Assert.False(firstWait.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await firstWait);
        Assert.Equal("@secondly", second.Expression);
        Assert.Equal("@secondly", expression.Expression);
    }

    [Fact]
    public void ParsedConstructorOverloadsPreserveOptionsAndValidateNulls()
    {
        using var system = new CronTimer(GeneratedSamples.Secondly);
        using var zone   = new CronTimer(GeneratedSamples.Secondly, TimeZoneInfo.Utc);
        Assert.Equal(GeneratedSamples.Secondly.Options, system.Options);
        Assert.Same(TimeZoneInfo.Utc, zone.TimeZone);
        Assert.Throws<ArgumentNullException>(() => new CronTimer((CronExpression)null!));
        Assert.Throws<ArgumentNullException>(() => new CronTimer(GeneratedSamples.Secondly, (TimeProvider)null!));
    }

    [Fact]
    public void GeneratedDataIsCopiedAndTheTextIsNotParsedAgain()
    {
        var bytes     = new CronExpressionData("*/5 * * * *", CronExpressionOptions.None).Encode();
        var generated = CronExpression.FromGenerated("display text, intentionally not cron", CronExpressionOptions.None, bytes);
        Array.Clear(bytes);
        Assert.Equal(Utc(2026, 1, 1, 0, 5), generated.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal("display text, intentionally not cron", generated.Expression);
    }

    [Fact]
    public void MalformedGeneratedDataIsRejected()
    {
        var valid = new CronExpressionData("* * * * *", CronExpressionOptions.None).Encode();
        Assert.Equal(42, valid.Length);
        Assert.Throws<ArgumentException>(() => CronExpression.FromGenerated("* * * * *", CronExpressionOptions.None, []));
        Assert.Throws<ArgumentException>(() => CronExpression.FromGenerated("* * * * *", CronExpressionOptions.None, valid[..^1]));
        Assert.Throws<ArgumentException>(() => CronExpression.FromGenerated("* * * * *", CronExpressionOptions.None, [.. valid, 0]));
        foreach (var index in new[] { 0, 1, 9, 17, 22, 27, 31, 40, 41 })
        {
            var bytes = valid.ToArray();
            bytes[index] = 255;
            Assert.Throws<ArgumentException>(() => CronExpression.FromGenerated("* * * * *", CronExpressionOptions.None, bytes));
        }

        Assert.Throws<ArgumentNullException>(() => CronExpression.FromGenerated(null!, CronExpressionOptions.None, valid));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpression.FromGenerated("* * * * *", (CronExpressionOptions)16, valid));
    }

    public static IEnumerable<object[]> Expressions()
    {
        yield return [GeneratedSamples.EveryFiveMinutes()];
        yield return [GeneratedSamples.LeadingWildcard];
        yield return [GeneratedSamples.TrailingWildcard];
        yield return [GeneratedSamples.LastDays];
        yield return [GeneratedSamples.Weekdays];
        yield return [GeneratedSamples.WeekdayOrdinals];
        yield return [GeneratedSamples.AllFields()];
        yield return [GeneratedSamples.Secondly];
        yield return [GeneratedSamples.Yearly()];
        yield return [GeneratedSamples.LargeStep];
        yield return [GeneratedSamples.Dst];
        yield return [GeneratedSamples.Impossible()];
    }
}
