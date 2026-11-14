using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;

namespace Caprine.Durations.Generators;

internal static class DurationDeclaration
{
    internal const string AttributeName = "Caprine.Durations.GeneratedDurationAttribute";

    internal static bool TryCreate(GeneratorAttributeSyntaxContext context,
                                   CancellationToken               ct,
                                   out GeneratedSource             source,
                                   out DurationProblem             problem)
    {
        source  = default;
        problem = default;

        ct.ThrowIfCancellationRequested();

        var member = context.TargetSymbol;
        if (context.Attributes.Length != 1 ||
            !DurationSignature.TryCreate(member, context.SemanticModel.Compilation, out var signature))
        {
            problem = new DurationProblem("CAPD001", "Use a static get-only partial duration property or a non-generic static partial method returning a duration, an anchored date/time, or bool with an out result. Leave the implementation to the generator.");
            return false;
        }

        var identity = member.GetDocumentationCommentId()!;
        var siblings = member.ContainingType.GetMembers(member.Name);
        if (siblings.Length > 1 && siblings.Any(candidate =>
                !SymbolEqualityComparer.Default.Equals(candidate, member) && candidate.GetDocumentationCommentId() == identity))
        {
            problem = new DurationProblem("CAPD001", "A generated duration member must have a unique signature within its containing type.");
            return false;
        }

        var containers = new List<TypeDeclarationSyntax>(4);
        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            ct.ThrowIfCancellationRequested();

            if (type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is not TypeDeclarationSyntax typeDeclaration ||
                type.TypeKind is not (TypeKind.Class or TypeKind.Struct)                                                    ||
                !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)                                                   ||
                typeDeclaration.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                problem = new DurationProblem("CAPD001", "Every containing type must be a non-file-local partial class, struct, or record.");
                return false;
            }

            containers.Add(typeDeclaration);
        }

        var attribute  = context.Attributes[0];
        var expression = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        var options = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int value
            ? (DurationParseOptions)value
            : DurationParseOptions.None;
        if (!DurationParser.TryParse(expression, ct, out var duration, out problem, options))
        {
            return false;
        }

        var requiresFixed = signature.AnchorKind == DurationValueKind.TimeOnly ||
                            signature is { AnchorKind: DurationValueKind.None, ValueKind: DurationValueKind.TimeSpan };
        if (requiresFixed && duration.HasCalendarComponents)
        {
            problem = new DurationProblem("CAPD007", "Months and years require a CalendarDuration value or a date anchor.");
            return false;
        }

        if (signature.AnchorKind == DurationValueKind.DateOnly && duration.FixedTicks % TimeSpan.TicksPerDay != 0)
        {
            problem = new DurationProblem("CAPD010", "A DateOnly operation requires a whole number of fixed days.");
            return false;
        }

        var text = DurationEmitter.Emit(member, (MemberDeclarationSyntax)context.TargetNode, containers, signature, duration);

        // Documentation identities include member kind, containing types, parameter types, and ref modifiers.
        using var hash       = SHA256.Create();
        var       identifier = hash.ComputeHash(Encoding.UTF8.GetBytes(identity));
        var       hintName   = "Duration_" + BitConverter.ToString(identifier).Replace("-", "") + ".g.cs";

        source = new GeneratedSource(hintName, text);

        return true;
    }
}
