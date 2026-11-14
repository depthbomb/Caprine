using Microsoft.Extensions.Time.Testing;
using static Caprine.CronTimer.Tests.CronExpressionTests;

namespace Caprine.CronTimer.Tests;

public sealed class CronTimerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitingAndCoalescingMatchPeriodicTimer(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        var       wait    = fixture.Wait();
        Assert.False(wait.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await wait);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await fixture.Wait());
        wait = fixture.Wait();
        Assert.False(wait.IsCompleted);
        fixture.Dispose();
        Assert.False(await wait);
        Assert.False(await fixture.Wait());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitRemainsExclusiveUntilItsResultIsConsumed(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        using var cts     = new CancellationTokenSource();
        cts.Cancel();
        var wait = fixture.Wait();
        Assert.Throws<InvalidOperationException>(() => fixture.Wait());
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(wait.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => fixture.Wait());
        Assert.Throws<InvalidOperationException>(() => fixture.Wait(cts.Token));
        Assert.True(await wait);
        var next = fixture.Wait();
        fixture.Dispose();
        Assert.False(await next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationKeepsTokenAndDoesNotStopScheduling(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        using var cts     = new CancellationTokenSource();
        var       wait    = fixture.Wait(cts.Token);
        cts.Cancel();
        Assert.True(wait.IsCanceled);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await wait);
        Assert.Equal(cts.Token, error.CancellationToken);
        var next = fixture.Wait();
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalVoidsSignaledButUnconsumedTicks(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        var       wait    = fixture.Wait();
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(wait.IsCompleted);
        fixture.Dispose();
        Assert.False(await wait);
        Assert.False(await fixture.Wait());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCanceledTokenWinsEvenAfterDisposal(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        using var cts     = new CancellationTokenSource();
        cts.Cancel();
        fixture.Dispose();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await fixture.Wait(cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndTickUseTheFirstSignal(bool cron)
    {
        using var fixture = new TimerFixture(cron);
        using var cts     = new CancellationTokenSource();
        var       wait    = fixture.Wait(cts.Token);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        cts.Cancel();
        Assert.True(await wait);

        using var secondCts = new CancellationTokenSource();
        wait = fixture.Wait(secondCts.Token);
        secondCts.Cancel();
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await wait);
    }

    [Fact]
    public async Task ExpressionChangeKeepsTheActiveWaitAndUsesNewSchedule()
    {
        var       clock = new FakeTimeProvider(Utc(2026, 1, 1));
        using var timer = new CronTimer("0 * * * *", clock);
        var       wait  = timer.WaitForNextTickAsync();
        timer.Expression = "*/5 * * * *";
        Assert.Equal(Utc(2026, 1, 1, 0, 5), timer.NextRun);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await wait);
        Assert.Equal(Utc(2026, 1, 1, 0, 5), timer.LastRun);
        Assert.Equal(Utc(2026, 1, 1, 0, 10), timer.NextRun);
        Assert.Same(clock, timer.TimeProvider);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpressionChangePreservesExistingTick(bool activeWait)
    {
        var       clock = new FakeTimeProvider(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        var       wait  = activeWait ? timer.WaitForNextTickAsync() : default;
        clock.Advance(TimeSpan.FromMinutes(1));
        timer.Expression = "0 * * * *";
        Assert.True(await (activeWait ? wait : timer.WaitForNextTickAsync()));
        Assert.Equal(Utc(2026, 1, 1, 1), timer.NextRun);
        Assert.Equal(Utc(2026, 1, 1, 0, 1), timer.LastRun);
    }

    [Fact]
    public void InvalidExpressionChangeLeavesTheScheduleUntouched()
    {
        var       clock = new FakeTimeProvider(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        var       next  = timer.NextRun;
        Assert.Throws<FormatException>(() => timer.Expression       = "@daily");
        Assert.Throws<ArgumentNullException>(() => timer.Expression = null!);
        Assert.Equal("* * * * *", timer.Expression);
        Assert.Equal(next, timer.NextRun);
        timer.Dispose();
        Assert.Null(timer.NextRun);
        Assert.Throws<ObjectDisposedException>(() => timer.Expression = "0 * * * *");
        Assert.Equal(Utc(2026, 1, 1, 0, 1), timer.GetNextOccurrence(Utc(2026, 1, 1)));
    }

    [Fact]
    public async Task ExhaustedScheduleReturnsFalseAndCanBeRestarted()
    {
        var       clock = new FakeTimeProvider(Utc(2026, 12, 31, 23, 58));
        using var timer = new CronTimer("59 23 31 12 * 2026", clock, TimeZoneInfo.Utc, CronExpressionOptions.IncludeYear);
        var       wait  = timer.WaitForNextTickAsync();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await wait);
        Assert.Null(timer.NextRun);
        Assert.False(await timer.WaitForNextTickAsync());
        timer.Expression = "0 0 1 1 * 2027";
        wait             = timer.WaitForNextTickAsync();
        Assert.False(wait.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await wait);
        Assert.False(await timer.WaitForNextTickAsync());
    }

    [Fact]
    public async Task ChangingToAnImpossibleScheduleCompletesAnActiveWait()
    {
        var       clock = new FakeTimeProvider(Utc(2026, 1, 1));
        using var timer = new CronTimer("* * * * *", clock);
        var       wait  = timer.WaitForNextTickAsync();
        timer.Expression = "0 0 31 FEB *";
        Assert.False(await wait);
        Assert.False(await timer.WaitForNextTickAsync());
        timer.Expression = "* * * * *";
        wait             = timer.WaitForNextTickAsync();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await wait);
    }

    [Fact]
    public void ProviderLocalTimeZoneIsSnapshotted()
    {
        var zone  = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(5), "test", "test");
        var clock = new FakeTimeProvider(Utc(2026, 1, 1));
        clock.SetLocalTimeZone(zone);
        using var timer = new CronTimer("0 12 * * *", clock);
        Assert.Same(zone, timer.TimeZone);
        Assert.Equal(Utc(2026, 1, 1, 7), timer.NextRun);
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Assert.Same(zone, timer.TimeZone);
        Assert.Throws<ArgumentNullException>(() => new CronTimer("* * * * *", null!));
    }

    [Fact]
    public async Task CancellationDisposalAndTicksCanRaceWithoutHanging()
    {
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var       clock = new FakeTimeProvider(Utc(2026, 1, 1));
            using var timer = new CronTimer("* * * * *", clock);
            using var cts   = new CancellationTokenSource();
            var       wait  = timer.WaitForNextTickAsync(cts.Token).AsTask();
            await Task.WhenAll(Task.Run(cts.Cancel), Task.Run(timer.Dispose),
                Task.Run(() => clock.Advance(TimeSpan.FromMinutes(1)))).WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                await wait.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException error)
            {
                Assert.Equal(cts.Token, error.CancellationToken);
            }

            Assert.False(await timer.WaitForNextTickAsync());
        }
    }

    private sealed class TimerFixture : IDisposable
    {
        internal         FakeTimeProvider Clock { get; } = new(Utc(2026, 1, 1));
        private readonly CronTimer?       _cron;
        private readonly PeriodicTimer?   _periodic;

        internal TimerFixture(bool cron)
        {
            if (cron)
            {
                _cron = new CronTimer("* * * * * *", Clock, TimeZoneInfo.Utc, CronExpressionOptions.IncludeSeconds);
            }
            else
            {
                _periodic = new PeriodicTimer(TimeSpan.FromSeconds(1), Clock);
            }
        }

        public void Dispose()
        {
            _cron?.Dispose();
            _periodic?.Dispose();
        }

        internal ValueTask<bool> Wait(CancellationToken ct = default) =>
            _cron is not null ? _cron.WaitForNextTickAsync(ct) : _periodic!.WaitForNextTickAsync(ct);
    }
}
