using System.Globalization;

namespace Caprine.CronTimer.Tests;

public sealed class CronExpressionTests
{
    [Theory]
    [InlineData("* * * * *")]
    [InlineData(" \t 0  12\t* * MON-FRI \t")]
    [InlineData("*/15 0-23/2 1,15 JAN,Jun,DEC MON-5")]
    [InlineData("1-59/2147483647 * * * *")]
    [InlineData("1-59/999999999999999999999 * * * *")]
    [InlineData("0 0 *,1 *,1 *,1")]
    [InlineData("0 0 31 FEB *")]
    [InlineData("0 0 1-31/1 * 0,7")]
    [InlineData("0001 00 01 01 07")]
    public void AcceptsOcpsSyntax(string text)
    {
        Assert.True(CronExpression.TryParse(text, out var expression));
        Assert.Equal(text, expression.Expression);
        Assert.Equal(text, expression.ToString());
        Assert.Equal(CronExpressionOptions.None, expression.Options);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("* * * * * * *")]
    [InlineData("@daily")]
    [InlineData("0/15 * * * *")]
    [InlineData("/15 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("*/-1 * * * *")]
    [InlineData("*/+1 * * * *")]
    [InlineData("1-0 * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("* * * * MON-SUN")]
    [InlineData("0 0 * JANUARY *")]
    [InlineData("0 0 * * MONDAY")]
    [InlineData("0 0 ? * *")]
    [InlineData("0 0 L * *")]
    [InlineData("0 0 15W * *")]
    [InlineData("0 0 * * 5#2")]
    [InlineData("0 0 * * FRI-L")]
    [InlineData("+1 * * * *")]
    [InlineData("1.0 * * * *")]
    [InlineData("١ * * * *")]
    [InlineData("1\0 * * * *")]
    [InlineData("\n0 0 * * *")]
    [InlineData("0 0 * * *\r")]
    [InlineData("0\v0 * * *")]
    [InlineData("0\u00a00 * * *")]
    [InlineData("0 0 *,999 * *")]
    [InlineData("0 0 * * *,8")]
    [InlineData("0 0 * *,BOG *")]
    [InlineData("0 0 * * *,")]
    [InlineData("0,,1 0 * * *")]
    [InlineData("1-2-3 * * * *")]
    [InlineData("*/1/2 * * * *")]
    [InlineData("0 0 * * * # comment")]
    public void RejectsNonOcpsSyntax(string text)
    {
        Assert.Throws<FormatException>(() => new CronExpression(text));
        Assert.False(CronExpression.TryParse(text, out var expression));
        Assert.Null(expression);
    }

    [Fact]
    public void NullAndInvalidOptionsHavePredictableExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => new CronExpression(null!));
        Assert.False(CronExpression.TryParse(null, out var result));
        Assert.Null(result);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CronExpression("* * * * *", (CronExpressionOptions)16));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpression.TryParse(null, out _, (CronExpressionOptions)(-1)));
    }

    [Theory]
    [InlineData("*", "MON", 15)]
    [InlineData("*,1", "MON", 15)]
    [InlineData("1,*", "MON", 9)]
    [InlineData("*/1", "MON", 9)]
    [InlineData("1-31", "MON", 9)]
    [InlineData("15", "*", 15)]
    [InlineData("15", "*,MON", 15)]
    [InlineData("15", "MON,*", 9)]
    [InlineData("15", "*/1", 9)]
    [InlineData("15", "0-7", 9)]
    [InlineData("9", "MON", 9)]
    public void LeadingLiteralWildcardControlsDayCombination(string monthDay, string weekday, int day)
    {
        var expression = new CronExpression($"0 12 {monthDay} * {weekday}");
        var anchor     = Utc(2026, 6, 8, 13);
        var expected   = Utc(2026, 6, day, 12);
        Assert.Equal(expected, expression.GetNextOccurrence(anchor, TimeZoneInfo.Utc));
        Assert.True(expression.IsMatch(expected, TimeZoneInfo.Utc));
    }

    [Fact]
    public void StepsStartAtRangeStartAndLargeStepsDoNotOverflow()
    {
        var expression = new CronExpression("1-59/999999999999999999999 * * * *");
        Assert.Equal(Utc(2026, 1, 1, 0, 1), expression.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 1, 1, 1, 1), expression.GetNextOccurrence(Utc(2026, 1, 1, 0, 1), TimeZoneInfo.Utc));
        var stepped = new CronExpression("5-59/20 * * * *");
        Assert.Equal(Utc(2026, 1, 1, 0, 25), stepped.GetNextOccurrence(Utc(2026, 1, 1, 0, 5), TimeZoneInfo.Utc));
    }

    [Fact]
    public void SearchesAreExclusiveAndPreserveFractionalBounds()
    {
        var expression = new CronExpression("* * * * *");
        var anchor     = Utc(2026, 6, 12, 12);
        Assert.Equal(anchor.AddMinutes(1), expression.GetNextOccurrence(anchor, TimeZoneInfo.Utc));
        Assert.Equal(anchor.AddMinutes(-1), expression.GetLastOccurrence(anchor, TimeZoneInfo.Utc));
        Assert.Equal(anchor, expression.GetLastOccurrence(anchor.AddTicks(1), TimeZoneInfo.Utc));
        Assert.Equal(anchor, expression.GetNextOccurrence(anchor.AddTicks(-1), TimeZoneInfo.Utc));
        Assert.False(expression.IsMatch(anchor.AddTicks(1), TimeZoneInfo.Utc));
        Assert.False(expression.IsMatch(anchor.AddSeconds(1), TimeZoneInfo.Utc));
        Assert.True(expression.IsMatch(anchor.ToOffset(TimeSpan.FromHours(9)), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1960)]
    [InlineData(2120)]
    [InlineData(9999)]
    public void UnrestrictedYearsUseTheWholeDateTimeRange(int year)
    {
        var expression = new CronExpression("0 0 * * *");
        Assert.Equal(Utc(year, 1, 2), expression.GetNextOccurrence(Utc(year, 1, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal(Utc(year, 1, 1), expression.GetLastOccurrence(Utc(year, 1, 2), TimeZoneInfo.Utc));
    }

    [Fact]
    public void DateTimeBoundsReturnNullInsteadOfOverflowing()
    {
        var expression = new CronExpression("* * * * *");
        Assert.Null(expression.GetNextOccurrence(DateTimeOffset.MaxValue, TimeZoneInfo.Utc));
        Assert.Null(expression.GetLastOccurrence(DateTimeOffset.MinValue, TimeZoneInfo.Utc));
        Assert.Equal(Utc(9999, 12, 31, 23, 59), expression.GetLastOccurrence(DateTimeOffset.MaxValue, TimeZoneInfo.Utc));
        Assert.Equal(Utc(1, 1, 1, 0, 1), expression.GetNextOccurrence(DateTimeOffset.MinValue, TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(-14)]
    [InlineData(14)]
    public void TimeZoneConversionDoesNotInventClippedDates(int offset)
    {
        var zone       = TimeZoneInfo.CreateCustomTimeZone("edge", TimeSpan.FromHours(offset), "edge", "edge");
        var expression = new CronExpression("* * * * *");
        Assert.Null(expression.GetLastOccurrence(DateTimeOffset.MinValue, zone));
        Assert.Null(expression.GetNextOccurrence(DateTimeOffset.MaxValue, zone));
        if (offset < 0)
        {
            Assert.False(expression.IsMatch(DateTimeOffset.MinValue, zone));
            Assert.Equal(new DateTimeOffset(1, 1, 1, 0, 0, 0, zone.BaseUtcOffset),
                expression.GetNextOccurrence(DateTimeOffset.MinValue, zone));
        }
        else
        {
            Assert.Equal(new DateTimeOffset(9999, 12, 31, 23, 59, 0, zone.BaseUtcOffset),
                expression.GetLastOccurrence(DateTimeOffset.MaxValue, zone));
        }
    }

    [Fact]
    public void ImpossibleDatesAreValidButHaveNoOccurrences()
    {
        var expression = new CronExpression("0 0 31 FEB *");
        Assert.Null(expression.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        Assert.Null(expression.GetLastOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        var leap = new CronExpression("0 0 29 FEB *");
        Assert.Equal(Utc(2104, 2, 29), leap.GetNextOccurrence(Utc(2099, 1, 1), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("en-US")]
    public void NamesAndNumbersAreCultureIndependent(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var expression = new CronExpression("0 12 * jan,feb,mar,apr,may,jun,jul,aug,sep,oct,nov,dec fri");
            Assert.Equal(Utc(2026, 6, 12, 12), expression.GetNextOccurrence(Utc(2026, 6, 11), TimeZoneInfo.Utc));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    internal static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
