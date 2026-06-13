using NanoTasks;

namespace NanoTasks.Tests;

public sealed class StageTests
{
    /// <summary>
    /// An async NanoTask method that does not suspend completes synchronously and stores its returned value.
    /// </summary>
    [Fact]
    public void SyncAsyncMethod_CompletesWithResult()
    {
        async NanoTask<int> Return42Async()
        {
            return 42;
        }

        var task = Return42Async();

        Assert.Equal(42, task.GetResult());
    }

    /// <summary>
    /// An async NanoTask method that throws before suspension completes as faulted and rethrows when observed.
    /// </summary>
    [Fact]
    public void SyncAsyncMethod_CapturesException()
    {
        async NanoTask<int> ThrowAsync()
        {
            throw new InvalidOperationException("boom");
        }

        var task = ThrowAsync();

        var exception = Assert.Throws<InvalidOperationException>(() => task.GetResult());
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// NanoTask exposes the awaiter pattern so the C# compiler can await it.
    /// </summary>
    [Fact]
    public void AwaiterSurface_ExistsOnNanoTask()
    {
        var getAwaiter = typeof(NanoTask<int>).GetMethod("GetAwaiter");

        Assert.NotNull(getAwaiter);

        var awaiterType = getAwaiter.ReturnType;
        Assert.NotNull(awaiterType.GetProperty("IsCompleted"));
        Assert.NotNull(awaiterType.GetMethod("GetResult", Type.EmptyTypes));
        Assert.NotNull(awaiterType.GetMethod("OnCompleted", [typeof(Action)]));
        Assert.NotNull(awaiterType.GetMethod("UnsafeOnCompleted", [typeof(Action)]));
    }

    /// <summary>
    /// Awaiting an already-completed NanoTask uses the completed fast path without registering a continuation.
    /// </summary>
    [Fact]
    public void CompletedNanoTask_CanBeAwaitedByAnotherNanoTask()
    {
        async NanoTask<int> CompletedAsync()
        {
            return 42;
        }

        async NanoTask<int> AwaitCompletedAsync()
        {
            var value = await CompletedAsync();
            return value + 1;
        }

        var task = AwaitCompletedAsync();

        Assert.Equal(43, task.GetResult());
    }

    /// <summary>
    /// Awaiting a faulted NanoTask rethrows the stored exception from awaiter.GetResult().
    /// </summary>
    [Fact]
    public void FaultedNanoTask_RethrowsWhenAwaitedByAnotherNanoTask()
    {
        async NanoTask<int> FaultedAsync()
        {
            throw new InvalidOperationException("boom");
        }

        async NanoTask<int> AwaitFaultedAsync()
        {
            return await FaultedAsync();
        }

        var task = AwaitFaultedAsync();

        var exception = Assert.Throws<InvalidOperationException>(() => task.GetResult());
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// OnCompleted registers a continuation without running it inline.
    /// </summary>
    [Fact]
    public void IncompleteAwaiter_OnCompletedDoesNotRunContinuationInline()
    {
        var task = new NanoTask<int>();
        var awaiter = task.GetAwaiter();
        var continuationCalls = 0;

        awaiter.OnCompleted(() => continuationCalls++);

        Assert.Equal(0, continuationCalls);
    }

    /// <summary>
    /// UnsafeOnCompleted registers a continuation without running it inline.
    /// </summary>
    [Fact]
    public void IncompleteAwaiter_UnsafeOnCompletedDoesNotRunContinuationInline()
    {
        var task = new NanoTask<int>();
        var awaiter = task.GetAwaiter();
        var continuationCalls = 0;

        awaiter.UnsafeOnCompleted(() => continuationCalls++);

        Assert.Equal(0, continuationCalls);
    }

    /// <summary>
    /// AwaitUnsafeOnCompleted registers the current state machine continuation when the awaited operation is incomplete.
    /// </summary>
    [Fact]
    public void IncompleteAwaiter_RegistersContinuationWithoutCompletingOuterTask()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> AwaitIncompleteAsync()
        {
            var value = await inner;
            return value + 1;
        }

        var outer = AwaitIncompleteAsync();

        Assert.False(outer.IsCompleted());
    }

    /// <summary>
    /// A scheduler will eventually resume a suspended state machine after the awaited NanoTask completes.
    /// </summary>
    [Fact(Skip = "Missing: scheduler queue to run registered continuations.")]
    public void IncompleteAwaiter_ResumesStateMachineWhenContinuationRuns()
    {
    }

    /// <summary>
    /// A scheduler will eventually resume a suspended state machine and propagate an awaited NanoTask exception.
    /// </summary>
    [Fact(Skip = "Missing: scheduler queue to run registered continuations.")]
    public void IncompleteFaultedNanoTask_FaultsOuterTaskWhenContinuationRuns()
    {
    }

    /// <summary>
    /// A single-thread scheduler owns queued continuations, and simulated I/O completes only when the scheduler advances.
    /// </summary>
    [Fact(Skip = "Missing: scheduler queue and simulated I/O awaitable.")]
    public void SimulatedIo_CompletesThroughSchedulerTick()
    {
    }

}
