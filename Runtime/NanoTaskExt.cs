using System.Runtime.CompilerServices;

namespace NanoTasks;

internal sealed class NanoTaskExt<TResult, TStateMachine> : NanoTask<TResult>
// This whole optimization assumes NanoTask to be class and TStateMachien to be struct,
// but this doesn't seem to be part of async/await's spec, restrain struct here will make compilation fail.
where TStateMachine : IAsyncStateMachine
{
    internal TStateMachine StateMachine = default!;

    // Reuseable Action objects to avoid alloc on every onCompleted.


    /// <summary>
    /// Continuation action executed on current scheduler.
    /// </summary>
    internal readonly Action LocalContinuation;
    /// <summary>
    /// Continuation action executed on a foreign scheduler, e.g. NanoTaskScheduler on other threads or BCL ThreadPool.
    /// </summary>
    internal readonly Action ForeignContinuation;

    internal NanoTaskExt()
    {
        // These unavoidably introduces allocations, because OnCompleted expects an Action nontheless.
        LocalContinuation = MoveNext;
        var capturedTask = Scheduler;
        ForeignContinuation = () => capturedTask.Post(LocalContinuation);
    }

    // Use a wrapper to ensure the delegation binds to current class instead of StateMachine struct field.
    private void MoveNext()
    {
        StateMachine.MoveNext();
    }
}
