namespace Pulse.Throttlers;

/// <summary>
/// Throttler for cumulative parameterized asynchronous tasks.
/// </summary>
/// <typeparam name="TParameter">Type of the parameter provided by the invoking signal.</typeparam>
/// <typeparam name="TParameterBatch">Type of the parameter batch provided to the task.</typeparam>
/// <remarks>
///     When an invoking signal is received, the throttler starts to throttle
///     and cumulates parameters of each coming invoking signal into the latest parameter batch,
///     without invoking the task. When the throttling period is over, the task is invoked,
///     on the latest parameter batch. Then the throttler resets the parameter batch and returns to its initial state.
/// </remarks>
public class AsyncCumulativeTaskThrottler<TParameter, TParameterBatch>
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Interval for the task.
    /// </summary>
    public required TimeSpan TaskInterval { private get; init; }

    /// <summary>
    /// Task to be invoked on the latest parameter batch.
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
    /// Interval for the task.
    /// </summary>
    private TimeSpan TaskInterval { get; }
    
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
    
    /// <summary>
    /// State machine for latest parameter batch cumulated in the throttler.
    /// </summary>
    private readonly ParameterStateMachine<TParameterBatch> _latestParameterBatch;

    /// <summary>
    /// Latest parameter batch cumulated in the throttler.
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
    /// Constructs a throttler for a cumulative parameterized asynchronous task.
    /// </summary>
    public AsyncCumulativeTaskThrottler()
    {
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _taskSemaphore = new(1);
        _stateSemaphore = new(1);
        _latestParameterBatch = new();
    }
#else
    /// <summary>
    /// Constructs a throttler for a cumulative parameterized asynchronous task.
    /// </summary>
    /// <param name="taskInterval">Interval for the task.</param>
    /// <param name="batchTask">Task to be invoked on the latest parameter batch.</param>
    /// <param name="cumulatingFunction">Function to cumulate the latest parameter into the latest parameter batch.</param>
    /// <param name="parameterBatchInitializer">Initializer for the parameter batch.</param>
    public AsyncCumulativeTaskThrottler(TimeSpan taskInterval, Func<TParameterBatch, Task> batchTask, Func<TParameterBatch, TParameter, TParameterBatch> cumulatingFunction, Func<TParameterBatch> parameterBatchInitializer)
    {
        TaskInterval = taskInterval;
        BatchTask = batchTask;
        CumulatingFunction = cumulatingFunction;
        ParameterBatchInitializer = parameterBatchInitializer;
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _taskSemaphore = new(1);
        _stateSemaphore = new(1);
        _latestParameterBatch = new();
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
            _taskSemaphore.Wait();
            try
            {
                if (!_latestParameterBatch.TryGetValue(out var parameter)) return;
                BatchTask(parameter);
                _latestParameterBatch.Invalidate();
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
    /// Sends an invoking signal with a parameter to be cumulated into the parameter batch.
    /// </summary>
    /// <param name="parameter">Parameter to be cumulated into the latest parameter batch.</param>
    public void Invoke(TParameter parameter)
    {
        _taskSemaphore.Wait();
        try
        {
            _latestParameterBatch.SetValue(CumulatingFunction(LatestParameterBatch, parameter));
        }
        finally
        {
            _taskSemaphore.Release();
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