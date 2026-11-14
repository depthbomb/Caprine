using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Caprine.CronTimer.Generators;

/// <summary>Generates cached cron expressions for GeneratedCronExpression attributes.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class CronGenerator : IIncrementalGenerator
{
    /// <summary>Registers attribute discovery, validation, and source emission.</summary>
    /// <param name="context">The compiler's incremental generation context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            CronDeclaration.AttributeName,
            static (node, _) => node is MethodDeclarationSyntax or PropertyDeclarationSyntax,
            static (attributeContext, ct) =>
            {
                var valid    = CronDeclaration.TryCreate(attributeContext, ct, out var source, out var problem);
                var location = valid ? null : attributeContext.Attributes[0].ApplicationSyntaxReference?.GetSyntax(ct).GetLocation();
                return (Source: valid ? (GeneratedSource?)source : null,
                        Problem: valid ? (CronProblem?)null : problem, Location: location);
            });

        // Keep locations out of the successful source model so unrelated edits retain cached output.
        var sources = declarations.Where(static declaration => declaration.Source.HasValue)
                                  .Select(static (declaration, _) => declaration.Source!.Value)
                                  .WithTrackingName("CronSource");
        var diagnostics = declarations.Where(static declaration => declaration.Problem.HasValue);

        context.RegisterSourceOutput(diagnostics, static (production, declaration) => production.ReportDiagnostic(CronDiagnostics.Create(declaration.Problem!.Value, declaration.Location)));
        context.RegisterSourceOutput(sources, static (production,     source) => production.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8)));
    }
}
