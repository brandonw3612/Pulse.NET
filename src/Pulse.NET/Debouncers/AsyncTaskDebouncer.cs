namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for parameterless asynchronous tasks.
/// </summary>
/// <remarks>When no invoking signal is received for a certain amount of time, the action is actually invoked.</remarks>
public class AsyncTaskDebouncer
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    public required TimeSpan TaskTimeout { private get; init; }
    
    /// <summary>
    /// Task to be invoked.
    /// </summary>
    public required Func<Task> Task { private get; init; }
#else
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    private TimeSpan TaskTimeout { get; }

    /// <summary>
    /// Task to be invoked.
    /// </summary>
    private Func<Task> Task { get; }
#endif
    
    /// <summary>
    /// Timer for the debouncer.
    /// </summary>
    private readonly Timer _debounceTimer;
    
    /// <summary>
    /// Access lock for the timer.
    /// </summary>
    private readonly object _timerLock;
    
    /// <summary>
    /// Access semaphore lock for the task.
    /// </summary>
    private readonly SemaphoreSlim _taskSemaphore;

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a parameterless asynchronous task.
    /// </summary>
    public AsyncTaskDebouncer()
    {
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerLock = new();
        _taskSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a debouncer for a parameterless asynchronous task.
    /// </summary>
    /// <param name="taskTimeout">Timeout for the task.</param>
    /// <param name="task">Task to be invoked.</param>
    public AsyncTaskDebouncer(TimeSpan taskTimeout, Func<Task> task)
    {
        TaskTimeout = taskTimeout;
        Task = task;
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerLock = new();
        _taskSemaphore = new(1);
    }
#endif
    
    /// <summary>
    /// Triggered when the debouncer timer elapses.
    /// </summary>
    private async void OnDebounceTimerElapsed(object? _)
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

    /// <summary>
    /// Sends a invoking signal to the debouncer.
    /// </summary>
    public void Invoke()
    {
        lock (_timerLock)
        {
            _debounceTimer.Change(TaskTimeout, Timeout.InfiniteTimeSpan);
        }
    }
}