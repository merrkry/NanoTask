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
        throw new NotImplementedException();
    }

    public void OnCompleted(Action continuation)
    {
        throw new NotImplementedException();
    }

    public bool IsCompleted => _task.IsCompleted();
    public TResult GetResult() => _task.GetResult();
}