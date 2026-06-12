using System.Runtime.CompilerServices;

namespace NanoTask;

public struct NanoTaskMethodBuilder<TResult>
{
    public NanoTask<TResult> Task { get; }

    public static NanoTaskMethodBuilder<TResult> Create() => default;
    public void SetException(Exception exception) { }
    public void SetResult(TResult result) { }

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

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine { }

    public void SetStateMachine(IAsyncStateMachine stateMachine) {}
}
