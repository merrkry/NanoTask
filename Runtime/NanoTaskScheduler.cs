namespace NanoTasks;


internal class NanoTaskScheduler
{
    private static NanoTaskScheduler? s_global;
    internal static NanoTaskScheduler Global => s_global ??= new();

    private readonly Queue<Action> _queue = new();
    private bool _draining = false;

    public void Enqueue(Action continuation)
    {
        _queue.Enqueue(continuation);
    }

    public void Drain()
    {
        if (_draining)
        {
            return;
        }
        _draining = true;

        // Not for exception handling, but provide defer-like semantics for _draining.
        try
        {
            while (_queue.Count != 0)
            {
                _queue.Dequeue().Invoke();
            }
        }
        finally
        {
            _draining = false;
        }
    }
}