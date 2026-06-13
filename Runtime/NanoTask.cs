using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NanoTasks;

internal readonly struct VoidResult { }

[AsyncMethodBuilder(typeof(NanoTaskMethodBuilder<>))]
public class NanoTask<TResult>
{
    private enum TaskStatus
    {
        Started,
        RanToCompletion,
        Faulted,
    }

    private TaskStatus _status;

    private TResult? _result;
    private Exception? _exception;
    // TODO: lazy initialization -> inline storage -> heap alloc
    private List<Action> _continuations = new();

    internal void SetResult(TResult result)
    {
        Debug.Assert(_status == TaskStatus.Started);

        _result = result;
        _status = TaskStatus.RanToCompletion;

        OnTermination();
    }

    internal void SetException(Exception exception)
    {
        Debug.Assert(_status == TaskStatus.Started);

        _exception = exception;
        _status = TaskStatus.Faulted;

        OnTermination();
    }

    private void OnTermination()
    {
        foreach(var continuation in _continuations)
        {
            continuation.Invoke();
        }
    }

    internal TResult GetResult()
    {
        Debug.Assert(_status != TaskStatus.Started);

        if (_status == TaskStatus.Faulted)
        {
            throw _exception!;
        }

        return _result!;
    }

    public NanoTaskAwaiter<TResult> GetAwaiter() => new(this);

    public bool IsCompleted() => _status is TaskStatus.RanToCompletion or TaskStatus.Faulted;

    public void RegisterContinuation(Action continuation)
    {
        _continuations.Add(continuation);
    }
}
