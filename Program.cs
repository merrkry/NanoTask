using NanoTask;

static async NanoTask.NanoTask<VoidResult> RunAsync()
{
    // await System.Threading.Tasks.Task.Yield();
    return default(VoidResult);
}

var task = RunAsync();