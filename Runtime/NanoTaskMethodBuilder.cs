using System.Runtime.CompilerServices;

namespace NanoTasks;

public struct NanoTaskMethodBuilder<TResult>
{
    private NanoTask<TResult>? _taskObject;
    public readonly NanoTask<TResult> Task
    {
        get
        {
            if (_taskObject is null)
            {
                throw new InvalidOperationException("Task is not initialized before result/exception or continuation is configured");
            }
            return _taskObject!;
        }
    }

    public static NanoTaskMethodBuilder<TResult> Create() => new();
    public void SetException(Exception exception)
    {
        PrepareResultTask();
        Task.SetException(exception);
    }
    public void SetResult(TResult result)
    {
        PrepareResultTask();
        Task.SetResult(result);
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
    ref TAwaiter awaiter, ref TStateMachine stateMachine)
    where TAwaiter : INotifyCompletion
    where TStateMachine : IAsyncStateMachine
    {
        var continuation = PrepareStateMachineContinuation(ref awaiter, ref stateMachine);
        awaiter.OnCompleted(continuation);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
    ref TAwaiter awaiter, ref TStateMachine stateMachine)
    where TAwaiter : ICriticalNotifyCompletion
    where TStateMachine : IAsyncStateMachine
    {
        var continuation = PrepareStateMachineContinuation(ref awaiter, ref stateMachine);
        awaiter.UnsafeOnCompleted(continuation);
    }

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        stateMachine.MoveNext();
    }

    private Action PrepareStateMachineContinuation<TAwaiter, TStateMachine>(
    ref TAwaiter awaiter, ref TStateMachine stateMachine)
    where TAwaiter : INotifyCompletion
    where TStateMachine : IAsyncStateMachine
    {
        // The order of assignments is critial here.
        // When boxing stateMachine, we are copying its builder field, which holds a ref to task object.
        // - we cannot access copied builder field of (boxed) stateMachine, the update of this ref must be done here before copying.
        // - We optimize stateMachine's boxing by making it an inline member of task. 
        // Therefore we must follow this order:
        // 1. Construct task
        // 2. update _task field
        // 3. Box/assign stateMachine
        var task = GetOrCreateSuspendedTask<TStateMachine>();
        task.StateMachine = stateMachine;

        // We cannot check awaiter's type and access its Scheduler field without introducing boxing.
        // For now we treat all continuation as foreign to ensure correctness.
        return task.ForeignContinuation;
    }

    private NanoTaskExt<TResult, TStateMachine> GetOrCreateSuspendedTask<TStateMachine>() where TStateMachine : IAsyncStateMachine
    {
        if (_taskObject is NanoTaskExt<TResult, TStateMachine> existing)
        {
            return existing;
        }

        if (_taskObject is not null)
        {
            throw new InvalidOperationException("Cannot upgrade an already-created NanoTask");
        }

        var task = new NanoTaskExt<TResult, TStateMachine>();
        _taskObject = task;
        return task;
    }

    private void PrepareResultTask()
    {
        if (_taskObject is null)
        {
            _taskObject = new NanoTask<TResult>();
        }
    }

    // Marked as legacy in dotnet runtime.
    public void SetStateMachine(IAsyncStateMachine stateMachine) { }
}
