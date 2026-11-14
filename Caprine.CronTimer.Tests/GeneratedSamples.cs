namespace Caprine.CronTimer.Tests;

internal static partial class GeneratedSamples
{
    [GeneratedCronExpression("0 12 *,1 * MON")]
    internal static partial CronExpression LeadingWildcard { get; }

    [GeneratedCronExpression("0 12 1,* * MON")]
    internal static partial CronExpression TrailingWildcard { get; }

    [GeneratedCronExpression("0 0 L-0,L-3,L-1 * ?", CronExpressionOptions.AllowCalendarModifiers)]
    internal static partial CronExpression LastDays { get; }

    [GeneratedCronExpression("0 0 1W,15W,31W,LW * *", CronExpressionOptions.AllowCalendarModifiers)]
    internal static partial CronExpression Weekdays { get; }

    [GeneratedCronExpression("0 0 ? * FRIL,MON#5,7L", CronExpressionOptions.AllowCalendarModifiers)]
    internal static partial CronExpression WeekdayOrdinals { get; }

    [GeneratedCronExpression("@secondly", CronExpressionOptions.AllowNicknames | CronExpressionOptions.IncludeSeconds)]
    internal static partial CronExpression Secondly { get; }

    [GeneratedCronExpression("1-59/99999999999999999999 * * * *")]
    internal static partial CronExpression LargeStep { get; }

    [GeneratedCronExpression("30 1,2,3 * * *")]
    internal static partial CronExpression Dst { get; }

    [GeneratedCronExpression("*/5 * * * *")]
    internal static partial CronExpression EveryFiveMinutes();

    [GeneratedCronExpression("5-59/15 3,8 1-23/4 1-31/3 jan,mar-jun sun,mon-7 2030,2028",
        CronExpressionOptions.IncludeSeconds | CronExpressionOptions.IncludeYear)]
    internal static partial CronExpression AllFields();

    [GeneratedCronExpression("@yearly", CronExpressionOptions.AllowNicknames | CronExpressionOptions.IncludeSeconds | CronExpressionOptions.IncludeYear)]
    internal static partial CronExpression Yearly();

    [GeneratedCronExpression("0 0 31 FEB *")]
    internal static partial CronExpression Impossible();
}
