using Caprine.CronTimer.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Caprine.CronTimer.Tests;

public sealed class CronGeneratorTests
{
    private static readonly CSharpParseOptions                ParseOptions = new(LanguageVersion.CSharp15);
    private static readonly ImmutableArray<MetadataReference> References   = CreateReferences();

    [Fact]
    public void EmitsMethodsAndPropertiesInNestedGenericRecords()
    {
        const string source = """
                              using Caprine.CronTimer;
                              namespace @event;
                              public partial record Outer<T> where T : class
                              {
                                  public readonly partial record struct Inner<U> where U : unmanaged
                                  {
                                      private const string Pattern = "*/5 " + "* * * *";
                                      [GeneratedCronExpression(Pattern)]
                                      public static partial CronExpression @return();
                                      [GeneratedCronExpression("@daily", CronExpressionOptions.AllowNicknames)]
                                      internal static partial CronExpression Daily { get; }
                                  }
                              }
                              """;
        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertCompiles(output);
        var generated = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(2, generated.Length);
        Assert.All(generated, item =>
        {
            Assert.Contains("CronExpression.FromGenerated(", item.SourceText.ToString());
            Assert.Contains("static readonly", item.SourceText.ToString());
            Assert.DoesNotContain("new global::Caprine.CronTimer.CronExpression(", item.SourceText.ToString());
        });
    }

    [Theory]
    [InlineData("public static partial class Values")]
    [InlineData("public partial struct Values")]
    [InlineData("public ref partial struct Values")]
    [InlineData("public partial record Values")]
    public void SupportsDifferentContainingTypeKinds(string container)
    {
        var source = $$"""
                       using Caprine.CronTimer;
                       {{container}}
                       {
                           [GeneratedCronExpression("* * * * *")]
                           public static partial CronExpression? Value { get; }
                       }
                       """;
        var (_, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertCompiles(output);
    }

    [Theory]
    [InlineData("", "CronExpressionOptions.None", "CAPC002")]
    [InlineData("0/15 * * * *", "CronExpressionOptions.None", "CAPC002")]
    [InlineData("* * * * * *", "CronExpressionOptions.None", "CAPC002")]
    [InlineData("@daily", "CronExpressionOptions.None", "CAPC002")]
    [InlineData("0 0 *,99 * *", "CronExpressionOptions.None", "CAPC002")]
    [InlineData("* * * * *", "(CronExpressionOptions)16", "CAPC003")]
    [InlineData("* * * * *", "(CronExpressionOptions)(-1)", "CAPC003")]
    public void ReportsExpressionAndOptionDiagnostics(string text, string options, string expected)
    {
        var source = $$"""
                       using Caprine.CronTimer;
                       public static partial class Values
                       {
                           [GeneratedCronExpression("{{text}}", {{options}})]
                           public static partial CronExpression Value();
                       }
                       """;
        var (driver, _, diagnostics) = Generate(CreateCompilation(source));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expected, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("GeneratedCronExpression", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
    }

    [Theory]
    [InlineData("public static CronExpression Value() => null!;", "public partial class Values")]
    [InlineData("public partial CronExpression Value();", "public partial class Values")]
    [InlineData("public static partial CronExpression Value(int x);", "public partial class Values")]
    [InlineData("public static partial CronExpression Value<T>();", "public partial class Values")]
    [InlineData("public static partial ref CronExpression Value();", "public partial class Values")]
    [InlineData("public static partial int Value();", "public partial class Values")]
    [InlineData("public static partial CronExpression Value { get; set; }", "public partial class Values")]
    [InlineData("public partial CronExpression Value { get; }", "public partial class Values")]
    [InlineData("public static partial CronExpression Value();", "public class Values")]
    [InlineData("public static partial CronExpression Value();", "file partial class Values")]
    [InlineData("public static partial CronExpression Value();", "public partial interface Values")]
    public void RejectsUnsupportedDeclarations(string member, string container)
    {
        var source = $$"""
                       using Caprine.CronTimer;
                       {{container}}
                       {
                           [GeneratedCronExpression("* * * * *")]
                           {{member}}
                       }
                       """;
        var (_, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal("CAPC001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void RejectsNullAndExistingImplementations()
    {
        const string source = """
                              using Caprine.CronTimer;
                              public partial class Values
                              {
                                  [GeneratedCronExpression(null)]
                                  public static partial CronExpression Missing();
                                  [GeneratedCronExpression("* * * * *")]
                                  public static partial CronExpression Existing();
                                  public static partial CronExpression Existing() => null!;
                              }
                              """;
        var (_, _, diagnostics) = Generate(CreateCompilation(source));
        Assert.Equal(["CAPC001", "CAPC002"], diagnostics.Select(item => item.Id).Order().ToArray());
    }

    [Fact]
    public void DistinctContainersAndGenericAritiesHaveDistinctGeneratedNames()
    {
        const string source = """
                              using Caprine.CronTimer;
                              public partial class Values
                              {
                                  [GeneratedCronExpression("* * * * *")]
                                  public static partial CronExpression Value();
                              }
                              public partial class Values<T>
                              {
                                  [GeneratedCronExpression("0 * * * *")]
                                  public static partial CronExpression Value();
                              }
                              """;
        var (driver, output, diagnostics) = Generate(CreateCompilation(source));
        Assert.Empty(diagnostics);
        AssertCompiles(output);
        Assert.Equal(2, driver.GetRunResult().Results.Single().GeneratedSources.Select(item => item.HintName).Distinct().Count());
    }

    [Fact]
    public void UnrelatedEditsRetainCachedOutputsAndLiteralEditsUpdateThem()
    {
        const string source = """
                              using Caprine.CronTimer;
                              public partial class Values
                              {
                                  [GeneratedCronExpression("*/5 * * * *")]
                                  public static partial CronExpression Value();
                              }
                              """;
        var compilation = CreateCompilation(source);
        var (driver, _, _) = Generate(compilation);
        var original = driver.GetRunResult().GeneratedTrees.Single().ToString();
        var edited   = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated {}", ParseOptions));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _);
        AssertCompiles(output);
        Assert.All(driver.GetRunResult().Results.Single().TrackedSteps["CronSource"].SelectMany(step => step.Outputs),
            value => Assert.True(value.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
        var replaced = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(source.Replace("*/5", "*/7"), ParseOptions, "Input.cs"));
        driver = driver.RunGeneratorsAndUpdateCompilation(replaced, out output, out _);
        AssertCompiles(output);
        Assert.NotEqual(original, driver.GetRunResult().GeneratedTrees.Single().ToString());
    }

    private static (GeneratorDriver Driver, Compilation Output, ImmutableArray<Diagnostic> Diagnostics) Generate(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CronGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions, driverOptions: new GeneratorDriverOptions(default, true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return (driver, output, diagnostics);
    }

    private static CSharpCompilation CreateCompilation(string source) => CSharpCompilation.Create(
        "CronGeneratorConsumer", [CSharpSyntaxTree.ParseText(source, ParseOptions, "Input.cs")],
        References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trusted.Append(typeof(CronExpression).Assembly.Location).Distinct()
                      .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
    }

    private static void AssertCompiles(Compilation output)
    {
        var errors = output.GetDiagnostics().Where(value => value.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(value => value.ToString())));
    }
}
