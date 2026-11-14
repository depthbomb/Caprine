# Caprine.CronTimer

A timer for cron schedules. If you've used `PeriodicTimer`, the wait loop should feel familiar.

Supports .NET 10 and .NET 11. Building from source or using the source generator requires the .NET 11 SDK with C# 15.

The .NET 10 SDK works too if you're only using runtime expressions. Older compilers skip the bundled generator.

## Getting started

Install the package:

```shell
dotnet add package Caprine.CronTimer --version 0.1.0
```

This runs every five minutes in UTC:

```csharp
using Caprine.CronTimer;

internal static class Program
{
    private static async Task Main()
    {
        using var timer = new CronTimer("*/5 * * * *", TimeZoneInfo.Utc);
        while (await timer.WaitForNextTickAsync())
        {
            Console.WriteLine($"Scheduled at {timer.LastRun:O}");
        }
    }
}
```

Leave out the time zone to use your local zone. You can also pass a `TimeProvider`, which is handy for testing with
`FakeTimeProvider`.

## Working with the timer

- `WaitForNextTickAsync(ct)` returns a `ValueTask<bool>`. Keep one wait active at a time and await each result once.
- Canceling a wait throws `OperationCanceledException`. The timer keeps running.
- If your work takes longer than the schedule allows, missed ticks are combined into one pending tick.
- `Dispose()` stops the timer and completes a pending wait with `false`.
- `LastRun` and `NextRun` give you the scheduled times as nullable `DateTimeOffset` values.

Set `timer.Expression` to change the schedule, much like setting `PeriodicTimer.Period`. Future ticks are rescheduled
from now, and any pending tick is kept. Invalid expressions leave the current schedule alone. The timer's time zone and
syntax options stay the same.

If a schedule has no more occurrences, waits return `false` after the final tick. Giving it a new expression with a
future occurrence starts it again.

## Cron syntax

By default, expressions
follow [OCPS 1.0, revision 3](https://github.com/open-source-cron/ocps/blob/main/specifications/OCPS-1.0.md):

```text
minute hour day-of-month month day-of-week
0-59   0-23  1-31         1-12  0-7
```

Use `*` for any value, commas for lists, and ascending ranges like `MON-FRI`. Steps work on wildcards and ranges, such
as `*/15` or `5-55/10`; `0/15` isn't valid. Month and weekday names are case-insensitive. Sunday is `0` or `7`.

Separate fields with spaces or tabs. Comments and newlines aren't accepted. Standard five-field expressions tick at
second zero.

One OCPS detail to keep in mind: a day field is unrestricted only if its first list item is a plain `*`. When both day
fields are restricted, either can match; otherwise, both must match. So `*,1` is unrestricted, but `1,*` and `*/1`
aren't.

### Extra syntax

Enable extras with `CronExpressionOptions` when creating a timer or expression. Combine flags with `|`:

| Option                   | Adds                                              |
|--------------------------|---------------------------------------------------|
| `IncludeSeconds`         | A seconds field at the start, from 0 to 59        |
| `IncludeYear`            | A year field at the end, from 1 to 9999           |
| `AllowNicknames`         | Shortcuts like `@daily`, `@hourly`, and `@weekly` |
| `AllowCalendarModifiers` | Day rules like `L`, `15W`, `LW`, and `MON#2`      |

Field layouts are explicit: six fields need either `IncludeSeconds` or `IncludeYear`; both flags give you seven fields.

Nicknames include `@yearly`/`@annually`, `@monthly`, `@weekly` (Sunday), `@daily`/`@midnight`, `@hourly`, and
`@minutely`. `@secondly` also needs `IncludeSeconds`.

With calendar modifiers enabled, `L` in the day-of-month field means the last day, `L-n` counts back from it, `15W`
picks the nearest weekday to the 15th, and `LW` picks the last weekday. In the day-of-week field, `MONL` means the last
Monday and `MON#2` means the second Monday. `?` works as a wildcard in either day field.

## Source generation

For expressions you know ahead of time, the included source generator checks the syntax during compilation and saves
parsing it at runtime:

```csharp
using Caprine.CronTimer;

internal static partial class Schedules
{
    [GeneratedCronExpression("*/5 * * * *")]
    internal static partial CronExpression Cleanup();

    [GeneratedCronExpression("@daily", CronExpressionOptions.AllowNicknames)]
    internal static partial CronExpression Daily { get; }
}
```

Pass `Schedules.Cleanup()` or `Schedules.Daily` straight to a `CronTimer` constructor. Each member returns a cached,
immutable expression that you can share across timers.

Use a static partial method with no parameters or type parameters, or a static partial property with only a getter.
Return `CronExpression` by value, leave the implementation to the generator, and make every containing type partial and
non-file-local.

If you're using project references instead of NuGet, also reference `Caprine.CronTimer.Generators.csproj` with
`OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

## Time zones and clock changes

Schedules follow the chosen time zone. When daylight saving time skips a local time, that occurrence is skipped too.
When a local time repeats, both occurrences run, in UTC order.

The timer checks the wall clock at least once a minute while scheduled. If a callback runs late, it signals the most
recent due occurrence and carries on from the current time. Moving the clock backward keeps the already selected next
occurrence.

`LastRun` is the scheduled time, so it can be earlier than when your code wakes up. Time-zone rules come from the host
system, and missed work isn't saved across app restarts.

## Checking a schedule without a timer

Create a `CronExpression`, or use a generated one, to query a schedule directly:

- `GetNextOccurrence(baseTime, timeZone)` finds the next time strictly after `baseTime`.
- `GetLastOccurrence(baseTime, timeZone)` finds the last time strictly before it.
- `IsMatch(instant, timeZone)` checks an exact occurrence; fractional seconds don't match.
- `TryParse(text, out expression, options)` lets you check input without catching a syntax exception.

Queries use the local time zone if you leave it out. Searches return `null` when there's no matching occurrence. A valid
expression can still describe an impossible date, like February 31.

Expressions are immutable and safe to share between threads. Invalid syntax throws `FormatException` when constructing
an expression or changing a timer's schedule.
