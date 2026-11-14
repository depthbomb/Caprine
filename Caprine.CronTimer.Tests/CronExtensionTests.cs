using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class CronExtensionTests
{
    [Theory]
    [InlineData("5 * * * * *", CronExpressionOptions.IncludeSeconds, 2026, 1, 1, 0, 0, 5)]
    [InlineData("0 0 1 1 * 2030,2028", CronExpressionOptions.IncludeYear, 2028, 1, 1, 0, 0, 0)]
    [InlineData("5 0 0 1 1 * 2030,2028", CronExpressionOptions.IncludeSeconds | CronExpressionOptions.IncludeYear, 2028, 1, 1, 0, 0, 5)]
    public void FieldLayoutsAreExplicit(string text, CronExpressionOptions options, int year, int month, int day, int hour, int minute, int second)
    {
        var expression = new CronExpression(text, options);
        var expected   = Utc(year, month, day, hour, minute, second);
        Assert.Equal(expected, expression.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal(expected, expression.GetLastOccurrence(expected.AddTicks(1), TimeZoneInfo.Utc));
        Assert.True(expression.IsMatch(expected, TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("@daily", "0 0 * * *")]
    [InlineData("@midnight", "0 0 * * *")]
    [InlineData("@hourly", "0 * * * *")]
    [InlineData("@minutely", "* * * * *")]
    [InlineData("@weekly", "0 0 * * 0")]
    [InlineData("@monthly", "0 0 1 * *")]
    [InlineData("@yearly", "0 0 1 1 *")]
    [InlineData("@annually", "0 0 1 1 *")]
    public void NicknamesRequireAnOptionAndRespectFieldLayouts(string nickname, string expanded)
    {
        var anchor   = Utc(2026, 6, 12, 12, 34, 56);
        var expected = new CronExpression(expanded).GetNextOccurrence(anchor, TimeZoneInfo.Utc);
        foreach (var extra in new[]
                 {
                     CronExpressionOptions.None, CronExpressionOptions.IncludeSeconds,
                     CronExpressionOptions.IncludeYear, CronExpressionOptions.IncludeSeconds | CronExpressionOptions.IncludeYear,
                 })
        {
            var expression = new CronExpression(nickname.ToUpperInvariant(), extra | CronExpressionOptions.AllowNicknames);
            Assert.Equal(expected, expression.GetNextOccurrence(anchor, TimeZoneInfo.Utc));
        }
    }

    [Fact]
    public void SecondlyRequiresSecondsAndNicknameOptions()
    {
        Assert.Throws<FormatException>(() => new CronExpression("@secondly", CronExpressionOptions.AllowNicknames));
        Assert.Throws<FormatException>(() => new CronExpression("@secondly", CronExpressionOptions.IncludeSeconds));
        var expression = new CronExpression("@secondly", CronExpressionOptions.AllowNicknames | CronExpressionOptions.IncludeSeconds);
        Assert.Equal(Utc(2026, 1, 1, 0, 0, 1), expression.GetNextOccurrence(Utc(2026, 1, 1), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("L", "*", 2026, 6, 30)]
    [InlineData("L-0", "*", 2026, 6, 30)]
    [InlineData("L-3", "*", 2026, 6, 27)]
    [InlineData("L-3,L-1", "*", 2026, 6, 27)]
    [InlineData("LW", "?", 2026, 5, 29)]
    [InlineData("wl", "?", 2026, 5, 29)]
    [InlineData("15w", "?", 2026, 8, 14)]
    [InlineData("15W", "?", 2026, 11, 16)]
    [InlineData("1W", "*", 2026, 8, 3)]
    [InlineData("31W", "*", 2026, 5, 29)]
    [InlineData("?", "FRIL", 2026, 6, 26)]
    [InlineData("?", "5L", 2026, 6, 26)]
    [InlineData("?", "5#3", 2026, 6, 19)]
    [InlineData("*", "L", 2026, 6, 27)]
    [InlineData("*", "7L", 2026, 6, 28)]
    public void CalendarModifiersHaveExplicitSemantics(string monthDay, string weekday, int year, int month, int day)
    {
        var expression = new CronExpression($"0 12 {monthDay} * {weekday}", CronExpressionOptions.AllowCalendarModifiers);
        var expected   = Utc(year, month, day, 12);
        Assert.Equal(expected, expression.GetNextOccurrence(Utc(year, month, 1), TimeZoneInfo.Utc));
        Assert.Equal(expected, expression.GetLastOccurrence(expected.AddMinutes(1), TimeZoneInfo.Utc));
        Assert.True(expression.IsMatch(expected, TimeZoneInfo.Utc));
    }

    [Fact]
    public void MultipleLastDayOffsetsAreAllRetained()
    {
        var expression = new CronExpression("0 0 L-3,L-1 * *", CronExpressionOptions.AllowCalendarModifiers);
        Assert.Equal(Utc(2026, 6, 27), expression.GetNextOccurrence(Utc(2026, 6, 1), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 6, 29), expression.GetNextOccurrence(Utc(2026, 6, 27), TimeZoneInfo.Utc));
    }

    [Fact]
    public void NearestWeekdayDoesNotClampNonexistentMonthDays()
    {
        var expression = new CronExpression("0 0 31W * *", CronExpressionOptions.AllowCalendarModifiers);
        Assert.Equal(Utc(2026, 3, 31), expression.GetNextOccurrence(Utc(2026, 2, 1), TimeZoneInfo.Utc));
        var fifth = new CronExpression("0 0 * * MON#5", CronExpressionOptions.AllowCalendarModifiers);
        Assert.Equal(Utc(2026, 3, 30), fifth.GetNextOccurrence(Utc(2026, 2, 1), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("0 0 L-31 * *")]
    [InlineData("0 0 0W * *")]
    [InlineData("0 0 * * MON#0")]
    [InlineData("0 0 * * MON#6")]
    [InlineData("0 0 * * 8L")]
    [InlineData("0 0 * * 1#2#3")]
    [InlineData("0 0 * * *,8L")]
    public void InvalidModifiersStillFail(string text)
    {
        Assert.False(CronExpression.TryParse(text, out _, CronExpressionOptions.AllowCalendarModifiers));
    }

    [Fact]
    public void YearListsAreChronologicalAndBounded()
    {
        var expression = new CronExpression("0 0 1 1 * 2030,2028", CronExpressionOptions.IncludeYear);
        Assert.Equal(Utc(2028, 1, 1), expression.GetNextOccurrence(Utc(2027, 1, 1), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2030, 1, 1), expression.GetLastOccurrence(Utc(2031, 1, 1), TimeZoneInfo.Utc));
        Assert.Null(expression.GetNextOccurrence(Utc(2030, 1, 1), TimeZoneInfo.Utc));
        Assert.Null(expression.GetLastOccurrence(Utc(2028, 1, 1), TimeZoneInfo.Utc));
        Assert.Throws<FormatException>(() => new CronExpression("0 0 * * * 10000", CronExpressionOptions.IncludeYear));
        Assert.Throws<FormatException>(() => new CronExpression("0 0 * * * 0", CronExpressionOptions.IncludeYear));
    }
}
