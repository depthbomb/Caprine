namespace Caprine.CronTimer;

// Compiled into the runtime and generator so literal validation uses exactly the runtime grammar.
internal sealed partial class CronExpressionData
{
    internal string                       Expression             { get; }
    internal CronExpressionOptions        Options                { get; }
    internal bool                         DayOfMonthUnrestricted { get; private set; }
    internal bool                         DayOfWeekUnrestricted  { get; private set; }
    internal bool                         LastWeekday            { get; private set; }
    internal bool[]?                      Years                  { get; private set; }
    internal bool[]                       Seconds                { get; } = new bool[60];
    internal bool[]                       Minutes                { get; } = new bool[60];
    internal bool[]                       Hours                  { get; } = new bool[24];
    internal bool[]                       Months                 { get; } = new bool[13];
    internal bool[]                       DaysOfMonth            { get; } = new bool[32];
    internal bool[]                       DaysOfWeek             { get; } = new bool[7];
    internal List<int>                    LastDayOffsets         { get; } = [];
    internal List<int>                    NearestWeekdays        { get; } = [];
    internal List<int>                    LastWeekdays           { get; } = [];
    internal List<(int Day, int Ordinal)> NthWeekdays            { get; } = [];

    internal CronExpressionData(string expression, CronExpressionOptions options, bool parse = true)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        ValidateOptions(options);

        Expression = expression;
        Options    = options;

        if (parse)
        {
            ParseFields(expression);
        }
    }

    internal static void ValidateOptions(CronExpressionOptions options)
    {
        const CronExpressionOptions all = CronExpressionOptions.IncludeSeconds | CronExpressionOptions.IncludeYear |
                                          CronExpressionOptions.AllowNicknames | CronExpressionOptions.AllowCalendarModifiers;
        if ((options & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}
