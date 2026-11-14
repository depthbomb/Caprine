using Microsoft.CodeAnalysis.CSharp;

namespace Caprine.Durations.Tests;

public sealed partial class DurationGeneratorTests
{
    [Theory]
    [InlineData("TimeSpan Value();")]
    [InlineData("TimeSpan Value { get; }")]
    [InlineData("bool Value(DateTime start, out DateTime result);")]
    public void DuplicateDeclarationsDoNotDisableUnrelatedGeneration(string member)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       public static partial class Values
                       {
                           [GeneratedDuration("1h")]
                           public static partial {{member}}
                           [GeneratedDuration("2h")]
                           public static partial {{member}}
                           [GeneratedDuration("1s")]
                           public static partial TimeSpan Healthy();
                       }
                       """;
        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("CAPD001", diagnostic.Id));
        Assert.All(driver.GetRunResult().Results, result => Assert.Null(result.Exception));
        Assert.Contains("@Healthy", Assert.Single(driver.GetRunResult().GeneratedTrees).ToString());
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Id == "CS8795" && diagnostic.GetMessage().Contains("Healthy"));
    }

    [Fact]
    public void AddingAndRemovingDuplicateDeclarationsUpdatesCachedSources()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration("1s")]
                                  public static partial TimeSpan Value();
                                  [GeneratedDuration("2s")]
                                  public static partial TimeSpan Healthy();
                              }
                              """;
        const string duplicate = """
                                 using System;
                                 using Caprine.Durations;
                                 public static partial class Values
                                 {
                                     [GeneratedDuration("3s")]
                                     public static partial TimeSpan Value();
                                 }
                                 """;
        var compilation = CreateCompilation(source);
        var (driver, _, _) = Generate(compilation);
        var tree   = CSharpSyntaxTree.ParseText(duplicate, ParseOptions, "Duplicate.cs");
        var edited = compilation.AddSyntaxTrees(tree);
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out _, out var diagnostics);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("CAPD001", diagnostic.Id));
        Assert.Contains("@Healthy", Assert.Single(driver.GetRunResult().GeneratedTrees).ToString());

        driver = driver.RunGeneratorsAndUpdateCompilation(edited.RemoveSyntaxTrees(tree), out var output, out diagnostics);
        Assert.Empty(diagnostics);
        AssertNoCompilationErrors(output);
        Assert.Equal(2, driver.GetRunResult().GeneratedTrees.Length);
    }

    [Theory]
    [InlineData("TimeSpan")]
    [InlineData("CalendarDuration")]
    [InlineData("DateTime")]
    [InlineData("DateTimeOffset")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    public void SameSimpleTypeNameDoesNotMatchAnUnrelatedType(string type)
    {
        var source = $$"""
                       using Caprine.Durations;
                       namespace Other
                       {
                           public struct {{type}} {}
                           public static partial class Values
                           {
                               [GeneratedDuration("1h")]
                               public static partial {{type}} Value();
                           }
                       }
                       """;
        var (driver, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal("CAPD001", Assert.Single(diagnostics).Id);
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }
}
