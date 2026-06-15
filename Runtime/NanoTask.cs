using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NanoTasks;

internal readonly struct VoidResult { }

[AsyncMethodBuilder(typeof(NanoTaskMethodBuilder<>))]
public class NanoTask<TResult>
{
    private static class TaskStatus
    {
        public const int Started = 0;
        public const int RanToCompletion = 1;
        public const int Faulted = 2;
    }

    private volatile int _status;

    private TResult? _result;
    private Exception? _exception;
    // TODO: lazy initialization -> inline storage -> heap alloc
    private List<Action>? _continuations = new();

    private readonly Lock _lock = new();

    internal readonly NanoTaskScheduler Scheduler;

    public NanoTask()
    {
        Scheduler = NanoTaskScheduler.Current;
    }

    internal void SetResult(TResult result)
    {
        _result = result;
        if (Interlocked.CompareExchange(ref _status, TaskStatus.RanToCompletion, TaskStatus.Started) != TaskStatus.Started)
        {
            throw new InvalidOperationException("Failed to switch to RanToCompletion");
        }

        OnTermination();
    }

    internal void SetException(Exception exception)
    {
        _exception = exception;
        if (Interlocked.CompareExchange(ref _status, TaskStatus.Faulted, TaskStatus.Started) != TaskStatus.Started)
        {
            throw new InvalidOperationException("Failed to switch to Faulted");
        }

        OnTermination();
    }

    private void OnTermination()
    {
        List<Action>? continuations;

        lock (_lock)
        {
            continuations = _continuations;
            _continuations = null;
        }

        if (continuations is null)
        {
            return;
        }

        foreach (var continuation in continuations)
        {
            Scheduler.Post(continuation);
        }
    }

    internal TResult GetResult()
    {
        var status = _status;
        Debug.Assert(status != TaskStatus.Started);

        if (status == TaskStatus.Faulted)
        {
            throw _exception!;
        }

        return _result!;
    }

    public NanoTaskAwaiter<TResult> GetAwaiter() => new NanoTaskAwaiter<TResult>(this);

    public bool IsCompleted() => _status is TaskStatus.RanToCompletion or TaskStatus.Faulted;

    public void RegisterContinuation(Action continuation)
    {
        bool postNow = false;

        lock (_lock)
        {
            // If we enter here BEFORE OnTermination() hit its critical zone, _continuations is guaranteed to be valid.
            // On the other hand, AFTER the critical zone, `IsCompleted` is guaranteed to return true and we do not access _continuations.
            if (IsCompleted())
            {
                postNow = true;
            }
            else
            {
                _continuations!.Add(continuation);
            }
        }

        if (postNow)
        {
            Scheduler.Post(continuation);
        }
    }
}
