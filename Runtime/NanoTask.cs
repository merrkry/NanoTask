using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NanoTasks;

internal readonly struct VoidResult { }

[AsyncMethodBuilder(typeof(NanoTaskMethodBuilder<>))]
public class NanoTask<TResult>
{
    private enum TaskState
    {
        Started,
        Completed,
        Faulted,
    }

    private TaskState _state;

    private TResult? _result;
    private Exception? _exception;
    private Action? _continuation;

    internal void SetResult(TResult result)
    {
        Debug.Assert(_state == TaskState.Started);

        _result = result;
        _state = TaskState.Completed;

        OnTermination();
    }

    internal void SetException(Exception exception)
    {
        Debug.Assert(_state == TaskState.Started);

        _exception = exception;
        _state = TaskState.Faulted;

        OnTermination();
    }

    private void OnTermination()
    {
        _continuation?.Invoke();
    }

    internal TResult GetResult()
    {
        Debug.Assert(_state != TaskState.Started);

        if (_state == TaskState.Faulted)
        {
            throw _exception!;
        }

        return _result!;
    }
}
