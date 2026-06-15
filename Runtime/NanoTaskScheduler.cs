using System.Collections.Concurrent;

namespace NanoTasks;


internal class NanoTaskScheduler
{
    private static NanoTaskScheduler? s_global;
    internal static NanoTaskScheduler Global => s_global ??= new();

    private readonly ConcurrentQueue<Action> _queue = new();

    /// <summary>
    /// If thread worker is already running on some thread.
    /// </summary>
    private int _running; // 0: false, 1: true

    /// <summary>
    /// Number of actions waiting to be invoked.
    /// </summary>
    private readonly SemaphoreSlim _signal = new(0);

    public void Post(Action action)
    {
        _queue.Enqueue(action);
        _signal.Release();
    }

    public TResult Run<TResult>(Func<NanoTask<TResult>> entry)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("Worker is already running on some thread, reentrant is forbidden");
        }
        // TODO: verify threadId for per-thread scheduler.

        try
        {
            var task = entry();
            PumpUntil(task);
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private void PumpUntil<TResult>(NanoTask<TResult> task)
    {
        while (!task.IsCompleted())
        {
            _signal.Wait();

            if (!_queue.TryDequeue(out var action))
            {
                throw new InvalidOperationException("Mismatch between signal and action");
            }

            action?.Invoke();
        }
    }
}