namespace Pulse.Throttlers;

/// <summary>
/// Throttler for parameterless asynchronous tasks.
/// </summary>
/// <remarks>
///     When an invoking signal is received, the throttler starts to throttle and
///     ignores any signals received in the period. When the period is over, the task is invoked.
///     Then the throttler returns to its initial state.
/// </remarks>
public class AsyncTaskThrottler
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Interval for the task.
    /// </summary>
    public required TimeSpan TaskInterval { private get; init; }
    
    /// <summary>
    /// Task to be invoked.
    /// </summary>
    public required Func<Task> Task { private get; init; }
    
    /// <summary>
    /// Whether the task is instantly invoked before entering a throttling period.
    /// </summary>
    public required bool IsInstantaneous { private get; init; }
#else
    /// <summary>
    /// Interval for the task.
    /// </summary>
    private TimeSpan TaskInterval { get; }
    
    /// <summary>
    /// Task to be invoked.
    /// </summary>
    private Func<Task> Task { get; }
    
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
    /// Access semaphore for the task.
    /// </summary>
    private readonly SemaphoreSlim _taskSemaphore;
    
    /// <summary>
    /// Access semaphore for the throttler's state.
    /// </summary>
    private readonly SemaphoreSlim _stateSemaphore;
    
    /// <summary>
    /// Whether the throttler is in a throttling period.
    /// </summary>
    private bool _isThrottling;
    
#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a throttler for a parameterless asynchronous task.
    /// </summary>
    public AsyncTaskThrottler()
    {
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _taskSemaphore = new(1);
        _stateSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a throttler for a parameterless asynchronous task.
    /// </summary>
    /// <param name="taskInterval">Interval for the task.</param>
    /// <param name="task">Task to be invoked.</param>
    /// <param name="isInstantaneous">Whether the task is instantly invoked before entering a throttling period.</param>
    public AsyncTaskThrottler(TimeSpan taskInterval, Func<Task> task, bool isInstantaneous = false)
    {
        TaskInterval = taskInterval;
        Task = task;
        IsInstantaneous = isInstantaneous;
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _taskSemaphore = new(1);
        _stateSemaphore = new(1);
    }
#endif
    
    /// <summary>
    /// Triggered when the throttler timer elapses.
    /// </summary>
    private async void OnThrottleTimerElapsed(object? _)
    {
        if (!_isThrottling) return;
        await _stateSemaphore.WaitAsync();
        try
        {
            if (!_isThrottling) return;
            _isThrottling = false;
            await _taskSemaphore.WaitAsync();
            try
            {
                await Task();
            }
            finally
            {
                _taskSemaphore.Release();
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
    public async Task InvokeAsync()
    {
        if (_isThrottling) return;
        if (IsInstantaneous)
        {
            await _taskSemaphore.WaitAsync();
            try
            {
                await Task();
            }
            finally
            {
                _taskSemaphore.Release();
            }
        }
        await _stateSemaphore.WaitAsync();
        try
        {
            if (_isThrottling) return;
            _isThrottling = true;
            await _timerSemaphore.WaitAsync();
            try
            {
                _throttleTimer.Change(TaskInterval, Timeout.InfiniteTimeSpan);
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