namespace Pulse.Throttlers;

/// <summary>
/// Throttler for cumulative parameterized synchronous actions.
/// </summary>
/// <typeparam name="TParameter">Type of the parameter provided by the invoking signal.</typeparam>
/// <typeparam name="TParameterBatch">Type of the parameter batch provided to the action.</typeparam>
/// <remarks>
///     When an invoking signal is received, the throttler starts to throttle
///     and cumulates parameters of each coming invoking signal into the latest parameter batch,
///     without invoking the task. When the throttling period is over, the action is invoked,
///     on the latest parameter batch. Then the throttler resets the parameter batch and returns to its initial state.
/// </remarks>
public class CumulativeActionThrottler<TParameter, TParameterBatch>
{
    #region User-specified fields

#if NET7_0_OR_GREATER
    /// <summary>
    /// Interval for the action.
    /// </summary>
    public required TimeSpan ActionInterval { private get; init; }
    
    /// <summary>
    /// Action to be invoked on the latest parameter batch.
    /// </summary>
    public required Action<TParameterBatch> BatchAction { private get; init; }
    
    /// <summary>
    /// Function to cumulate the latest parameter into the latest parameter batch.
    /// </summary>
    public required Func<TParameterBatch, TParameter, TParameterBatch> CumulatingFunction { private get; init; }
    
    /// <summary>
    /// Initializer for the parameter batch. Invoked after the action is invoked.
    /// </summary>
    public required Func<TParameterBatch> ParameterBatchInitializer { private get; init; }
#else
    /// <summary>
    /// Interval for the action.
    /// </summary>
    private TimeSpan ActionInterval { get; }

    /// <summary>
    /// Action to be invoked on the latest parameter batch.
    /// </summary>
    private Action<TParameterBatch> BatchAction { get; }

    /// <summary>
    /// Function to cumulate the latest parameter into the latest parameter batch.
    /// </summary>
    private Func<TParameterBatch, TParameter, TParameterBatch> CumulatingFunction { get; }

    /// <summary>
    /// Initializer for the parameter batch. Invoked after the action is invoked.
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
    /// Constructs a throttler for a cumulative parameterized synchronous action.
    /// </summary>
    public CumulativeActionThrottler()
    {
        _throttleTimer = new(OnThrottleTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _taskSemaphore = new(1);
        _stateSemaphore = new(1);
        _latestParameterBatch = new();
    }
#else
    /// <summary>
    /// Constructs a throttler for a cumulative parameterized synchronous action.
    /// </summary>
    /// <param name="actionInterval">Interval for the action.</param>
    /// <param name="batchAction">Action to be invoked on the latest parameter batch.</param>
    /// <param name="cumulatingFunction">Function to cumulate the latest parameter into the latest parameter batch.</param>
    /// <param name="parameterBatchInitializer">Initializer for the parameter batch.</param>
    public CumulativeActionThrottler(TimeSpan actionInterval, Action<TParameterBatch> batchAction,
        Func<TParameterBatch, TParameter, TParameterBatch> cumulatingFunction,
        Func<TParameterBatch> parameterBatchInitializer)
    {
        ActionInterval = actionInterval;
        BatchAction = batchAction;
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
                if (!_latestParameterBatch.TryGetValue(out var parameterBatch)) return;
                BatchAction.Invoke(parameterBatch);
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
        _timerSemaphore.Wait();
        try
        {
            if (_isThrottling) return;
            _isThrottling = true;
            _throttleTimer.Change(ActionInterval, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _timerSemaphore.Release();
        }
    }
}