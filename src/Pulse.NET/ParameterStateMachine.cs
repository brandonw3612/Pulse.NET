namespace Pulse;

/// <summary>
/// State machine for action/task parameters used in this library, maintaining its state of validity.
/// </summary>
/// <typeparam name="T">Type of the parameter.</typeparam>
internal class ParameterStateMachine<T>
{
    /// <summary>
    /// Real value of the parameter.
    /// </summary>
    private T? _value;
    
    /// <summary>
    /// Whether the parameter is currently set as valid.
    /// </summary>
    private bool _isValid;
    
    /// <summary>
    /// Access lock for the parameter.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Invalidates the parameter.
    /// </summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _isValid = false;
        }
    }
    
    /// <summary>
    /// Sets the value of the parameter.
    /// </summary>
    /// <param name="value"></param>
    public void SetValue(T value)
    {
        lock (_lock)
        {
            _value = value;
            _isValid = true;
        }
    }
    
    /// <summary>
    /// Tries to get the value of the parameter.
    /// </summary>
    /// <param name="value">If the method succeeded, the value of the parameter.</param>
    /// <returns>Whether current parameter is valid.</returns>
    public bool TryGetValue(out T value)
    {
        lock (_lock)
        {
            if (_isValid)
            {
                value = _value!;
                return true;
            }
            value = default!;
            return false;
        }
    }
}