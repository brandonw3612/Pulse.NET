namespace Pulse;

internal class ParameterStateMachine<T>
{
    private T? _value;
    private bool _isValid;
    private readonly object _lock = new();

    public void Invalidate() => _isValid = false;
    
    public void SetValue(T value)
    {
        lock (_lock)
        {
            _value = value;
            _isValid = true;
        }
    }
    
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