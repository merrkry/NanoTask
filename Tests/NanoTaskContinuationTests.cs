using NanoTasks;

namespace NanoTasks.Tests;

public sealed class NanoTaskContinuationTests
{
    /// <summary>
    /// Completing a NanoTask posts previously registered manual continuations; Run owns their execution.
    /// </summary>
    [Fact]
    public void SetResult_PostsPreviouslyRegisteredContinuationThroughRun()
    {
        var task = new NanoTask<int>();
        var continuationCalls = 0;

        task.GetAwaiter().OnCompleted(() => continuationCalls++);

        async NanoTask<int> Entry()
        {
            NanoTaskScheduler.Current.Post(() => task.SetResult(42));
            await task;
            return continuationCalls;
        }

        var result = NanoTaskScheduler.Current.Run(Entry);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Registering a continuation after completion still posts it to the scheduler instead of running inline.
    /// </summary>
    [Fact]
    public void CompletedNanoTask_OnCompletedPostsContinuationThroughRun()
    {
        var task = new NanoTask<int>();
        var gate = new NanoTask<int>();
        var continuationCalls = 0;

        task.SetResult(42);

        async NanoTask<int> Entry()
        {
            task.GetAwaiter().OnCompleted(() =>
            {
                continuationCalls++;
                gate.SetResult(0);
            });

            await gate;
            return continuationCalls;
        }

        var result = NanoTaskScheduler.Current.Run(Entry);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// A scheduler will eventually resume a suspended state machine and propagate an awaited NanoTask exception.
    /// </summary>
    [Fact]
    public void IncompleteFaultedNanoTask_FaultsOuterTaskWhenContinuationRuns()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> Entry()
        {
            NanoTaskScheduler.Current.Post(() => inner.SetException(new InvalidOperationException("boom")));
            return await inner;
        }

        var exception = Assert.Throws<InvalidOperationException>(() => NanoTaskScheduler.Current.Run(Entry));
        Assert.Equal("boom", exception.Message);
    }
}
