namespace DuckRun.Core.Internal.Execution;

internal sealed class ConcurrencyTracker
{
    private int _current;
    private readonly int _max;

    public ConcurrencyTracker(int max)
    {
        _max = max <= 0 ? int.MaxValue : max;
    }

    public int InFlight => Volatile.Read(ref _current);
    public int Max => _max;

    public bool TryAcquire()
    {
        while (true)
        {
            var c = Volatile.Read(ref _current);
            if (c >= _max) return false;
            if (Interlocked.CompareExchange(ref _current, c + 1, c) == c) return true;
        }
    }

    public void Release() => Interlocked.Decrement(ref _current);
}
