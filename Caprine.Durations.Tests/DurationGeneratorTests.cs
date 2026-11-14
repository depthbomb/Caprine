using Caprine.Durations.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Caprine.Durations.Tests;

public sealed partial class DurationGeneratorTests
{
    private static readonly CSharpParseOptions                ParseOptions = new(LanguageVersion.CSharp15);
    private static readonly ImmutableArray<MetadataReference> References   = CreateReferences();

    [Fact]
    public void EmitsCompilableImplementationsForConstantsAndNestedGenericRecords()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              namespace @event;
                              public partial record Outer<T> where T : class
                              {
                                  public readonly partial record struct Inner<U> where U : unmanaged
                                  {
                                      private const string Text = "1w " + "4d 4h 100ms";
                                      [GeneratedDuration(Text)]
                                      public static partial TimeSpan @return();
                                      [GeneratedDuration("1y 2mo 3us")]
                                      internal static partial CalendarDuration Period();
                                  }
                              }
                              """;

        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertNoCompilationErrors(output);
        var generated = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(2, generated.Length);
        Assert.Contains(generated, item => item.SourceText.ToString().Contains("9648001000000L"));
        Assert.Contains(generated, item => item.SourceText.ToString().Contains("CalendarDuration(1, 2, 30L)"));
        Assert.All(generated, item => Assert.DoesNotContain("Regex", item.SourceText.ToString()));
    }

    [Fact]
    public void DifferentContainersWithTheSameMethodNameGetDistinctFiles()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class First
                              {
                                  [GeneratedDuration("1h")]
                                  public static partial TimeSpan Value();
                              }
                              public static partial class Second
                              {
                                  [GeneratedDuration("2h")]
                                  public static partial TimeSpan Value();
                              }
                              """;
        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertNoCompilationErrors(output);
        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(2, sources.Select(item => item.HintName).Distinct().Count());
    }

    [Theory]
    [InlineData("1.5mo", "TimeSpan", "CAPD003")]
    [InlineData("0.01us", "TimeSpan", "CAPD004")]
    [InlineData("922337203685477580.8us", "TimeSpan", "CAPD005")]
    [InlineData("1mo", "TimeSpan", "CAPD007")]
    [InlineData("1h", "int", "CAPD001")]
    public void ReportsSemanticErrorsAtTheAttribute(string expression, string returnType, string expectedId)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       public static partial class Values
                       {
                           [GeneratedDuration("{{expression}}")]
                           public static partial {{returnType}} Value();
                       }
                       """;

        var (_, _, diagnostics) = Generate(CreateCompilation(source));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("GeneratedDuration", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Theory]
    [InlineData("public static TimeSpan Value() => default;", "public static partial class Values")]
    [InlineData("public static partial TimeSpan Value(int number);", "public static partial class Values")]
    [InlineData("public static partial TimeSpan Value<T>();", "public static partial class Values")]
    [InlineData("public partial TimeSpan Value();", "public partial class Values")]
    [InlineData("public static partial TimeSpan Value();", "public static class Values")]
    [InlineData("public static partial TimeSpan Value();", "file static partial class Values")]
    public void RejectsUnsupportedDeclarations(string member, string container)
    {
        var source = $$"""
                       using System;
                       using Caprine.Durations;
                       {{container}}
                       {
                           [GeneratedDuration("1h")]
                           {{member}}
                       }
                       """;

        var compilation = CreateCompilation(source);
        var (_, _, diagnostics) = Generate(compilation);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("CAPD001", diagnostic.Id);
    }

    [Fact]
    public void RejectsNullAndUserProvidedPartialImplementations()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration(null)]
                                  public static partial TimeSpan Missing();
                                  [GeneratedDuration("1h")]
                                  public static partial TimeSpan Existing();
                                  public static partial TimeSpan Existing() => default;
                              }
                              """;
        var (_, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal(["CAPD001", "CAPD002"], diagnostics.Select(item => item.Id).Order().ToArray());
    }

    [Fact]
    public void UnrelatedEditsKeepGeneratedOutputsUnchanged()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration("1h")]
                                  public static partial TimeSpan Value();
                              }
                              """;
        var compilation = CreateCompilation(source);
        var (driver, _, _) = Generate(compilation);
        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated {}", ParseOptions));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _);
        AssertNoCompilationErrors(output);

        var steps = driver.GetRunResult().Results.Single().TrackedSteps["DurationSource"];
        Assert.All(steps.SelectMany(step => step.Outputs), result =>
            Assert.True(result.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
    }

    [Fact]
    public void EditingALiteralUpdatesItsGeneratedValue()
    {
        const string source = """
                              using System;
                              using Caprine.Durations;
                              public static partial class Values
                              {
                                  [GeneratedDuration("1us")]
                                  public static partial TimeSpan Value();
                              }
                              """;
        var compilation = CreateCompilation(source);
        var (driver, _, _) = Generate(compilation);
        var replacement = CSharpSyntaxTree.ParseText(source.Replace("1us", "2us"), ParseOptions, "Input.cs");
        var edited      = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), replacement);
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _);
        AssertNoCompilationErrors(output);
        Assert.Contains("TimeSpan(20L)", driver.GetRunResult().GeneratedTrees.Single().ToString());
    }

    private static (GeneratorDriver Driver, Compilation Output, ImmutableArray<Diagnostic> Diagnostics) Generate(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DurationGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(default, true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        return (driver, output, diagnostics);
    }

    private static CSharpCompilation CreateCompilation(
        #if NET11_0_OR_GREATER
        [StringSyntax(StringSyntaxAttribute.CSharp)]
        #else
        [StringSyntax("C#")]
        #endif
        string source) => CSharpCompilation.Create(
        "GeneratorConsumer",
        [CSharpSyntaxTree.ParseText(source, ParseOptions, "Input.cs")],
        References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);

        return paths.Append(typeof(CalendarDuration).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => MetadataReference.CreateFromFile(path)).ToImmutableArray<MetadataReference>();
    }

    private static void AssertNoCompilationErrors(Compilation compilation) =>
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
}
