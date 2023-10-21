namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for parameterless synchronous actions.
/// </summary>
/// <remarks>When no invoking signal is received for a certain amount of time, the action is actually invoked.</remarks>
public class ActionDebouncer
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
    public required Action Action { private get; init; }
#else
    /// <summary>
    /// Timeout for the action.
    /// When no input signal is received for this amount of time, the action is actually invoked.
    /// </summary>
    private TimeSpan ActionTimeout { get; }

    /// <summary>
    /// Action to be invoked.
    /// </summary>
    private Action Action { get; }
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

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a parameterless synchronous action.
    /// </summary>
    public ActionDebouncer()
    {
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a debouncer for a parameterless synchronous action.
    /// </summary>
    /// <param name="actionTimeout">Timeout for the action.</param>
    /// <param name="action">Action to be invoked.</param>
    public ActionDebouncer(TimeSpan actionTimeout, Action action)
    {
        ActionTimeout = actionTimeout;
        Action = action;
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
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
            Action.Invoke();
        }
        finally
        {
            _actionSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends an invoking signal to the debouncer.
    /// </summary>
    public void Invoke()
    {
        _timerSemaphore.Wait();
        try
        {
            _debounceTimer.Change(ActionTimeout, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _timerSemaphore.Release();
        }
    }
}