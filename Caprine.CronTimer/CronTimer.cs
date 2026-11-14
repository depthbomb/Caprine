namespace Caprine.CronTimer;

/// <summary>Provides an asynchronous, single-consumer timer driven by a cron expression.</summary>
/// <remarks>
///     <para>
///         Waiting, cancellation, tick coalescing, and disposal follow <see cref="PeriodicTimer" />.
///         Use <see cref="Expression" /> to change the schedule. There is no fixed-length period.
///     </para>
///     <para>
///         Times are evaluated in <see cref="TimeZone" />. Missing local times are skipped, and repeated
///         local times produce two occurrences. Delayed callbacks coalesce missed occurrences into one tick.
///     </para>
///     <para>
///         The timer checks the wall clock at least once per minute while scheduled. Clock changes
///         can therefore take up to a minute to be noticed. This is not a real-time scheduling guarantee.
///     </para>
/// </remarks>
public sealed partial class CronTimer : IDisposable
{
    /// <summary>Gets or sets the cron expression used for future ticks.</summary>
    /// <remarks>
    ///     Changing this property recalculates the next occurrence from the current time, using the same
    ///     <see cref="Options" /> and <see cref="TimeZone" />. Pending ticks are preserved. An invalid expression
    ///     leaves the schedule unchanged. A schedule with no future occurrence can be restarted by setting this property.
    ///     A clock or timer-provider failure is rethrown and faults pending and subsequent waits, unless a pending
    ///     wait has already completed. Dispose the timer after a provider failure.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="FormatException">The value is not valid for <see cref="Options" />.</exception>
    /// <exception cref="ObjectDisposedException">The timer has been disposed.</exception>
    public string Expression
    {
        get => _state.GetExpression().Expression;
        set => _state.ChangeExpression(new CronExpression(value, Options));
    }

    /// <summary>Gets the time zone used to evaluate the schedule.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>Gets the clock and timer provider used by this instance.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets the syntax extensions allowed when constructing or changing the expression.</summary>
    public CronExpressionOptions Options { get; }

    /// <summary>Gets the scheduled instant of the most recently signaled tick, or null before the first tick.</summary>
    /// <remarks>
    ///     If a callback is late, this is the most recent missed occurrence, not the callback's execution time.
    ///     The value is retained after changing the expression or disposing the timer.
    /// </remarks>
    public DateTimeOffset? LastRun => _state.GetLastRun();

    /// <summary>Gets the next scheduled instant, or null when disposed or the schedule has no future occurrence.</summary>
    /// <remarks>The value may be in the past while a due callback is waiting to execute.</remarks>
    public DateTimeOffset? NextRun => _state.GetNextRun();

    private readonly State _state;

    /// <summary>Creates an OCPS 1.0 timer using the system clock and local time zone.</summary>
    /// <param name="expression">A five-field OCPS 1.0 expression.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public CronTimer(string expression) : this(expression, TimeProvider.System, null) { }

    /// <summary>Creates an OCPS 1.0 timer using a supplied clock and its local time zone.</summary>
    /// <param name="expression">A five-field OCPS 1.0 expression.</param>
    /// <param name="timeProvider">The provider for the clock, timer, and default time zone.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public CronTimer(string expression, TimeProvider timeProvider) : this(expression, timeProvider, null) { }

    /// <summary>Creates a timer with explicit syntax extensions using the system clock and local time zone.</summary>
    /// <param name="expression">A cron expression interpreted using <paramref name="options" />.</param>
    /// <param name="options">Extensions to enable; use <see cref="CronExpressionOptions.None" /> for OCPS 1.0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options contain an undefined flag.</exception>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public CronTimer(string expression, CronExpressionOptions options) : this(expression, TimeProvider.System, null, options) { }

    /// <summary>Creates a timer using the system clock and a chosen time zone.</summary>
    /// <param name="expression">A cron expression interpreted using <paramref name="options" />.</param>
    /// <param name="timeZone">The evaluation time zone; null uses the system local time zone.</param>
    /// <param name="options">Explicit syntax extensions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options contain an undefined flag.</exception>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public CronTimer(string expression, TimeZoneInfo? timeZone, CronExpressionOptions options = CronExpressionOptions.None)
        : this(expression, TimeProvider.System, timeZone, options) { }

    /// <summary>Creates a timer with a supplied clock, time zone, and syntax options.</summary>
    /// <param name="expression">A cron expression interpreted using <paramref name="options" />.</param>
    /// <param name="timeProvider">The provider for the clock and timer.</param>
    /// <param name="timeZone">The evaluation time zone; null snapshots <paramref name="timeProvider" />'s local time zone.</param>
    /// <param name="options">Explicit syntax extensions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> or <paramref name="timeProvider" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options contain an undefined flag.</exception>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public CronTimer(string                expression,
                     TimeProvider          timeProvider,
                     TimeZoneInfo?         timeZone,
                     CronExpressionOptions options = CronExpressionOptions.None)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var parsed = new CronExpression(expression, options);

        TimeProvider = timeProvider;
        TimeZone     = timeZone ?? timeProvider.LocalTimeZone;
        Options      = options;
        _state       = new State(parsed, timeProvider, TimeZone);
    }

    /// <summary>Creates a timer from an already parsed or generated expression using the system clock.</summary>
    /// <param name="expression">An immutable expression that can be shared by multiple timers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    public CronTimer(CronExpression expression) : this(expression, TimeProvider.System, null) { }

    /// <summary>Creates a timer from an already parsed expression using a supplied clock.</summary>
    /// <param name="expression">The expression to reuse without parsing.</param>
    /// <param name="timeProvider">The clock, timer provider, and source of the default local time zone.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CronTimer(CronExpression expression, TimeProvider timeProvider) : this(expression, timeProvider, null) { }

    /// <summary>Creates a timer from an already parsed expression using the system clock and a chosen zone.</summary>
    /// <param name="expression">The expression to reuse without parsing.</param>
    /// <param name="timeZone">The evaluation time zone; null uses the system local time zone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    public CronTimer(CronExpression expression, TimeZoneInfo? timeZone) : this(expression, TimeProvider.System, timeZone) { }

    /// <summary>Creates a timer from an already parsed expression using a supplied clock and time zone.</summary>
    /// <param name="expression">The expression to reuse without parsing.</param>
    /// <param name="timeProvider">The provider for the clock and timer.</param>
    /// <param name="timeZone">The evaluation time zone; null snapshots the provider's local time zone.</param>
    /// <remarks>
    ///     The timer retains the expression's syntax options. Later assignments to <see cref="Expression" />
    ///     are parsed at runtime using those options and do not change the shared expression.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> or <paramref name="timeProvider" /> is null.</exception>
    public CronTimer(CronExpression expression, TimeProvider timeProvider, TimeZoneInfo? timeZone)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
        TimeZone     = timeZone ?? timeProvider.LocalTimeZone;
        Options      = expression.Options;

        _state       = new State(expression, timeProvider, TimeZone);
    }

    /// <summary>Releases the underlying timer if this instance is collected without being disposed.</summary>
    ~CronTimer()
    {
        _state?.Stop();
    }

    /// <summary>Waits for a tick, disposal, or exhaustion of the schedule.</summary>
    /// <param name="cancellationToken">Cancels this wait only. The timer continues running.</param>
    /// <returns>True for a tick; false when disposed or no future occurrence remains.</returns>
    /// <remarks>
    ///     <para>
    ///         Only one wait may be in flight, including a completed wait whose result has not been consumed.
    ///         Await each returned value once. Multiple ticks between waits coalesce into one tick.
    ///     </para>
    ///     <para>
    ///         Disposal voids an unconsumed tick. A token already canceled when the method is called produces
    ///         a canceled result even if the timer has been disposed, provided no other wait is active.
    ///     </para>
    ///     <para>Unexpected clock or timer-provider failures fault the current and subsequent waits.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Another wait is still in flight.</exception>
    /// <exception cref="OperationCanceledException">The returned value is awaited after cancellation wins the wait.</exception>
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        => _state.Wait(this, cancellationToken);

    /// <summary>Finds the first occurrence of the current expression strictly after an instant.</summary>
    /// <param name="baseTime">The exclusive lower bound.</param>
    /// <returns>The next occurrence in <see cref="TimeZone" />, or null when none is representable.</returns>
    /// <remarks>This query does not change the timer and remains available after disposal.</remarks>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset baseTime)
        => _state.GetExpression().GetNextOccurrence(baseTime, TimeZone);

    /// <summary>Finds the last occurrence of the current expression strictly before an instant.</summary>
    /// <param name="baseTime">The exclusive upper bound.</param>
    /// <returns>The previous occurrence in <see cref="TimeZone" />, or null when none is representable.</returns>
    /// <remarks>This query does not change the timer and remains available after disposal.</remarks>
    public DateTimeOffset? GetLastOccurrence(DateTimeOffset baseTime)
        => _state.GetExpression().GetLastOccurrence(baseTime, TimeZone);

    /// <summary>Stops the timer, discards pending ticks, and releases its resources.</summary>
    /// <remarks>
    ///     An active wait completes with false unless cancellation or a provider failure already won it.
    ///     Later waits return false unless given an already canceled token. Repeated calls are harmless.
    ///     This method may run concurrently with a wait or an expression change.
    /// </remarks>
    public void Dispose()
    {
        _state.Stop();
        GC.SuppressFinalize(this);
    }
}
