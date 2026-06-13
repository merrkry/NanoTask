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
    /// Completing an awaited NanoTask enqueues the compiler-generated state machine continuation.
    /// </summary>
    [Fact]
    public void AwaitedNanoTaskCompletion_EnqueuesStateMachineContinuation()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> AwaitIncompleteAsync()
        {
            var value = await inner;
            return value + 1;
        }

        var outer = AwaitIncompleteAsync();

        inner.SetResult(41);

        Assert.False(outer.IsCompleted());

        NanoTaskScheduler.Global.Drain();

        Assert.True(outer.IsCompleted());
        Assert.Equal(42, outer.GetResult());
    }

    /// <summary>
    /// A scheduler will eventually resume a suspended state machine and propagate an awaited NanoTask exception.
    /// </summary>
    [Fact]
    public void IncompleteFaultedNanoTask_FaultsOuterTaskWhenContinuationRuns()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> AwaitIncompleteAsync()
        {
            return await inner;
        }

        var outer = AwaitIncompleteAsync();

        inner.SetException(new InvalidOperationException("boom"));

        Assert.False(outer.IsCompleted());

        NanoTaskScheduler.Global.Drain();

        var exception = Assert.Throws<InvalidOperationException>(() => outer.GetResult());
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// Completing a NanoTask only enqueues an already-registered manual continuation; scheduler.Drain owns execution.
    /// </summary>
    [Fact]
    public void SetResult_EnqueuesPreviouslyRegisteredContinuationWithoutRunningInline()
    {
        var task = new NanoTask<int>();
        var awaiter = task.GetAwaiter();
        var continuationCalls = 0;

        awaiter.OnCompleted(() => continuationCalls++);

        task.SetResult(42);

        Assert.Equal(0, continuationCalls);

        NanoTaskScheduler.Global.Drain();

        Assert.Equal(1, continuationCalls);
    }

    /// <summary>
    /// Registering a continuation after completion still schedules it for the next scheduler drain.
    /// </summary>
    [Fact]
    public void CompletedNanoTask_OnCompletedEnqueuesContinuation()
    {
        var task = new NanoTask<int>();
        var continuationCalls = 0;

        task.SetResult(42);

        task.GetAwaiter().OnCompleted(() => continuationCalls++);

        Assert.Equal(0, continuationCalls);

        NanoTaskScheduler.Global.Drain();

        Assert.Equal(1, continuationCalls);
    }

    /// <summary>
    /// The same NanoTaskExt state machine box can resume through multiple incomplete awaits.
    /// </summary>
    [Fact]
    public void SequentialIncompleteAwaits_ReuseStateMachineBoxAndPreserveLocals()
    {
        var first = new NanoTask<int>();
        var second = new NanoTask<int>();
        var observedValues = new List<int>();

        async NanoTask<int> AwaitTwiceAsync()
        {
            var value = 10;
            value += await first;
            observedValues.Add(value);
            value += await second;
            observedValues.Add(value);
            return value;
        }

        var task = AwaitTwiceAsync();

        first.SetResult(1);
        NanoTaskScheduler.Global.Drain();

        Assert.False(task.IsCompleted());
        Assert.Equal([11], observedValues);

        second.SetResult(2);
        NanoTaskScheduler.Global.Drain();

        Assert.Equal([11, 13], observedValues);
        Assert.Equal(13, task.GetResult());
    }

    /// <summary>
    /// Independent suspended state machines can be resumed by the same scheduler drain.
    /// </summary>
    [Fact]
    public void ParallelIncompleteAwaits_ResumeIndependentStateMachinesInOneDrain()
    {
        var first = new NanoTask<int>();
        var second = new NanoTask<int>();
        var completions = new List<string>();

        async NanoTask<int> AwaitFirstAsync()
        {
            var value = await first;
            completions.Add("first");
            return value + 10;
        }

        async NanoTask<int> AwaitSecondAsync()
        {
            var value = await second;
            completions.Add("second");
            return value + 20;
        }

        var firstOuter = AwaitFirstAsync();
        var secondOuter = AwaitSecondAsync();

        first.SetResult(1);
        second.SetResult(2);

        Assert.False(firstOuter.IsCompleted());
        Assert.False(secondOuter.IsCompleted());
        Assert.Empty(completions);

        NanoTaskScheduler.Global.Drain();

        Assert.Equal(["first", "second"], completions);
        Assert.Equal(11, firstOuter.GetResult());
        Assert.Equal(22, secondOuter.GetResult());
    }

    /// <summary>
    /// Multiple awaiters of the same NanoTask each get their own state machine continuation.
    /// </summary>
    [Fact]
    public void SharedIncompleteNanoTask_ResumesAllAwaitingStateMachines()
    {
        var shared = new NanoTask<int>();
        var completions = new List<string>();

        async NanoTask<int> AwaitSharedAsync(string name, int offset)
        {
            var value = await shared;
            completions.Add(name);
            return value + offset;
        }

        var first = AwaitSharedAsync("first", 1);
        var second = AwaitSharedAsync("second", 2);

        shared.SetResult(40);

        Assert.False(first.IsCompleted());
        Assert.False(second.IsCompleted());
        Assert.Empty(completions);

        NanoTaskScheduler.Global.Drain();

        Assert.Equal(["first", "second"], completions);
        Assert.Equal(41, first.GetResult());
        Assert.Equal(42, second.GetResult());
    }

    /// <summary>
    /// A state machine resumed by the scheduler can suspend again without blocking other ready continuations.
    /// </summary>
    [Fact]
    public void ParallelAwaits_OneStateMachineCanSuspendAgainWhileAnotherCompletes()
    {
        var firstStep = new NanoTask<int>();
        var secondStep = new NanoTask<int>();
        var independent = new NanoTask<int>();
        var observed = new List<string>();

        async NanoTask<int> AwaitTwiceAsync()
        {
            var value = await firstStep;
            observed.Add($"after-first:{value}");
            value += await secondStep;
            observed.Add($"after-second:{value}");
            return value;
        }

        async NanoTask<int> AwaitIndependentAsync()
        {
            var value = await independent;
            observed.Add($"independent:{value}");
            return value;
        }

        var chained = AwaitTwiceAsync();
        var other = AwaitIndependentAsync();

        firstStep.SetResult(10);
        independent.SetResult(7);
        NanoTaskScheduler.Global.Drain();

        Assert.False(chained.IsCompleted());
        Assert.True(other.IsCompleted());
        Assert.Equal(["after-first:10", "independent:7"], observed);
        Assert.Equal(7, other.GetResult());

        secondStep.SetResult(5);
        NanoTaskScheduler.Global.Drain();

        Assert.Equal(["after-first:10", "independent:7", "after-second:15"], observed);
        Assert.Equal(15, chained.GetResult());
    }

    /// <summary>
    /// A reentrant scheduler.Drain call is deferred to the active drain loop.
    /// </summary>
    [Fact]
    public void SchedulerDrain_ReentrantCallDefersToOuterDrain()
    {
        var calls = new List<int>();

        NanoTaskScheduler.Global.Enqueue(() =>
        {
            calls.Add(1);
            NanoTaskScheduler.Global.Enqueue(() => calls.Add(3));
            NanoTaskScheduler.Global.Drain();
            calls.Add(2);
        });

        NanoTaskScheduler.Global.Drain();

        Assert.Equal([1, 2, 3], calls);
    }

    /// <summary>
    /// A single-thread scheduler owns queued continuations, and simulated I/O completes only when the scheduler advances.
    /// </summary>
    [Fact(Skip = "Missing: scheduler queue and simulated I/O awaitable.")]
    public void SimulatedIo_CompletesThroughSchedulerTick()
    {
    }

}
