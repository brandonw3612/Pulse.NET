namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for cumulative parameterized asynchronous tasks.
/// </summary>
/// <typeparam name="TParameter">Type of the parameter provided by the invoking signal.</typeparam>
/// <typeparam name="TParameterBatch">Type of the parameter batch provided to the task.</typeparam>
/// <remarks>
///     When no invoking signal is received for a certain amount of time, the task is actually invoked,
///     on the latest updated parameter batch.
/// </remarks>
public class AsyncCumulativeTaskDebouncer<TParameter, TParameterBatch> where TParameterBatch : notnull
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    public required TimeSpan TaskTimeout { private get; init; }
    
    /// <summary>
    /// Task to be invoked.
    /// </summary>
    public required Func<TParameterBatch, Task> BatchTask { private get; init; }
    
    /// <summary>
    /// Function to cumulate the latest parameter into the latest parameter batch.
    /// </summary>
    public required Func<TParameterBatch, TParameter, TParameterBatch> CumulatingFunction { private get; init; }
    
    /// <summary>
    /// Initializer for the parameter batch. Invoked after the task is invoked.
    /// </summary>
    public required Func<TParameterBatch> ParameterBatchInitializer { private get; init; }
#else
    /// <summary>
    /// Timeout for the task.
    /// </summary>
    private TimeSpan TaskTimeout { get; }

    /// <summary>
    /// Task to be invoked on the latest parameter batch.
    /// </summary>
    private Func<TParameterBatch, Task> BatchTask { get; }

    /// <summary>
    /// Function to cumulate the latest parameter into the latest parameter batch.
    /// </summary>
    private Func<TParameterBatch, TParameter, TParameterBatch> CumulatingFunction { get; }
    
    /// <summary>
    /// Initializer for the parameter batch. Invoked after the task is invoked.
    /// </summary>
    private Func<TParameterBatch> ParameterBatchInitializer { get; }
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
    /// Access semaphore lock for the task.
    /// </summary>
    private readonly SemaphoreSlim _taskSemaphore;
    
    /// <summary>
    /// State machine for latest parameter batch cumulated in the debouncer.
    /// </summary>
    private readonly ParameterStateMachine<TParameterBatch> _latestParameterBatch;

    /// <summary>
    /// Latest parameter batch cumulated in the debouncer.
    /// </summary>
    private TParameterBatch LatestParameterBatch
    {
        get
        {
            if (_latestParameterBatch.TryGetValue(out var batch)) return batch;
            _latestParameterBatch.SetValue(ParameterBatchInitializer());
            _latestParameterBatch.TryGetValue(out var batch2);
            return batch2;
        }
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a cumulative parameterized asynchronous task.
    /// </summary>
    public AsyncCumulativeTaskDebouncer()
    {
        _latestParameterBatch = new();
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerLock = new();
        _taskSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a debouncer for a cumulative parameterized asynchronous task.
    /// </summary>
    /// <param name="taskTimeout">Timeout for the task.</param>
    /// <param name="batchTask">Task to be invoked on the latest parameter batch.</param>
    /// <param name="cumulatingFunction">Function to cumulate the latest parameter into the latest parameter batch.</param>
    /// <param name="parameterBatchInitializer">Initializer for the parameter batch.</param>
    public AsyncCumulativeTaskDebouncer(TimeSpan taskTimeout, Func<TParameterBatch, Task> batchTask,
        Func<TParameterBatch, TParameter, TParameterBatch> cumulatingFunction, Func<TParameterBatch> parameterBatchInitializer)
    {
        TaskTimeout = taskTimeout;
        BatchTask = batchTask;
        ParameterBatchInitializer = parameterBatchInitializer;
        _latestParameterBatch = new();
        CumulatingFunction = cumulatingFunction;
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
            await BatchTask.Invoke(LatestParameterBatch);
            _latestParameterBatch.Invalidate();
        }
        finally
        {
            _taskSemaphore.Release();
        }
    }

    /// <summary>
    /// Invokes the debouncer with a parameter to be cumulated into the latest parameter batch.
    /// </summary>
    /// <param name="parameter">Parameter to be cumulated into the latest parameter batch.</param>
    public void Invoke(TParameter parameter)
    {
        lock (_timerLock)
        {
            _latestParameterBatch.SetValue(CumulatingFunction(LatestParameterBatch, parameter));
            _debounceTimer.Change(TaskTimeout, Timeout.InfiniteTimeSpan);
        }
    }
}