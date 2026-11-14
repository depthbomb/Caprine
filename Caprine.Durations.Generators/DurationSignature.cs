using Microsoft.CodeAnalysis;

namespace Caprine.Durations.Generators;

internal enum DurationValueKind
{
    None,
    TimeSpan,
    CalendarDuration,
    DateTime,
    DateTimeOffset,
    DateOnly,
    TimeOnly,
}

internal readonly struct DurationSignature
{
    internal DurationValueKind ValueKind  { get; }
    internal DurationValueKind AnchorKind { get; }
    internal bool              IsTry      { get; }

    internal string RuntimeMethod => ValueKind == DurationValueKind.TimeSpan
        ? IsTry ? "TryGetTimeSpan" : "ToTimeSpan"
        : IsTry
            ? "TryAddTo"
            : "AddTo";

    private DurationSignature(DurationValueKind valueKind, DurationValueKind anchorKind, bool isTry)
    {
        ValueKind  = valueKind;
        AnchorKind = anchorKind;
        IsTry      = isTry;
    }

    internal static bool TryCreate(ISymbol member, Compilation compilation, out DurationSignature signature)
    {
        signature = default;

        if (member is IPropertySymbol property)
        {
            var validProperty = property is
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
            };

            return validProperty && TryCreateValue(property.Type, compilation, out signature);
        }

        if (member is not IMethodSymbol method || method is not
            {
                IsStatic                               : true,
                IsPartialDefinition                    : true,
                PartialImplementationPart              : null,
                TypeParameters.Length                  : 0,
                ReturnsByRef                           : false,
                ReturnsByRefReadonly                   : false,
                IsExtensionMethod                      : false,
                IsAbstract                             : false,
                IsExtern                               : false,
                IsAsync                                : false,
                ExplicitInterfaceImplementations.Length: 0,
            } || method.Parameters.Any(parameter => parameter.IsOptional || parameter.IsParams))
        {
            return false;
        }

        var parameters = method.Parameters;
        if (parameters.Length == 0)
        {
            return TryCreateValue(method.ReturnType, compilation, out signature);
        }

        var anchorKind = GetKind(parameters[0].Type, compilation);
        if (parameters[0].RefKind != RefKind.None || anchorKind is not
                (DurationValueKind.DateTime or DurationValueKind.DateTimeOffset or DurationValueKind.DateOnly or DurationValueKind.TimeOnly))
        {
            return false;
        }

        var isTry          = method.ReturnType.SpecialType == SpecialType.System_Boolean;
        var parameterCount = isTry ? 2 : 1;
        if (parameters.Length < parameterCount || parameters.Length > parameterCount + 1 ||
            isTry && parameters[1].RefKind != RefKind.Out)
        {
            return false;
        }

        var valueKind = GetKind(isTry ? parameters[1].Type : method.ReturnType, compilation);
        var validResult = valueKind == anchorKind ||
                          valueKind == DurationValueKind.TimeSpan &&
                          anchorKind is DurationValueKind.DateTime or DurationValueKind.DateTimeOffset;
        if (!validResult)
        {
            return false;
        }

        if (parameters.Length > parameterCount &&
            (anchorKind                                  != DurationValueKind.TimeOnly ||
             parameters[parameterCount].RefKind          != RefKind.Out                ||
             parameters[parameterCount].Type.SpecialType != SpecialType.System_Int32))
        {
            return false;
        }

        signature = new DurationSignature(valueKind, anchorKind, isTry);
        return true;
    }

    private static bool TryCreateValue(ITypeSymbol type, Compilation compilation, out DurationSignature signature)
    {
        var kind = GetKind(type, compilation);
        signature = new DurationSignature(kind, DurationValueKind.None, false);

        return kind is DurationValueKind.TimeSpan or DurationValueKind.CalendarDuration;
    }

    private static DurationValueKind GetKind(ITypeSymbol type, Compilation compilation)
    {
        var (kind, name) = type.Name switch
        {
            "TimeSpan"         => (DurationValueKind.TimeSpan, "System.TimeSpan"),
            "CalendarDuration" => (DurationValueKind.CalendarDuration, "Caprine.Durations.CalendarDuration"),
            "DateTime"         => (DurationValueKind.DateTime, "System.DateTime"),
            "DateTimeOffset"   => (DurationValueKind.DateTimeOffset, "System.DateTimeOffset"),
            "DateOnly"         => (DurationValueKind.DateOnly, "System.DateOnly"),
            "TimeOnly"         => (DurationValueKind.TimeOnly, "System.TimeOnly"),
            _                  => (DurationValueKind.None, ""),
        };

        return kind != DurationValueKind.None && SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName(name))
            ? kind
            : DurationValueKind.None;
    }
}
