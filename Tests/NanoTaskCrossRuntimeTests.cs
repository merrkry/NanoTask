using NanoTasks;

namespace NanoTasks.Tests;

public sealed class NanoTaskCrossRuntimeTests
{
    /// <summary>
    /// Awaiting a BCL Task resumes the NanoTask state machine on the scheduler worker thread.
    /// </summary>
    [Fact]
    public void AwaitingBclTask_ResumesOnNanoTaskSchedulerThread()
    {
        var beforeAwaitThreadId = 0;
        var afterAwaitThreadId = 0;

        async NanoTask<int> Entry()
        {
            beforeAwaitThreadId = Environment.CurrentManagedThreadId;

            var value = await System.Threading.Tasks.Task.Run(() => 41);

            afterAwaitThreadId = Environment.CurrentManagedThreadId;
            return value + 1;
        }

        var result = NanoTaskScheduler.Current.Run(Entry);

        Assert.Equal(42, result);
        Assert.Equal(beforeAwaitThreadId, afterAwaitThreadId);
    }

    /// <summary>
    /// Awaiting an incomplete BCL Task posts the NanoTask state machine continuation back
    /// to the scheduler that owns the awaiting NanoTask.
    /// </summary>
    [Fact]
    public void AwaitingIncompleteBclTask_ResumesOnAwaitingSchedulerThread()
    {
        var source = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var beforeAwaitThreadId = 0;
        var completionThreadId = 0;
        var afterAwaitThreadId = 0;

        async NanoTask<int> Entry()
        {
            beforeAwaitThreadId = Environment.CurrentManagedThreadId;

            NanoTaskScheduler.Current.Post(() =>
            {
                var completionThread = new Thread(() =>
                {
                    completionThreadId = Environment.CurrentManagedThreadId;
                    source.SetResult(41);
                })
                {
                    IsBackground = true,
                };

                completionThread.Start();
            });

            var value = await source.Task;

            afterAwaitThreadId = Environment.CurrentManagedThreadId;
            return value + 1;
        }

        var result = NanoTaskScheduler.Current.Run(Entry);

        Assert.Equal(42, result);
        Assert.NotEqual(completionThreadId, afterAwaitThreadId);
        Assert.Equal(beforeAwaitThreadId, afterAwaitThreadId);
    }

    /// <summary>
    /// Awaiting a NanoTask owned by another scheduler still resumes the awaiting state machine on its own scheduler.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task AwaitingNanoTaskFromAnotherScheduler_ResumesOnAwaitingSchedulerThread()
    {
        var foreignTaskSource = new TaskCompletionSource<NanoTask<int>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var foreignSchedulerSource = new TaskCompletionSource<NanoTaskScheduler>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var foreignSchedulerThreadId = 0;

        async NanoTask<int> ForeignEntry()
        {
            foreignSchedulerThreadId = Environment.CurrentManagedThreadId;

            var task = new NanoTask<int>();
            foreignSchedulerSource.SetResult(NanoTaskScheduler.Current);
            foreignTaskSource.SetResult(task);

            return await task;
        }

        var foreignThread = new Thread(() => NanoTaskScheduler.Current.Run(ForeignEntry))
        {
            IsBackground = true,
        };
        foreignThread.Start();

        var foreignTask = await foreignTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var foreignScheduler = await foreignSchedulerSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var beforeAwaitThreadId = 0;
        var afterAwaitThreadId = 0;

        async NanoTask<int> AwaitingEntry()
        {
            beforeAwaitThreadId = Environment.CurrentManagedThreadId;

            _ = System.Threading.Tasks.Task.Run(() =>
            {
                Thread.Sleep(50);
                foreignScheduler.Post(() => foreignTask.SetResult(41));
            });

            var value = await foreignTask;

            afterAwaitThreadId = Environment.CurrentManagedThreadId;
            return value + 1;
        }

        var result = NanoTaskScheduler.Current.Run(AwaitingEntry);

        Assert.Equal(42, result);
        Assert.Equal(beforeAwaitThreadId, afterAwaitThreadId);
        Assert.NotEqual(foreignSchedulerThreadId, afterAwaitThreadId);
        Assert.True(foreignThread.Join(TimeSpan.FromSeconds(5)));
    }
}
