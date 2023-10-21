namespace Pulse.Throttlers;

/// <summary>
/// Throttler for parameterized synchronous actions.
/// </summary>
/// <typeparam name="TParameter">Type of the action's parameter.</typeparam>
/// <remarks>
///     When an invoking signal is received, the throttler starts to throttle and
///     ignores any signals received in the period. When the period is over, the action is invoked,
///     on the latest parameter received. Then the throttler returns to its initial state.
/// </remarks>
public class ActionThrottler<TParameter>
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Interval for the action.
    /// </summary>
    public required TimeSpan ActionInterval { private get; init; }

    /// <summary>
    /// Action to be invoked.
    /// </summary>
    public required Action<TParameter> Action { private get; init; }
#else
    /// <summary>
    /// Interval for the action.
    /// </summary>
    private TimeSpan ActionInterval { get; }
    
    /// <summary>
    /// Action to be invoked.
    /// </summary>
    private Action<TParameter> Action { get; }
#endif

    #endregion

    /// <summary>
    /// Timer for the throttler.
    /// </summary>
    private readonly Timer _throttleTimer;
    
    /// <summary>
    /// Access semaphore for the timer.
    /// </summary>
    private readonly SemaphoreSlim _timerSemaphore;
    
    /// <summary>
    /// Access semaphore for the action.
    /// </summary>
    private readonly SemaphoreSlim _actionSemaphore;
    
    /// <summary>
    /// Access semaphore for the throttler's state.
    /// </summary>
    private readonly SemaphoreSlim _stateSemaphore;
    
    /// <summary>
    /// Whether the throttler is in a throttling period.
    /// </summary>
    private bool _isThrottling;
    
    /// <summary>
    /// State machine for latest parameter received by the throttler.
    /// </summary>
    private readonly ParameterStateMachine<TParameter> _latestParameter;
    
#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a throttler for a parameterized synchronous action.
    /// </summary>
    public ActionThrottler()
    {
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
        _stateSemaphore = new(1);
        _latestParameter = new();
    }
#else
    /// <summary>
    /// Constructs a throttler for a parameterized synchronous action.
    /// </summary>
    /// <param name="actionInterval">Interval for the action.</param>
    /// <param name="action">Action to be invoked.</param>
    public ActionThrottler(TimeSpan actionInterval, Action<TParameter> action)
    {
        ActionInterval = actionInterval;
        Action = action;
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
        _stateSemaphore = new(1);
        _latestParameter = new();
    }
#endif

    /// <summary>
    /// Triggered when the throttler timer elapses.
    /// </summary>
    private void OnThrottleTimerElapsed(object? _)
    {
        if (!_isThrottling) return;
        _stateSemaphore.Wait();
        try
        {
            if (!_isThrottling) return;
            _isThrottling = false;
            _actionSemaphore.Wait();
            try
            {
                if (!_latestParameter.TryGetValue(out var parameter)) return;
                Action(parameter);
                _latestParameter.Invalidate();
            }
            finally
            {
                _actionSemaphore.Release();
            }
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends an invoking signal with the action's parameter to the throttler.
    /// </summary>
    /// <param name="parameter">Parameter to be passed to the action.</param>
    public void Invoke(TParameter parameter)
    {
        _actionSemaphore.Wait();
        try
        {
            _latestParameter.SetValue(parameter);
        }
        finally
        {
            _actionSemaphore.Release();
        }
        if (_isThrottling) return;
        _stateSemaphore.Wait();
        try
        {
            if (_isThrottling) return;
            _isThrottling = true;
            _timerSemaphore.Wait();
            try
            {
                _throttleTimer.Change(ActionInterval, Timeout.InfiniteTimeSpan);
            }
            finally
            {
                _timerSemaphore.Release();
            }
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }
}