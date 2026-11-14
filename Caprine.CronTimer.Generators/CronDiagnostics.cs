using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Caprine.CronTimer.Generators;

internal static class CronDiagnostics
{
    private static readonly DiagnosticDescriptor                 Declaration = new("CAPC001", "Invalid generated cron declaration", "{0}", "Caprine.CronTimer", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Expression  = new("CAPC002", "Invalid cron expression", "{0}", "Caprine.CronTimer", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor                 Options     = new("CAPC003", "Invalid cron expression options", "{0}", "Caprine.CronTimer", DiagnosticSeverity.Error, true);
    private static readonly ImmutableArray<DiagnosticDescriptor> Rules       = [Declaration, Expression, Options];

    internal static Diagnostic Create(CronProblem problem, Location? location)
    {
        var rule = Rules.First(candidate => candidate.Id == problem.Id);

        return Diagnostic.Create(rule, location, problem.Message);
    }
}
