using NanoTasks;

namespace NanoTasks.Tests;

public sealed class NanoTaskRunTests
{
    /// <summary>
    /// Run starts the entry async method and returns its completed result.
    /// </summary>
    [Fact]
    public void Run_ReturnsSynchronouslyCompletedEntryResult()
    {
        async NanoTask<int> Entry()
        {
            return 42;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(42, result);
    }

    /// <summary>
    /// Run observes the entry task result, so a synchronous async-method exception is rethrown.
    /// </summary>
    [Fact]
    public void Run_PropagatesSynchronouslyFaultedEntryException()
    {
        async NanoTask<int> Entry()
        {
            throw new InvalidOperationException("boom");
        }

        var exception = Assert.Throws<InvalidOperationException>(() => NanoTaskScheduler.Global.Run(Entry));
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// Work posted before Run can complete the entry task once the pump starts.
    /// </summary>
    [Fact]
    public void Run_PumpsWorkPostedBeforeEntryCompletes()
    {
        var inner = new NanoTask<int>();
        NanoTaskScheduler.Global.Post(() => inner.SetResult(40));

        async NanoTask<int> Entry()
        {
            var value = await inner;
            return value + 2;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(42, result);
    }

    /// <summary>
    /// The entry async method can post simulated I/O completion and then suspend until the pump runs it.
    /// </summary>
    [Fact]
    public void Run_PumpsWorkPostedByEntryBeforeAwait()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> Entry()
        {
            NanoTaskScheduler.Global.Post(() => inner.SetResult(41));
            var value = await inner;
            return value + 1;
        }

        var result = NanoTaskScheduler.Global.Run(Entry);

        Assert.Equal(42, result);
    }

    /// <summary>
    /// Run observes exceptions thrown after the entry task resumes from suspension.
    /// </summary>
    [Fact]
    public void Run_PropagatesExceptionAfterSuspension()
    {
        var inner = new NanoTask<int>();

        async NanoTask<int> Entry()
        {
            NanoTaskScheduler.Global.Post(() => inner.SetResult(0));
            await inner;
            throw new InvalidOperationException("boom");
        }

        var exception = Assert.Throws<InvalidOperationException>(() => NanoTaskScheduler.Global.Run(Entry));
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// Run owns the scheduler pump; nested Run calls are rejected instead of creating a reentrant pump.
    /// </summary>
    [Fact]
    public void Run_RejectsReentrantRun()
    {
        async NanoTask<int> Nested()
        {
            return 1;
        }

        async NanoTask<int> Entry()
        {
            return NanoTaskScheduler.Global.Run(Nested);
        }

        var exception = Assert.Throws<InvalidOperationException>(() => NanoTaskScheduler.Global.Run(Entry));
        Assert.Contains("reentrant", exception.Message);
    }
}
