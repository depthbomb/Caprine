using Caprine.Durations.Generators;
using GeneratorOptions = Caprine.Durations.Generators.DurationParseOptions;

namespace Caprine.Durations.Tests;

public sealed class DurationStrictParserTests
{
    [Theory]
    [InlineData("1tick", 1L)]
    [InlineData("2 TICKS", 2L)]
    [InlineData("-1tick +2ticks", 1L)]
    [InlineData("0s", 0L)]
    [InlineData("1tick -1tick", 0L)]
    [InlineData("0.1us", 1L)]
    [InlineData("1.0tick", 1L)]
    [InlineData("1h30m", 54_000_000_000L)]
    [InlineData(" 1h\t30m\r\n", 54_000_000_000L)]
    [InlineData("1h,30m", 54_000_000_000L)]
    [InlineData("1h and 30m", 54_000_000_000L)]
    [InlineData("1h, AND 30m", 54_000_000_000L)]
    [InlineData("1h,and 30m", 54_000_000_000L)]
    [InlineData("9223372036854775807ticks", long.MaxValue)]
    [InlineData("-9223372036854775808ticks", long.MinValue)]
    public void StrictModeAcceptsCompleteExpressions(string expression, long ticks)
    {
        Assert.True(DurationParser.TryParse(expression, default, out var value, out var problem, GeneratorOptions.Strict), problem.Message);
        Assert.Equal(ticks, value.FixedTicks);
        Assert.Equal(0, value.Years);
        Assert.Equal(0, value.Months);
    }

    [Fact]
    public void StrictModePreservesCalendarComponents()
    {
        Assert.True(DurationParser.TryParse("1y, -2mo and 3ticks", default, out var value, out _, GeneratorOptions.Strict));
        Assert.Equal(1, value.Years);
        Assert.Equal(-2, value.Months);
        Assert.Equal(3, value.FixedTicks);
        Assert.True(DurationParser.TryParse("1mo -1mo", default, out value, out _, GeneratorOptions.Strict));
        Assert.False(value.HasCalendarComponents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("noise")]
    [InlineData("noise 1h")]
    [InlineData("1h trailing")]
    [InlineData("5mss")]
    [InlineData("1h 5mss")]
    [InlineData("1ns")]
    [InlineData("1monthology")]
    [InlineData("1h 30")]
    [InlineData("1..5h")]
    [InlineData("1e3h")]
    [InlineData("1,000h")]
    [InlineData("1_000h")]
    [InlineData("--2h")]
    [InlineData("+h")]
    [InlineData("1.h")]
    [InlineData("\u06612h")]
    [InlineData("1h\u0301")]
    [InlineData("1ms\U00010400")]
    [InlineData(",1h")]
    [InlineData("and 1h")]
    [InlineData("1h,")]
    [InlineData("1h and ")]
    [InlineData("1h, and")]
    [InlineData("1h,,2h")]
    [InlineData("1h and and 2h")]
    [InlineData("1h and2h")]
    [InlineData("1h;2h")]
    [InlineData("1h!")]
    public void StrictModeRejectsUnconsumedOrMalformedInput(string expression)
    {
        Assert.False(DurationParser.TryParse(expression, default, out _, out var problem, GeneratorOptions.Strict));
        Assert.Equal("CAPD008", problem.Id);
    }

    [Theory]
    [InlineData("0.5tick", "CAPD004")]
    [InlineData("1.5mo", "CAPD003")]
    [InlineData("9223372036854775808ticks", "CAPD005")]
    [InlineData("-9223372036854775809ticks", "CAPD005")]
    [InlineData("10001y", "CAPD005")]
    [InlineData(null, "CAPD002")]
    public void StrictModeKeepsPrecisionAndRangeDiagnostics(string? expression, string expectedId)
    {
        Assert.False(DurationParser.TryParse(expression, default, out _, out var problem, GeneratorOptions.Strict));
        Assert.Equal(expectedId, problem.Id);
    }

    [Fact]
    public void StrictModeRejectsUnknownOptionsAndBoundsCompileTimeWork()
    {
        Assert.False(DurationParser.TryParse("1s", default, out _, out var options, (GeneratorOptions)2));
        Assert.Equal("CAPD009", options.Id);
        Assert.False(DurationParser.TryParse(new string(' ', 32_769), default, out _, out var length, GeneratorOptions.Strict));
        Assert.Equal("CAPD006", length.Id);
        Assert.False(DurationParser.TryParse(new string('9', 1_025) + "ticks", default, out _, out var digits, GeneratorOptions.Strict));
        Assert.Equal("CAPD006", digits.Id);
        Assert.Throws<OperationCanceledException>(() => DurationParser.TryParse("1tick", new CancellationToken(true), out _, out _, GeneratorOptions.Strict));
    }

    [Fact]
    public void PermissiveModeStillExtractsValuesAndRecognizesTicks()
    {
        Assert.True(DurationParser.TryParse("noise 5mss 1tick and 2ticks --2h", default, out var value, out _));
        Assert.Equal(3, value.FixedTicks);
        Assert.True(DurationParser.TryParse("5mss", default, out value, out _));
        Assert.Equal(0, value.FixedTicks);
        Assert.False(DurationParser.TryParse("0.5tick", default, out _, out var problem));
        Assert.Equal("CAPD004", problem.Id);
    }
}
