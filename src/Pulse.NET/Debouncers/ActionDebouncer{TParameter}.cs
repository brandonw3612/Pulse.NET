namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for parameterized synchronous actions.
/// </summary>
/// <typeparam name="TParameter">Type of the action's parameter.</typeparam>
/// <remarks>
///     When no invoking signal is received for a certain amount of time, the action is actually invoked,
///     on the latest parameter received.
/// </remarks>
public class ActionDebouncer<TParameter>
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Timeout for the action.
    /// </summary>
    public required TimeSpan ActionTimeout { private get; init; }
    
    /// <summary>
    /// Action to be invoked.
    /// </summary>
    public required Action<TParameter> Action { private get; init; }
#else
    /// <summary>
    /// Timeout for the action.
    /// </summary>
    private TimeSpan ActionTimeout { get; }

    /// <summary>
    /// Action to be invoked.
    /// </summary>
    private Action<TParameter> Action { get; }
#endif

    #endregion

    /// <summary>
    /// Timer for the debouncer.
    /// </summary>
    private readonly Timer _debounceTimer;
    
    /// <summary>
    /// Access semaphore for the timer.
    /// </summary>
    private readonly SemaphoreSlim _timerSemaphore;
    
    /// <summary>
    /// Access semaphore for the action.
    /// </summary>
    private readonly SemaphoreSlim _actionSemaphore;
    
    /// <summary>
    /// State machine for latest parameter received by the debouncer.
    /// </summary>
    private readonly ParameterStateMachine<TParameter> _latestParameter;

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a parameterized synchronous action.
    /// </summary>
    public ActionDebouncer()
    {
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
        _latestParameter = new();
    }
#else
    /// <summary>
    /// Constructs a debouncer for a parameterized synchronous action.
    /// </summary>
    /// <param name="actionTimeout">Timeout for the action.</param>
    /// <param name="action">Action to be invoked.</param>
    public ActionDebouncer(TimeSpan actionTimeout, Action<TParameter> action)
    {
        ActionTimeout = actionTimeout;
        Action = action;
        _latestParameter = new();
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
    }
#endif
    
    /// <summary>
    /// Triggered when the debouncer timer elapses.
    /// </summary>
    private void OnDebounceTimerElapsed(object? _)
    {
        _actionSemaphore.Wait();
        try
        {
            if (!_latestParameter.TryGetValue(out var parameter)) return;
            Action.Invoke(parameter);
            _latestParameter.Invalidate();
        }
        finally
        {
            _actionSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends an invoking signal with the action's parameter to the debouncer.
    /// </summary>
    /// <param name="parameter">Parameter to be passed to the action.</param>
    public void Invoke(TParameter parameter)
    {
        _timerSemaphore.Wait();
        try
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
            _debounceTimer.Change(ActionTimeout, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _timerSemaphore.Release();
        }
    }
}