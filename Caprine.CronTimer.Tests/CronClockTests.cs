using System.Runtime.CompilerServices;
using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class CronClockTests
{
    [Fact]
    public async Task EarlyCallbacksCannotSignalATick()
    {
        var       clock = new ControlledClock(Utc(2026, 1, 1).AddTicks(-1));
        using var timer = new CronTimer("* * * * *", clock);
        var       wait  = timer.WaitForNextTickAsync();
        Assert.Equal(TimeSpan.FromMilliseconds(1), clock.Timer!.DueTime);
        clock.Timer.Fire();
        Assert.False(wait.IsCompleted);
        Assert.Null(timer.LastRun);
        clock.Now = Utc(2026, 1, 1);
        clock.Timer.Fire();
        Assert.True(await wait);
        Assert.Equal(clock.Now, timer.LastRun);
    }

    [Fact]
    public async Task LateCallbacksCoalesceDirectlyToThePresent()
    {
        var       clock = new ControlledClock(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * * *", clock, TimeZoneInfo.Utc, CronExpressionOptions.IncludeSeconds);
        var       wait  = timer.WaitForNextTickAsync();
        clock.Now = clock.Now.AddDays(10).AddMilliseconds(500);
        clock.Timer!.Fire();
        Assert.True(await wait);
        Assert.Equal(Utc(2026, 1, 11), timer.LastRun);
        Assert.Equal(Utc(2026, 1, 11, 0, 0, 1), timer.NextRun);
        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Timer.DueTime);
        Assert.Equal(2, clock.Timer.Changes);
        wait = timer.WaitForNextTickAsync();
        Assert.False(wait.IsCompleted);
        timer.Dispose();
        Assert.False(await wait);
    }

    [Fact]
    public async Task BackwardClockChangesDelayTheOriginalNextOccurrence()
    {
        var       clock = new ControlledClock(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        var       wait  = timer.WaitForNextTickAsync();
        clock.Now = clock.Now.AddHours(-1);
        clock.Timer!.Fire();
        Assert.False(wait.IsCompleted);
        Assert.Equal(Utc(2026, 1, 1, 0, 1), timer.NextRun);
        Assert.Equal(TimeSpan.FromMinutes(1), clock.Timer.DueTime);
        clock.Now = Utc(2026, 1, 1, 0, 1);
        clock.Timer.Fire();
        Assert.True(await wait);
    }

    [Fact]
    public async Task CallbackAfterDisposalDoesNothing()
    {
        var       clock = new ControlledClock(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        timer.Dispose();
        clock.Now = clock.Now.AddHours(1);
        clock.Timer!.Fire();
        Assert.True(clock.Timer.Disposed);
        Assert.Null(timer.NextRun);
        Assert.Null(timer.LastRun);
        Assert.False(await timer.WaitForNextTickAsync());
    }

    [Fact]
    public async Task ClockFailuresFaultWaitsInsteadOfEscapingTheCallback()
    {
        var       clock = new ControlledClock(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        var       wait  = timer.WaitForNextTickAsync();
        clock.Failure = new InvalidOperationException("Clock failed.");
        clock.Timer!.Fire();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await wait);
        Assert.Same(clock.Failure, error);
        Assert.Null(timer.NextRun);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(async () => await timer.WaitForNextTickAsync()));
        timer.Dispose();
        Assert.False(await timer.WaitForNextTickAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimerCreationDoesNotCaptureExecutionContext(bool alreadySuppressed)
    {
        var clock = new ControlledClock(Utc(2026, 1, 1));
        if (alreadySuppressed)
        {
            using (ExecutionContext.SuppressFlow())
            {
                using var timer = new CronTimer("* * * * *", clock);
                Assert.True(clock.FlowSuppressedDuringCreation);
            }
        }
        else
        {
            using var timer = new CronTimer("* * * * *", clock);
            Assert.True(clock.FlowSuppressedDuringCreation);
        }

        Assert.False(ExecutionContext.IsFlowSuppressed());
    }

    [Fact]
    public void AnIdleTimerCanBeCollectedAndReleasesItsProviderTimer()
    {
        var clock     = new ControlledClock(Utc(2026, 1, 1));
        var reference = CreateAbandonedTimer(clock);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.IsAlive);
        Assert.True(clock.Timer!.Disposed);
    }

    [Fact]
    public async Task AnActiveWaitKeepsItsOwnerAliveUntilConsumption()
    {
        var clock = new ControlledClock(Utc(2026, 1, 1));
        var (reference, wait) = CreateAbandonedWait(clock);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.True(reference.IsAlive);
        clock.Now = clock.Now.AddMinutes(1);
        clock.Timer!.Fire();
        Assert.True(await wait);
    }

    [Theory]
    [InlineData("clock")]
    [InlineData("change")]
    [InlineData("disposed")]
    public async Task ProviderFailuresDuringExpressionChangesCompleteWaits(string failure)
    {
        var       clock    = new ControlledClock(Utc(2026, 1, 1));
        using var timer    = new CronTimer("* * * * *", clock);
        var       wait     = timer.WaitForNextTickAsync();
        var       expected = new InvalidOperationException("Provider failed.");
        if (failure == "clock")
        {
            clock.Failure = expected;
        }
        else if (failure == "change")
        {
            clock.Timer!.ChangeFailure = expected;
        }
        else
        {
            clock.Timer!.Dispose();
        }

        var error = Record.Exception(() => timer.Expression = "*/5 * * * *");
        Assert.NotNull(error);
        Assert.True(wait.IsCompleted, "A provider failure must not leave a wait pending indefinitely.");
        Assert.Same(error, await Record.ExceptionAsync(async () => await wait));
        Assert.Null(timer.NextRun);
        Assert.Same(error, await Record.ExceptionAsync(async () => await timer.WaitForNextTickAsync()));
        Assert.Same(error, Record.Exception(() => timer.Expression = "0 * * * *"));
        timer.Dispose();
        Assert.False(await timer.WaitForNextTickAsync());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAbandonedTimer(ControlledClock clock) => new(new CronTimer("* * * * *", clock));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Reference, ValueTask<bool> Wait) CreateAbandonedWait(ControlledClock clock)
    {
        var timer = new CronTimer("* * * * *", clock);
        return (new WeakReference(timer), timer.WaitForNextTickAsync());
    }

    private sealed class ControlledClock : TimeProvider
    {
        public override TimeZoneInfo     LocalTimeZone                => TimeZoneInfo.Utc;
        internal        DateTimeOffset   Now                          { get; set; }
        internal        ControlledTimer? Timer                        { get; private set; }
        internal        Exception?       Failure                      { get; set; }
        internal        bool             FlowSuppressedDuringCreation { get; private set; }

        internal ControlledClock(DateTimeOffset now)
        {
            Now = now;
        }

        public override DateTimeOffset GetUtcNow() => Failure is { } error ? throw error : Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, dueTime);
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            FlowSuppressedDuringCreation = ExecutionContext.IsFlowSuppressed();
            Timer                        = new ControlledTimer(callback, state);
            return Timer;
        }
    }

    private sealed class ControlledTimer : ITimer
    {
        internal         TimeSpan      DueTime       { get; private set; }
        internal         bool          Disposed      { get; private set; }
        internal         int           Changes       { get; private set; }
        internal         Exception?    ChangeFailure { get; set; }
        private readonly TimerCallback _callback;
        private readonly object?       _state;

        internal ControlledTimer(TimerCallback callback, object? state)
        {
            _callback = callback;
            _state    = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (ChangeFailure is { } error)
            {
                throw error;
            }

            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            DueTime = dueTime;
            Changes++;
            return !Disposed;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        internal void Fire() => _callback(_state);
    }
}
