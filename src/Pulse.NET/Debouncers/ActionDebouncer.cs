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
    /// Access lock for the timer.
    /// </summary>
    private readonly object _timerLock;
    
    /// <summary>
    /// Access lock for the action.
    /// </summary>
    private readonly object _actionLock;

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a parameterless synchronous action.
    /// </summary>
    public ActionDebouncer()
    {
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _timerLock = new();
        _actionLock = new();
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
        _timerLock = new();
        _actionLock = new();
    }
#endif

    /// <summary>
    /// Triggered when the debouncer timer elapses.
    /// </summary>
    private void OnDebounceTimerElapsed(object? _)
    {
        lock (_actionLock)
        {
            Action.Invoke();
        }
    }

    /// <summary>
    /// Sends an invoking signal to the debouncer.
    /// </summary>
    public void Invoke()
    {
        lock (_timerLock)
        {
            _debounceTimer.Change(ActionTimeout, Timeout.InfiniteTimeSpan);
        }
    }
}