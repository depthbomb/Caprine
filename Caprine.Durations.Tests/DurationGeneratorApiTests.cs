using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Caprine.Durations.Tests;

public sealed partial class DurationGeneratorTests
{
    [Fact]
    public void EmitsPropertiesAndOverloadsInNestedGenericTypesWithEscapedIdentifiers()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              using Stamp = System.DateTimeOffset;
                              namespace @event.@class;
                              public partial record Outer<T> where T : class
                              {
                                  public readonly partial record struct Inner<U> where U : unmanaged
                                  {
                                      [GeneratedDuration("1tick", DurationParseOptions.Strict)]
                                      public static partial TimeSpan @return { get; }
                                      [GeneratedDuration("1y 1mo")]
                                      public static partial CalendarDuration Period { get; }
                                      [GeneratedDuration("1h")]
                                      public static partial TimeSpan Value();
                                      [GeneratedDuration("1h")]
                                      public static partial DateTime Value(DateTime @event);
                                      [GeneratedDuration("1h")]
                                      public static partial Stamp Value(Stamp @event);
                                      [GeneratedDuration("1h")]
                                      public static partial bool Value(DateTime @event, out DateTime @out);
                                      [GeneratedDuration("1h")]
                                      public static partial bool Value(DateTime @event, scoped out TimeSpan @out);
                                      [GeneratedDuration("1h")]
                                      public static partial TimeOnly Value(TimeOnly @event, out int @out);
                                  }
                              }
                              """;
        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertNoCompilationErrors(output);
        var generated = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(8, generated.Length);
        Assert.Equal(8, generated.Select(item => item.HintName).Distinct().Count());

        var renamed = source.Replace("@event);", "@start);").Replace("@event,", "@start,");
        var (renamedDriver, renamedOutput, renamedDiagnostics) = Generate(CreateCompilation(renamed));
        Assert.Empty(renamedDiagnostics);
        AssertNoCompilationErrors(renamedOutput);
        Assert.Equal(generated.Select(item => item.HintName).Order(), renamedDriver.GetRunResult().Results.Single().GeneratedSources.Select(item => item.HintName).Order());
    }

    [Theory]
    [InlineData("public static partial TimeSpan Value { get; set; }")]
    [InlineData("public static TimeSpan Value { get; }")]
    [InlineData("public partial TimeSpan Value { get; }")]
    [InlineData("public static partial DateTime Value { get; }")]
    [InlineData("public static partial TimeSpan? Value { get; }")]
    [InlineData("public static partial DateTime Value();")]
    [InlineData("public static partial CalendarDuration Value(DateTime start);")]
    [InlineData("public static partial DateTime Value(DateTime? start);")]
    [InlineData("public static partial DateTime Value(ref DateTime start);")]
    [InlineData("public static partial DateTime Value(in DateTime start);")]
    [InlineData("public static partial DateTime Value(out DateTime start);")]
    [InlineData("public static partial DateTime Value(DateTime start = default);")]
    [InlineData("public static partial DateTime Value(this DateTime start);")]
    [InlineData("public static partial bool Value(DateTime start);")]
    [InlineData("public static partial bool Value(DateTime start, DateTime result);")]
    [InlineData("public static partial bool Value(DateTime start, ref DateTime result);")]
    [InlineData("public static partial bool Value(DateTime start, out DateTimeOffset result);")]
    [InlineData("public static partial bool Value(DateTime start, out DateTime result, out int days);")]
    [InlineData("public static partial bool Value(TimeOnly start, out int days, out TimeOnly result);")]
    [InlineData("public static partial TimeOnly Value(TimeOnly start, out long days);")]
    [InlineData("public static partial TimeOnly Value(TimeOnly start, int days);")]
    [InlineData("public static partial DateOnly Value(DateOnly start, out int days);")]
    [InlineData("public static partial TimeSpan Value(DateOnly start);")]
    [InlineData("public static partial TimeSpan Value(TimeOnly start);")]
    [InlineData("public static partial DateOnly Value(DateTime start);")]
    public void RejectsUnsupportedPropertyAndOperationSignatures(string member)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       public partial class Values
                       {
                           [GeneratedDuration("1d")]
                           {{member}}
                       }
                       """;
        var (driver, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal("CAPD001", Assert.Single(diagnostics).Id);
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    [Theory]
    [InlineData("1mo", "TimeSpan Value { get; }", "CAPD007")]
    [InlineData("1mo", "TimeOnly Value(TimeOnly start);", "CAPD007")]
    [InlineData("1mo", "bool Value(TimeOnly start, out TimeOnly result);", "CAPD007")]
    [InlineData("1y", "bool Value(TimeOnly start, out TimeOnly result, out int days);", "CAPD007")]
    [InlineData("1tick", "DateOnly Value(DateOnly start);", "CAPD010")]
    [InlineData("-1tick", "bool Value(DateOnly start, out DateOnly result);", "CAPD010")]
    public void RejectsLiteralsThatCannotBeAppliedToTheTarget(string expression, string member, string expectedId)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       public static partial class Values
                       {
                           [GeneratedDuration("{{expression}}")]
                           public static partial {{member}}
                       }
                       """;
        var (driver, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal(expectedId, Assert.Single(diagnostics).Id);
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    [Theory]
    [InlineData("5mss", "DurationParseOptions.Strict", "CAPD008")]
    [InlineData("1h 5mss", "DurationParseOptions.Strict", "CAPD008")]
    [InlineData("1e3h", "DurationParseOptions.Strict", "CAPD008")]
    [InlineData("", "DurationParseOptions.Strict", "CAPD008")]
    [InlineData("1s", "(DurationParseOptions)2", "CAPD009")]
    [InlineData("1s", "(DurationParseOptions)(-1)", "CAPD009")]
    [InlineData("0.5tick", "DurationParseOptions.Strict", "CAPD004")]
    public void ReportsStrictParsingAndOptionDiagnostics(string expression, string options, string expectedId)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       public static partial class Values
                       {
                           [GeneratedDuration({{SymbolDisplay.FormatLiteral(expression, true)}}, {{options}})]
                           public static partial TimeSpan Value { get; }
                       }
                       """;
        var (_, _, diagnostics) = Generate(CreateCompilation(source));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("GeneratedDuration", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public void RejectsUserProvidedPropertyImplementationsAndDuplicateAttributes()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration("1s")]
                                  public static partial TimeSpan Existing { get; }
                                  public static partial TimeSpan Existing => default;
                                  [GeneratedDuration("1s"), GeneratedDuration("2s")]
                                  public static partial TimeSpan Duplicate { get; }
                              }
                              """;
        var (driver, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, item => Assert.Equal("CAPD001", item.Id));
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    [Fact]
    public void NewMemberShapesRetainIncrementalCachingAndReactToOptionChanges()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration("1tick trailing", DurationParseOptions.None)]
                                  public static partial TimeSpan Value { get; }
                                  [GeneratedDuration("1mo")]
                                  public static partial DateTime Add(DateTime start);
                              }
                              """;
        var compilation = CreateCompilation(source);
        var (driver, _, diagnostics) = Generate(compilation);
        Assert.Empty(diagnostics);
        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated {}", ParseOptions));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out diagnostics);
        Assert.Empty(diagnostics);
        AssertNoCompilationErrors(output);
        var steps = driver.GetRunResult().Results.Single().TrackedSteps["DurationSource"];
        Assert.All(steps.SelectMany(step => step.Outputs), result =>
            Assert.True(result.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));

        var replacement = CSharpSyntaxTree.ParseText(source.Replace("DurationParseOptions.None", "DurationParseOptions.Strict"), ParseOptions, "Input.cs");
        edited = edited.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), replacement);
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out _, out diagnostics);
        Assert.Equal("CAPD008", Assert.Single(diagnostics).Id);
        Assert.Single(driver.GetRunResult().GeneratedTrees);
    }
}
