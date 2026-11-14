using Caprine.Durations.Generators;
using System.Globalization;

namespace Caprine.Durations.Tests;

public sealed class DurationParserTests
{
    [Theory]
    [InlineData("1w 4 days 4 hr and 100ms", 0, 0, 9_648_001_000_000L)]
    [InlineData("1h30m", 0, 0, 54_000_000_000L)]
    [InlineData("noise +1.5h and -30 min!", 0, 0, 36_000_000_000L)]
    [InlineData("1us 1µs 1μs 1microsecond 1microseconds", 0, 0, 50L)]
    [InlineData("0.1us", 0, 0, 1L)]
    [InlineData("-.5 milliseconds", 0, 0, -5_000L)]
    [InlineData("1m 1mo", 0, 1, 600_000_000L)]
    [InlineData("1year -2months +3days", 1, -2, 2_592_000_000_000L)]
    [InlineData("1monthology 2h", 0, 0, 72_000_000_000L)]
    [InlineData("100msx 2s", 0, 0, 20_000_000L)]
    [InlineData("1 rubbish h", 0, 0, 0L)]
    [InlineData("unrecognized prose 42 8fortnights", 0, 0, 0L)]
    [InlineData("1ns 5 nanoseconds", 0, 0, 0L)]
    [InlineData("1mo -1mo 2yr -2yrs", 0, 0, 0L)]
    [InlineData("1.0month", 0, 1, 0L)]
    [InlineData("1..5h 1e3h 1,000h 1_000h --2h", 0, 0, 0L)]
    [InlineData("١2h", 0, 0, 0L)]
    [InlineData("1h\u0301 1ms\U00010400", 0, 0, 0L)]
    [InlineData("1\tH\n30\r\nM", 0, 0, 54_000_000_000L)]
    [InlineData("922337203685477580.7us", 0, 0, long.MaxValue)]
    [InlineData("-922337203685477580.8us", 0, 0, long.MinValue)]
    [InlineData("9223372036854775808us -9223372036854775808us 1us", 0, 0, 10L)]
    [InlineData("", 0, 0, 0L)]
    public void ExtractsOnlyCompleteSupportedPairs(string text, int years, int months, long ticks)
    {
        Assert.True(DurationParser.TryParse(text, default, out var value, out var problem), problem.Message);
        Assert.Equal(years, value.Years);
        Assert.Equal(months, value.Months);
        Assert.Equal(ticks, value.FixedTicks);
    }

    [Theory]
    [InlineData("1.5 months", "CAPD003")]
    [InlineData("-.5y", "CAPD003")]
    [InlineData("0.01us", "CAPD004")]
    [InlineData("0.05us 0.05us", "CAPD004")]
    [InlineData("922337203685477580.8us", "CAPD005")]
    [InlineData("-922337203685477580.9us", "CAPD005")]
    [InlineData("10001y", "CAPD005")]
    [InlineData("120001mo", "CAPD005")]
    [InlineData(null, "CAPD002")]
    public void ReportsRecognizedButUnrepresentableValues(string? text, string diagnosticId)
    {
        Assert.False(DurationParser.TryParse(text, default, out _, out var problem));
        Assert.Equal(diagnosticId, problem.Id);
    }

    [Fact]
    public void BoundsCompileTimeWorkAndHonorsCancellation()
    {
        Assert.False(DurationParser.TryParse(new string('a', 32_769), default, out _, out var length));
        Assert.Equal("CAPD006", length.Id);
        Assert.False(DurationParser.TryParse(new string('9', 1_025) + "s", default, out _, out var digits));
        Assert.Equal("CAPD006", digits.Id);
        Assert.Throws<OperationCanceledException>(() => DurationParser.TryParse("1h", new CancellationToken(true), out _, out _));
    }

    [Fact]
    public void MatchesAliasesIndependentlyOfCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(DurationParser.TryParse("1 MINUTE 1 MILLISECOND 1 ΜS", default, out var value, out _));
            Assert.Equal(600_010_010L, value.FixedTicks);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("ms msec msecs millisecond milliseconds", 10_000L)]
    [InlineData("s sec secs second seconds", 10_000_000L)]
    [InlineData("m min mins minute minutes", 600_000_000L)]
    [InlineData("h hr hrs hour hours", 36_000_000_000L)]
    [InlineData("d day days", 864_000_000_000L)]
    [InlineData("w wk wks week weeks", 6_048_000_000_000L)]
    public void SupportsEachFixedUnitAlias(string aliases, long ticks)
    {
        foreach (var alias in aliases.Split(' '))
        {
            Assert.True(DurationParser.TryParse("1" + alias, default, out var value, out _));
            Assert.Equal(ticks, value.FixedTicks);
        }
    }
}
