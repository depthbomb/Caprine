namespace Caprine.CronTimer;

/// <summary>Generates a cached, pre-parsed cron expression from a compile-time literal.</summary>
/// <remarks>
///     Apply this attribute to a parameterless, non-generic static partial method or a static
///     read-only partial property returning <see cref="CronExpression" />. All containing classes,
///     structs, or records must be partial and must not be file-local. Invalid expressions produce
///     compiler diagnostics. Time-zone evaluation and timer operations still happen at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
public sealed class GeneratedCronExpressionAttribute : Attribute
{
    /// <summary>Gets the expression parsed during compilation.</summary>
    public string Expression { get; }

    /// <summary>Gets the explicitly enabled syntax extensions.</summary>
    public CronExpressionOptions Options { get; }

    /// <summary>Requests an implementation that returns a cached cron expression.</summary>
    /// <param name="expression">A constant cron expression. The default syntax is OCPS 1.0.</param>
    /// <param name="options">Constant flags selecting optional syntax extensions.</param>
    /// <remarks>The generator validates these arguments during compilation.</remarks>
    public GeneratedCronExpressionAttribute(string expression, CronExpressionOptions options = CronExpressionOptions.None)
    {
        Expression = expression;
        Options    = options;
    }
}
