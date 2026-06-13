using System.Runtime.CompilerServices;

namespace NanoTasks;

internal sealed class NanoTaskExt<TResult, TStateMachine> : NanoTask<TResult>
// This whole optimization assumes NanoTask to be class and TStateMachien to be struct,
// but this doesn't seem to be part of async/await's spec, restrain struct here will make compilation fail.
where TStateMachine : IAsyncStateMachine
{
    internal TStateMachine StateMachine = default!;
    // Re-use Action object to avoid alloc on every onCompleted.
    internal readonly Action ContinuationAction;

    internal NanoTaskExt()
    {
        // This unavoidably introduces allocations, because OnCompleted expects an Action nontheless.
        ContinuationAction = MoveNext;
    }

    // Use a wrapper to ensure the delegation binds to current class instead of StateMachine struct field.
    private void MoveNext()
    {
        StateMachine.MoveNext();
    }
}
