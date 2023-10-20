namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for parameterized asynchronous tasks.
/// </summary>
/// <typeparam name="TParameter">Type of the task's parameter.</typeparam>
/// <remarks>
///     When no invoking signal is received for a certain amount of time, the task is actually invoked,
///     on the latest parameter received.
/// </remarks>
public class AsyncTaskDebouncer<TParameter> where TParameter : notnull
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    public required TimeSpan TaskTimeout { private get; init; }

    /// <summary>
    /// Task to be invoked.
    /// </summary>
    public required Func<TParameter, Task> Task { private get; init; }
#else
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    private TimeSpan TaskTimeout { get; }
    
    /// <summary>
    /// Task to be invoked.
    /// </summary>
    private Func<TParameter, Task> Task { get; }
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
    
    /// <summary>
    /// State machine for latest parameter received by the debouncer.
    /// </summary>
    private readonly ParameterStateMachine<TParameter> _latestParameter;
    
#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a parameterized asynchronous task.
    /// </summary>
    public AsyncTaskDebouncer()
    {
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerLock = new();
        _taskSemaphore = new(1);
        _latestParameter = new();
    }
#else
    /// <summary>
    /// Constructs a debouncer for a parameterized asynchronous task.
    /// </summary>
    /// <param name="taskTimeout">Timeout for the task.</param>
    /// <param name="task">Task to be invoked.</param>
    public AsyncTaskDebouncer(TimeSpan taskTimeout, Func<TParameter, Task> task)
    {
        TaskTimeout = taskTimeout;
        Task = task;
        _latestParameter = new();
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
            if (!_latestParameter.TryGetValue(out var parameter)) return;
            await Task(parameter);
        }
        finally
        {
            _taskSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends an invoking signal to the debouncer with the task's parameter.
    /// </summary>
    /// <param name="parameter">Parameter to be passed to the task.</param>
    public void Invoke(TParameter parameter)
    {
        lock (_timerLock)
        {
            _latestParameter.SetValue(parameter);
            _debounceTimer.Change(TaskTimeout, Timeout.InfiniteTimeSpan);
        }
    }
}