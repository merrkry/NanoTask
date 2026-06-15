using NanoTasks;

namespace NanoTasks.Tests;

public sealed class NanoTaskAwaiterTests
{
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
}
