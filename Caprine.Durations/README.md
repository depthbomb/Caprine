# Caprine.Durations

Duration literals checked at compile time, with exact `TimeSpan` values and calendar arithmetic for months and years.

Supports .NET 10 and .NET 11. Building from source or using the source generator requires the .NET 11 SDK with C# 15.
The .NET 10 SDK works for runtime `CalendarDuration` operations; older compilers skip the bundled generator.

## Getting started

Install the package:

```shell
dotnet add package Caprine.Durations --version 0.1.0
```

Declare a duration value or an operation that adds it to a starting date:

```csharp
using Caprine.Durations;

internal static partial class Program
{
    [GeneratedDuration("30s", DurationParseOptions.Strict)]
    private static partial TimeSpan RequestTimeout { get; }

    [GeneratedDuration("1mo 3d", DurationParseOptions.Strict)]
    private static partial DateTimeOffset RenewalDate(DateTimeOffset start);

    private static void Main()
    {
        Console.WriteLine(RequestTimeout);
        Console.WriteLine(RenewalDate(new DateTimeOffset(2025, 1, 31, 12, 0, 0, TimeSpan.Zero)));
    }
}
```

The generator parses the expression during compilation. Generated members construct values from precomputed
components, so no text is parsed at runtime. Pass generated `TimeSpan` values straight to APIs such as `Task.Delay`.

## Duration syntax

Use signed decimal quantities with case-insensitive units. Common units are `tick` / `ticks`, `us`, `ms`, `s`, `m`,
`h`, `d`, `w`, `mo`, and `y`. Full singular and plural names work too, such as `microsecond`, `hours`, and `months`.
Use `m` for minutes and `mo` for months.

- `1h30m`, `1.5h`, and `1h 30m` describe the same fixed interval.
- Quantities may be negative, such as `1h -15m`.
- Each fixed quantity must represent an exact number of 100-nanosecond ticks. `0.1us` is one tick; `0.5tick` is invalid.
- Months and years must be whole numbers. Days are always 24 hours, and weeks are seven days.
- Numbers use ASCII decimal notation without grouping separators or exponents.

By default, unknown text, unknown units, and malformed numbers are ignored. An expression with no recognized
quantities produces zero. Out-of-range totals and quantities that cannot be represented exactly produce compiler errors.

Use `DurationParseOptions.Strict` to reject unrecognized or malformed input. Strict expressions accept adjacent
quantities, whitespace, commas, and `and` between quantities, including `1h, 30m and 2ticks`. Write `0s` for zero;
empty expressions and leading or trailing connectors are rejected.

## Generated members

Values can be static get-only partial properties or parameterless static partial methods returning `TimeSpan`
or `CalendarDuration`. Calendar components require `CalendarDuration` or a starting date.

Methods can accept a `DateTime`, `DateTimeOffset`, `DateOnly`, or `TimeOnly` and return the same type after addition.
A `DateTime` or `DateTimeOffset` argument also supports a `TimeSpan` result for the interval after applying the
duration:

```csharp
using Caprine.Durations;

internal static partial class Durations
{
    [GeneratedDuration("1y 1mo", DurationParseOptions.Strict)]
    internal static partial CalendarDuration Subscription { get; }

    [GeneratedDuration("1mo", DurationParseOptions.Strict)]
    internal static partial TimeSpan BillingPeriodLength(DateTimeOffset start);

    [GeneratedDuration("1mo", DurationParseOptions.Strict)]
    internal static partial bool TryRenewalDate(DateTimeOffset start, out DateTimeOffset result);

    [GeneratedDuration("27h", DurationParseOptions.Strict)]
    internal static partial TimeOnly AdvanceClock(TimeOnly start, out int wrappedDays);
}
```

Returning `bool` with an `out` result gives a nonthrowing operation: an out-of-range date returns `false` and resets
the result to `default`. The out result can also be a `TimeSpan` when the starting value is `DateTime` or
`DateTimeOffset`.
`TimeOnly` operations may append `out int wrappedDays`, after the out result for bool methods.

Every containing type must be partial and non-file-local. Methods must be static, partial, non-generic, and have no
implementation, optional parameters, or `ref` / `in` parameters. `DateOnly` requires a whole-day fixed portion;
`TimeOnly` rejects months and years. Incompatible declarations produce compiler errors.

If you're using project references instead of NuGet, also reference `Caprine.Durations.Generators.csproj` with
`OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

## Calendar arithmetic

`CalendarDuration` keeps years, months, and fixed ticks separate. It applies them in that order, using .NET's
month-end clamping. Adding one month to January 31 reaches the last day of February. Applying `1y 1mo` can differ
from applying `13mo`, and negating a duration does not necessarily undo its application.

Use `duration.AddTo(start)` or `start.Add(duration)` to apply a value. `ToTimeSpan(start)` resolves it against a date;
`ToTimeSpan()` requires a fixed-only value. `TryAddTo` and `TryGetTimeSpan` provide nonthrowing alternatives.

Operations preserve `DateTime.Kind` or the numeric `DateTimeOffset` offset. They do not apply named time-zone or
daylight-saving rules. Use UTC starting values for elapsed-time arithmetic. Intermediate and final dates must stay
within the supported date range.
