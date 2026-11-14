using System.Runtime.ExceptionServices;
using System.Threading.Tasks.Sources;

namespace Caprine.CronTimer;

public sealed partial class CronTimer
{
    // The reusable wait and lifetime design follows .NET's PeriodicTimer (MIT).
    // See THIRD-PARTY-NOTICES.txt for its copyright and license.
    private sealed class State : IValueTaskSource<bool>
    {
        private CronExpression                _expression;
        private DateTimeOffset?               _nextRun;
        private DateTimeOffset?               _lastRun;
        private CronTimer?                    _owner;
        private CancellationTokenRegistration _ctr;
        private ManualResetValueTaskSourceCore<bool> _source = new()
        {
            RunContinuationsAsynchronously = true,
        };
        private Exception? _failure;
        private bool       _stopped;
        private bool       _signaled;
        private bool       _activeWait;
        private bool       _tick;

        private readonly Lock         _gate = new();
        private readonly TimeProvider _clock;
        private readonly TimeZoneInfo _zone;
        private readonly ITimer       _timer;

        internal State(CronExpression expression, TimeProvider clock, TimeZoneInfo zone)
        {
            _expression = expression;
            _clock      = clock;
            _zone       = zone;
            _timer      = CreateTimer(clock, this);

            try
            {
                lock (_gate)
                {
                    var now = clock.GetUtcNow();
                    _nextRun = expression.GetNextOccurrence(now, zone);
                    Schedule(now);
                }
            }
            catch
            {
                _timer.Dispose();
                throw;
            }
        }

        public bool GetResult(short token)
        {
            // Waiting for an executing cancellation callback while holding _gate would deadlock.
            _ctr.Dispose();
            lock (_gate)
            {
                try
                {
                    var tick = _source.GetResult(token);
                    return tick && !_stopped;
                }
                finally
                {
                    _source.Reset();
                    _ctr        = default;
                    _activeWait = false;
                    _signaled   = false;
                    _owner      = null;
                }
            }
        }

        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

        public void OnCompleted(Action<object?>                 continuation,
                                object?                         state,
                                short                           token,
                                ValueTaskSourceOnCompletedFlags flags) => _source.OnCompleted(continuation, state, token, flags);

        internal CronExpression GetExpression()
        {
            lock (_gate)
            {
                return _expression;
            }
        }

        internal DateTimeOffset? GetNextRun()
        {
            lock (_gate)
            {
                return _nextRun;
            }
        }

        internal DateTimeOffset? GetLastRun()
        {
            lock (_gate)
            {
                return _lastRun;
            }
        }

        internal void ChangeExpression(CronExpression expression)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_stopped, typeof(CronTimer));

                if (_failure is not null)
                {
                    ExceptionDispatchInfo.Throw(_failure);
                }

                try
                {
                    var now  = _clock.GetUtcNow();
                    var next = expression.GetNextOccurrence(now, _zone);

                    _expression = expression;
                    _nextRun    = next;

                    Schedule(now);

                    if (next is null && _activeWait)
                    {
                        Signal(false);
                    }
                }
                catch (Exception error)
                {
                    Fail(error);
                    throw;
                }
            }
        }

        internal ValueTask<bool> Wait(CronTimer owner, CancellationToken ct)
        {
            lock (_gate)
            {
                if (_activeWait)
                {
                    throw new InvalidOperationException("Only one wait may be in flight at a time.");
                }

                if (ct.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled<bool>(ct);
                }

                if (_stopped)
                {
                    return new ValueTask<bool>(false);
                }

                if (_failure is not null)
                {
                    return ValueTask.FromException<bool>(_failure);
                }

                if (_signaled)
                {
                    _signaled = false;
                    return new ValueTask<bool>(_tick);
                }

                if (_nextRun is null)
                {
                    return new ValueTask<bool>(false);
                }

                // The callback keeps State alive. Root the owner only while a consumer waits,
                // so an abandoned idle timer can be finalized and release its native timer.
                _owner      = owner;
                _activeWait = true;
                _ctr        = ct.UnsafeRegister(static (state, token) => ((State)state!).Cancel(token), this);
                return new ValueTask<bool>(this, _source.Version);
            }
        }

        internal void Stop()
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _nextRun = null;
                Signal(false);
            }

            _timer.Dispose();
        }

        private void Cancel(CancellationToken ct)
        {
            lock (_gate)
            {
                Signal(false, new OperationCanceledException(ct));
            }
        }

        private void OnTick()
        {
            lock (_gate)
            {
                if (_stopped || _nextRun is null)
                {
                    return;
                }

                try
                {
                    var now = _clock.GetUtcNow();
                    if (now >= _nextRun.Value)
                    {
                        // Skip directly to the present after a late callback. Replaying old
                        // occurrences would create a catch-up loop despite tick coalescing.
                        _lastRun = _expression.GetOccurrenceAtOrBefore(now, _zone);
                        _nextRun = _expression.GetNextOccurrence(now, _zone);
                        Signal(true);
                    }

                    Schedule(now);
                }
                catch (Exception error)
                {
                    Fail(error);
                }
            }
        }

        private void Fail(Exception error)
        {
            _failure = error;
            _nextRun = null;
            Signal(false, error);
        }

        private void Signal(bool tick, Exception? error = null)
        {
            if (_signaled)
            {
                return;
            }

            _signaled = true;
            _tick     = tick;
            if (_activeWait)
            {
                if (error is null)
                {
                    _source.SetResult(tick);
                }
                else
                {
                    _source.SetException(error);
                }
            }
        }

        private void Schedule(DateTimeOffset now)
        {
            var delay = Timeout.InfiniteTimeSpan;
            if (_nextRun is { } next)
            {
                var ticks = Math.Clamp((next - now).Ticks, 1, TimeSpan.TicksPerMinute);
                // System timers truncate milliseconds. Round up so a sub-millisecond remainder
                // cannot cause an early tick or repeatedly schedule a zero-delay callback.
                delay = TimeSpan.FromMilliseconds((ticks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond);
            }

            if (!_timer.Change(delay, Timeout.InfiniteTimeSpan))
            {
                throw new ObjectDisposedException(nameof(ITimer));
            }
        }

        private static ITimer CreateTimer(TimeProvider clock, State state)
        {
            if (ExecutionContext.IsFlowSuppressed())
            {
                return clock.CreateTimer(
                    static value => ((State)value!).OnTick(), state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan
                );
            }

            using (ExecutionContext.SuppressFlow())
            {
                return clock.CreateTimer(
                    static value => ((State)value!).OnTick(), state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan
                );
            }
        }
    }
}
