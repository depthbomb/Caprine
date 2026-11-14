namespace Caprine.Durations.Generators;

internal readonly struct ParsedDuration
{
    internal int  Years                 { get; }
    internal int  Months                { get; }
    internal long FixedTicks            { get; }
    internal bool HasCalendarComponents => Years != 0 || Months != 0;

    internal ParsedDuration(int years, int months, long fixedTicks)
    {
        Years      = years;
        Months     = months;
        FixedTicks = fixedTicks;
    }
}
