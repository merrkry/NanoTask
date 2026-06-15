using NanoTasks;

namespace NanoTasks.Tests;

public sealed class NanoTaskStateMachineTests
{
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

        async NanoTask<int> Entry()
        {
            var value = await CompletedAsync();
            return value + 1;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(43, result);
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

        async NanoTask<int> Entry()
        {
            return await FaultedAsync();
        }

        var exception = Assert.Throws<InvalidOperationException>(() => NanoTaskScheduler.Global.Run(Entry));
        Assert.Equal("boom", exception.Message);
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

        async NanoTask<int> Entry()
        {
            var value = 10;

            NanoTaskScheduler.Global.Post(() => first.SetResult(1));
            value += await first;
            observedValues.Add(value);

            NanoTaskScheduler.Global.Post(() => second.SetResult(2));
            value += await second;
            observedValues.Add(value);

            return value;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal([11, 13], observedValues);
        Assert.Equal(13, result);
    }

    /// <summary>
    /// Independent suspended state machines can be resumed by the same scheduler run.
    /// </summary>
    [Fact]
    public void ParallelIncompleteAwaits_ResumeIndependentStateMachinesInOneRun()
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

        async NanoTask<int> Entry()
        {
            var firstOuter = AwaitFirstAsync();
            var secondOuter = AwaitSecondAsync();

            NanoTaskScheduler.Global.Post(() =>
            {
                first.SetResult(1);
                second.SetResult(2);
            });

            return await firstOuter + await secondOuter;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(["first", "second"], completions);
        Assert.Equal(33, result);
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

        async NanoTask<int> Entry()
        {
            var first = AwaitSharedAsync("first", 1);
            var second = AwaitSharedAsync("second", 2);

            NanoTaskScheduler.Global.Post(() => shared.SetResult(40));

            return await first + await second;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(["first", "second"], completions);
        Assert.Equal(83, result);
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

            NanoTaskScheduler.Global.Post(() => secondStep.SetResult(5));

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

        async NanoTask<int> Entry()
        {
            var chained = AwaitTwiceAsync();
            var other = AwaitIndependentAsync();

            NanoTaskScheduler.Global.Post(() =>
            {
                firstStep.SetResult(10);
                independent.SetResult(7);
            });

            return await chained + await other;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(["after-first:10", "independent:7", "after-second:15"], observed);
        Assert.Equal(22, result);
    }
}
