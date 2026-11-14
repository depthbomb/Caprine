using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Caprine.CronTimer.Generators;

internal static class CronDeclaration
{
    internal const string AttributeName = "Caprine.CronTimer.GeneratedCronExpressionAttribute";

    internal static bool TryCreate(GeneratorAttributeSyntaxContext context,
                                   CancellationToken               ct,
                                   out GeneratedSource             source,
                                   out CronProblem                 problem)
    {
        source  = default;
        problem = default;

        ct.ThrowIfCancellationRequested();

        var symbol      = context.TargetSymbol;
        var attribute   = context.Attributes[0];
        var declaration = context.TargetNode as MemberDeclarationSyntax;
        var type = symbol switch
        {
            IMethodSymbol method     => method.ReturnType,
            IPropertySymbol property => property.Type,
            _                        => null,
        };
        var expectedType = context.SemanticModel.Compilation.GetTypeByMetadataName("Caprine.CronTimer.CronExpression");
        if (context.Attributes.Length != 1 ||
            !IsValidMember(symbol)         ||
            !SymbolEqualityComparer.Default.Equals(type, expectedType))
        {
            problem = new CronProblem("CAPC001", "Use a parameterless, non-generic static partial method or a static get-only partial property returning CronExpression by value, without an implementation.");
            return false;
        }

        var containers = new List<TypeDeclarationSyntax>();
        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            ct.ThrowIfCancellationRequested();

            if (container.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is not TypeDeclarationSyntax syntax ||
                container.TypeKind is not (TypeKind.Class or TypeKind.Struct)                                           ||
                !syntax.Modifiers.Any(SyntaxKind.PartialKeyword)                                                        ||
                syntax.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                problem = new CronProblem("CAPC001", "Every containing type must be a non-file-local partial class, struct, or record.");
                return false;
            }

            containers.Add(syntax);
        }

        var text = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        var options = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int value
            ? (CronExpressionOptions)value
            : CronExpressionOptions.None;

        CronExpressionData data;

        try
        {
            data = new CronExpressionData(text!, options);
        }
        catch (ArgumentOutOfRangeException)
        {
            problem = new CronProblem("CAPC003", "Cron options contain an undefined flag.");
            return false;
        }
        catch (ArgumentNullException)
        {
            problem = new CronProblem("CAPC002", "The cron expression must be a non-null constant string.");
            return false;
        }
        catch (FormatException error)
        {
            problem = new CronProblem("CAPC002", error.Message);
            return false;
        }

        ct.ThrowIfCancellationRequested();

        var       identity = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + symbol.MetadataName;
        using var hash     = SHA256.Create();
        var       key      = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        var       holder   = "__CaprineCron_" + key;
        while (symbol.ContainingType.GetMembers(holder).Length != 0)
        {
            holder += "_";
        }

        source = new GeneratedSource("Cron_" + key + ".g.cs", Emit(symbol, declaration!, containers, type!, text!, options, data.Encode(), holder));
        return true;
    }

    private static bool IsValidMember(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method is
        {
            IsStatic                               : true,
            IsPartialDefinition                    : true,
            PartialImplementationPart              : null,
            Parameters.Length                      : 0,
            TypeParameters.Length                  : 0,
            ReturnsByRef                           : false,
            ReturnsByRefReadonly                   : false,
            IsAbstract                             : false,
            IsExtern                               : false,
            IsExtensionMethod                      : false,
            ExplicitInterfaceImplementations.Length: 0,
        },
        IPropertySymbol property => property is
        {
            IsStatic                               : true,
            IsPartialDefinition                    : true,
            PartialImplementationPart              : null,
            Parameters.Length                      : 0,
            GetMethod                              : not null,
            SetMethod                              : null,
            ReturnsByRef                           : false,
            ReturnsByRefReadonly                   : false,
            IsAbstract                             : false,
            IsExtern                               : false,
            ExplicitInterfaceImplementations.Length: 0,
        },
        _ => false,
    };

    private static string Emit(ISymbol                     member,
                               MemberDeclarationSyntax     declaration,
                               List<TypeDeclarationSyntax> containers,
                               ITypeSymbol                 returnType,
                               string                      expression,
                               CronExpressionOptions       options,
                               byte[]                      data,
                               string                      holder)
    {
        var builder     = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        var indentation = 0;
        var namespaces  = new Stack<string>();
        for (var current = member.ContainingNamespace; !current.IsGlobalNamespace; current = current.ContainingNamespace)
        {
            namespaces.Push("@" + current.Name);
        }

        if (namespaces.Count != 0)
        {
            builder.Append("namespace ").Append(string.Join(".", namespaces)).AppendLine();
            builder.AppendLine("{");
            indentation = 4;
        }

        for (var index = containers.Count - 1; index >= 0; index--)
        {
            var container = containers[index];
            var kind = container is RecordDeclarationSyntax record
                ? record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record class"
                : container.Keyword.Text;

            builder.Append(' ', indentation).Append(string.Join(" ", container.Modifiers.Select(token => token.Text)));
            builder.Append(' ').Append(kind).Append(" @").Append(container.Identifier.ValueText);

            if (container.TypeParameterList is { } parameters)
            {
                builder.Append('<').Append(string.Join(", ", parameters.Parameters.Select(parameter => "@" + parameter.Identifier.ValueText))).Append('>');
            }

            builder.AppendLine().Append(' ', indentation).AppendLine("{");
            indentation += 4;
        }

        var modifiers = declaration is MethodDeclarationSyntax method ? method.Modifiers : ((PropertyDeclarationSyntax)declaration).Modifiers;
        var format    = SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        builder.Append(' ', indentation).Append(string.Join(" ", modifiers.Select(token => token.Text)));
        builder.Append(' ').Append(returnType.ToDisplayString(format)).Append(" @").Append(member.Name);
        if (member is IMethodSymbol)
        {
            builder.Append("()");
        }

        builder.Append(" => ").Append(holder).AppendLine(".Instance;");
        builder.AppendLine();
        builder.Append(' ', indentation).Append("private static class ").AppendLine(holder);
        builder.Append(' ', indentation).AppendLine("{");
        builder.Append(' ', indentation + 4).AppendLine("internal static readonly global::Caprine.CronTimer.CronExpression Instance =");
        builder.Append(' ', indentation + 8).Append("global::Caprine.CronTimer.CronExpression.FromGenerated(");
        builder.Append(SymbolDisplay.FormatLiteral(expression, true)).Append(", (global::Caprine.CronTimer.CronExpressionOptions)");
        builder.Append(((int)options).ToString(CultureInfo.InvariantCulture)).AppendLine(",");
        builder.Append(' ', indentation + 12).Append('[').Append(string.Join(", ", data.Select(value => value.ToString(CultureInfo.InvariantCulture)))).AppendLine("]);");
        builder.Append(' ', indentation).AppendLine("}");
        for (var index = 0; index < containers.Count; index++)
        {
            indentation -= 4;
            builder.Append(' ', indentation).AppendLine("}");
        }

        if (namespaces.Count != 0)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }
}
