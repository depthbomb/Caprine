#if DURATION_GENERATOR
namespace Caprine.Durations.Generators;
#else
namespace Caprine.Durations;
#endif

/// <summary>Controls validation of compile-time duration expressions.</summary>
[Flags]
#if DURATION_GENERATOR
internal enum DurationParseOptions
    #else
public enum DurationParseOptions
    #endif
{
    /// <summary>Extract recognized quantities, ignoring other text. No matches produces zero.</summary>
    None = 0,

    /// <summary>Require at least one valid quantity and reject unknown units, malformed numbers, and other text.</summary>
    /// <remarks>
    ///     Quantities may be adjacent or separated by whitespace, a comma, or the word <c>and</c>.
    ///     A comma may be followed by <c>and</c>. Leading and trailing connectors are not allowed.
    ///     Use an explicit quantity such as <c>0s</c> for zero.
    /// </remarks>
    Strict = 1,
}
