using System.Runtime.CompilerServices;

namespace NanoTask;

public struct NanoTaskMethodBuilder<TResult>
{
    private NanoTask<TResult> _task;
    public NanoTask<TResult> Task => _task;

    public static NanoTaskMethodBuilder<TResult> Create()
    {
        return new NanoTaskMethodBuilder<TResult>
        {
            _task = new NanoTask<TResult>()
        };
    }
    public void SetException(Exception exception)
    {
        Task.SetException(exception);
    }
    public void SetResult(TResult result)
    {
        Task.SetResult(result);
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
    ref TAwaiter awaiter, ref TStateMachine stateMachine)
    where TAwaiter : INotifyCompletion
    where TStateMachine : IAsyncStateMachine
    { }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
    ref TAwaiter awaiter, ref TStateMachine stateMachine)
    where TAwaiter : ICriticalNotifyCompletion
    where TStateMachine : IAsyncStateMachine
    { }

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        stateMachine.MoveNext();
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine) {}
}
