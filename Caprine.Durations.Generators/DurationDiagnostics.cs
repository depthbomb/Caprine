using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Caprine.Durations.Generators;

internal static class DurationDiagnostics
{
    private static readonly DiagnosticDescriptor                 Declaration        = new("CAPD001", "Invalid duration declaration", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Missing            = new("CAPD002", "Missing duration expression", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 FractionalCalendar = new("CAPD003", "Fractional calendar quantity", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Precision          = new("CAPD004", "Unrepresentable duration precision", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Range              = new("CAPD005", "Duration out of range", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Size               = new("CAPD006", "Duration expression too large", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Anchor             = new("CAPD007", "Calendar duration requires an anchor", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Syntax             = new("CAPD008", "Invalid duration syntax", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Options            = new("CAPD009", "Invalid duration parsing options", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Target             = new("CAPD010", "Duration is incompatible with target", "{0}", "Caprine.Durations", DiagnosticSeverity.Error, true);
    private static readonly ImmutableArray<DiagnosticDescriptor> Rules              = [Declaration, Missing, FractionalCalendar, Precision, Range, Size, Anchor, Syntax, Options, Target];

    internal static Diagnostic Create(DurationProblem problem, Location? location)
    {
        var rule = Rules.First(candidate => candidate.Id == problem.Id);

        return Diagnostic.Create(rule, location, problem.Message);
    }
}
