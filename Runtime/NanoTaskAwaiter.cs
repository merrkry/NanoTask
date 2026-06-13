using System.Runtime.CompilerServices;

namespace NanoTasks;

public readonly struct NanoTaskAwaiter<TResult> : ICriticalNotifyCompletion
{
    private readonly NanoTask<TResult> _task;

    internal NanoTaskAwaiter(NanoTask<TResult> task)
    {
        _task = task;
    }

    public void UnsafeOnCompleted(Action continuation)
    {
        _task.RegisterContinuation(continuation);
    }

    // Not particularly meaningful under our single-threaded assumption.
    public void OnCompleted(Action continuation)
    {
        UnsafeOnCompleted(continuation);
    }

    public bool IsCompleted => _task.IsCompleted();
    public TResult GetResult() => _task.GetResult();
}