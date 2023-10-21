namespace Pulse.Debouncers;

/// <summary>
/// Debouncer for cumulative parameterized synchronous actions.
/// </summary>
/// <typeparam name="TParameter">Type of the parameter provided by the invoking signal.</typeparam>
/// <typeparam name="TParameterBatch">Type of the parameter batch provided to the action.</typeparam>
public class CumulativeActionDebouncer<TParameter, TParameterBatch>
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// Timeout for the action.
    /// </summary>
    public required TimeSpan ActionTimeout { private get; init; }

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
    /// Timeout for the action.
    /// </summary>
    private TimeSpan ActionTimeout { get; }
    
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
            _latestParameterBatch.SetValue(ParameterBatchInitializer.Invoke());
            _latestParameterBatch.TryGetValue(out var batch2);
            return batch2;
        }
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a debouncer for a cumulative parameterized synchronous action.
    /// </summary>
    public CumulativeActionDebouncer()
    {
        _latestParameterBatch = new();
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timerSemaphore = new(1);
        _actionSemaphore = new(1);
    }
#else
    /// <summary>
    /// Constructs a debouncer for a cumulative parameterized synchronous action.
    /// </summary>
    /// <param name="actionTimeout">Timeout for the action.</param>
    /// <param name="batchAction">Action to be invoked on the latest parameter batch.</param>
    /// <param name="cumulatingFunction">Function to cumulate the latest parameter into the latest parameter batch.</param>
    /// <param name="parameterBatchInitializer">Initializer for the parameter batch.</param>
    public CumulativeActionDebouncer(TimeSpan actionTimeout, Action<TParameterBatch> batchAction,
        Func<TParameterBatch, TParameter, TParameterBatch> cumulatingFunction, Func<TParameterBatch> parameterBatchInitializer)
    {
        ActionTimeout = actionTimeout;
        BatchAction = batchAction;
        ParameterBatchInitializer = parameterBatchInitializer;
        _latestParameterBatch = new();
        CumulatingFunction = cumulatingFunction;
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
            if (!_latestParameterBatch.TryGetValue(out var parameter)) return;
            BatchAction.Invoke(parameter);
            _latestParameterBatch.Invalidate();
        }
        finally
        {
            _actionSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends an invoking signal with a parameter to be cumulated into the parameter batch.
    /// </summary>
    /// <param name="parameter">Parameter to be cumulated into the latest parameter batch.</param>
    public void Invoke(TParameter parameter)
    {
        _timerSemaphore.Wait();
        try
        {
            _latestParameterBatch.SetValue(CumulatingFunction.Invoke(LatestParameterBatch, parameter));
            _debounceTimer.Change(ActionTimeout, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _timerSemaphore.Release();
        }
    }
}