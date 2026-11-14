using Caprine.Durations.Generators;
using GeneratorOptions = Caprine.Durations.Generators.DurationParseOptions;

namespace Caprine.Durations.Tests;

public sealed class DurationParserPrecisionTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(100)]
    [InlineData(1_000)]
    public void LongQuantitiesAndCancellationRemainExact(int digits)
    {
        var value      = new string('9', digits);
        var expression = value + "ticks -" + value + "ticks 1tick";
        Assert.True(DurationParser.TryParse(expression, default, out var result, out var problem, GeneratorOptions.Strict), problem.Message);
        Assert.Equal(1, result.FixedTicks);
        expression = value + "y -" + value + "y 1y";
        Assert.True(DurationParser.TryParse(expression, default, out result, out problem, GeneratorOptions.Strict), problem.Message);
        Assert.Equal(1, result.Years);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(100)]
    [InlineData(1_000)]
    public void DecimalChunkBoundariesKeepTrailingZeroesAndPrecision(int zeroes)
    {
        var suffix = new string('0', zeroes);
        Assert.True(DurationParser.TryParse("-0.1" + suffix + "us", default, out var result, out var problem), problem.Message);
        Assert.Equal(-1, result.FixedTicks);
        Assert.True(DurationParser.TryParse(suffix + "1.0mo", default, out result, out problem), problem.Message);
        Assert.Equal(1, result.Months);
        Assert.False(DurationParser.TryParse("0." + suffix + "1s", default, out _, out problem));
        Assert.Equal("CAPD004", problem.Id);
    }

    [Theory]
    [InlineData("1\u00b5s 1\u03bcs 1\u039cs", 30)]
    [InlineData("1h\u0301 2ticks", 2)]
    [InlineData("1ms\U00010400 3ticks", 3)]
    [InlineData("\ud800 4ticks \udfff", 4)]
    public void NonAsciiUnitsAndMalformedSurrogatesKeepTheirBoundaries(string expression, long ticks)
    {
        Assert.True(DurationParser.TryParse(expression, default, out var result, out var problem), problem.Message);
        Assert.Equal(ticks, result.FixedTicks);
    }
}
