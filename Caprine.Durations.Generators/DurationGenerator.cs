using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Caprine.Durations.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class DurationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            DurationDeclaration.AttributeName,
            static (node, _) => node is MethodDeclarationSyntax or PropertyDeclarationSyntax,
            static (attributeContext, ct) =>
            {
                var attribute = attributeContext.Attributes[0];
                var valid     = DurationDeclaration.TryCreate(attributeContext, ct, out var source, out var problem);
                var location  = valid ? null : attribute.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation();

                return (Source: valid ? (GeneratedSource?)source : null,
                        Problem: valid ? (DurationProblem?)null : problem, Location: location);
            });

        var sources = declarations.Where(static declaration => declaration.Source.HasValue).Select(static (declaration, _) => declaration.Source!.Value).WithTrackingName("DurationSource");

        // Report errors here because missing partial implementations can prevent analyzer execution.
        // Keep locations out of the successful source model so unrelated edits retain cached output.
        var diagnostics = declarations.Where(static declaration => declaration.Problem.HasValue);

        context.RegisterSourceOutput(diagnostics, static (productionContext, declaration) => productionContext.ReportDiagnostic(DurationDiagnostics.Create(declaration.Problem!.Value, declaration.Location)));
        context.RegisterSourceOutput(sources, static (productionContext,     source) => productionContext.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8)));
    }
}
