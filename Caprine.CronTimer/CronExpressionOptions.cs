namespace Caprine.CronTimer;

/// <summary>Selects extensions to the default OCPS 1.0 five-field syntax.</summary>
/// <remarks>
///     Options are explicit. Six fields mean seconds or a year according to the selected flags; the parser never
///     guesses.
/// </remarks>
[Flags]
#if CRON_GENERATOR
internal enum CronExpressionOptions
    #else
public enum CronExpressionOptions
    #endif
{
    /// <summary>Accept only OCPS 1.0 Revision 3 syntax.</summary>
    None = 0,

    /// <summary>Require a seconds field before the five standard fields.</summary>
    IncludeSeconds = 1,

    /// <summary>Require a year field after the five standard fields. Years range from 1 through 9999.</summary>
    IncludeYear = 2,

    /// <summary>Allow the documented scheduling nicknames, such as <c>@daily</c>.</summary>
    /// <remarks>
    ///     Enables <c>@yearly</c>, <c>@annually</c>, <c>@monthly</c>, <c>@weekly</c>,
    ///     <c>@daily</c>, <c>@midnight</c>, <c>@hourly</c>, and <c>@minutely</c>.
    ///     <c>@secondly</c> additionally requires <see cref="IncludeSeconds" />.
    ///     Names are case-insensitive and expand to the selected field layout.
    /// </remarks>
    AllowNicknames = 4,

    /// <summary>
    ///     Allow <c>?</c>, <c>L</c>, <c>L-n</c>, <c>nW</c>, <c>LW</c>, <c>nL</c>, and <c>n#k</c> in the applicable day
    ///     fields.
    /// </summary>
    /// <remarks>
    ///     In day-of-month, L is the last day, L-n subtracts 0 through 30 days, nW selects the nearest
    ///     weekday without crossing month boundaries, and LW or WL selects the last weekday.
    ///     Nonexistent requested days are skipped. In day-of-week, nL is the last weekday n and n#k
    ///     selects occurrence 1 through 5; bare L means the last Saturday. Weekday names are accepted.
    ///     A question mark acts as a wildcard in either day field. Modifiers may be listed, but not stepped or ranged.
    /// </remarks>
    AllowCalendarModifiers = 8,
}
