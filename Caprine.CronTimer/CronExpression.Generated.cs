using System.ComponentModel;

namespace Caprine.CronTimer;

public sealed partial class CronExpression
{
    /// <summary>Creates an expression from the versioned data emitted by the source generator.</summary>
    /// <param name="expression">The original expression text, retained for display without parsing it.</param>
    /// <param name="options">The options used to compile the expression.</param>
    /// <param name="data">The generator's precomputed field and modifier data. The contents are copied.</param>
    /// <returns>An immutable expression evaluated using the precomputed data.</returns>
    /// <remarks>
    /// This method supports generated code and is not intended for manually supplied schedules.
    /// The data determines matching behavior; the text is not reparsed to verify that they agree.
    /// Unsupported data versions and malformed payloads are rejected.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an undefined flag.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is malformed or uses an unsupported version.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CronExpression FromGenerated(string expression, CronExpressionOptions options, ReadOnlySpan<byte> data)
        => new(CronExpressionData.Decode(expression, options, data));
}
