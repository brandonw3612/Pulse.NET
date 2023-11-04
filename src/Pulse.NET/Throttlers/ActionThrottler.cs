namespace Pulse.Throttlers;

/// <summary>
/// Throttler for parameterless synchronous actions.
/// </summary>
/// <remarks>
///     When an invoking signal is received, the throttler starts to throttle and
///     ignores any signals received in the period. When the period is over, the action is invoked.
///     Then the throttler returns to its initial state.
/// </remarks>
public class ActionThrottler
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
    public required Action Action { private get; init; }
    
    /// <summary>
    /// Whether the task is instantly invoked before entering a throttling period.
    /// </summary>
    public required bool IsInstantaneous { private get; init; }
#else
    /// <summary>
    /// Interval for the action.
    /// </summary>
    private TimeSpan ActionInterval { get; }
    
    /// <summary>
    /// Action to be invoked.
    /// </summary>
    private Action Action { get; }
    
    /// <summary>
    /// Whether the task is instantly invoked before entering a throttling period.
    /// </summary>
    private bool IsInstantaneous { get; }
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
    /// Access semaphore for the throttling state.
    /// </summary>
    private readonly SemaphoreSlim _stateSemaphore;
    
    /// <summary>
    /// Access semaphore for the action.
    /// </summary>
    private readonly SemaphoreSlim _actionSemaphore;
    
    /// <summary>
    /// Whether the throttler is in a throttling period.
    /// </summary>
    private bool _isThrottling;

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a throttler for a parameterless synchronous action.
    /// </summary>
    public ActionThrottler()
    {
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
        _stateSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a throttler for a parameterless synchronous action.
    /// </summary>
    /// <param name="actionInterval">Interval for the action.</param>
    /// <param name="action">Action to be invoked.</param>\
    /// <param name="isInstantaneous">Whether the action is instantly invoked before entering a throttling period.</param>
    public ActionThrottler(TimeSpan actionInterval, Action action, bool isInstantaneous = false)
    {
        ActionInterval = actionInterval;
        Action = action;
        IsInstantaneous = isInstantaneous;
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
        _stateSemaphore = new(1);
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
            if (IsInstantaneous) return;
            _actionSemaphore.Wait();
            try
            {
                Action();
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
    /// Sends an invoking signal to the throttler.
    /// </summary>
    public void Invoke()
    {
        if (_isThrottling) return;
        if (IsInstantaneous)
        {
            _actionSemaphore.Wait();
            try
            {
                Action();
            }
            finally
            {
                _actionSemaphore.Release();
            }
        }
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