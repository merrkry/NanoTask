# NanoTask

A minimal custom awaitable type for C#, written to learn how `async`/`await` works underneath: compiler-generated state machines, async method builders, awaiters, and continuation scheduling.

```csharp
async NanoTask<int> Entry()
{
    // The BCL Task completes on the thread pool,
    // and the rest of this method is posted back to this thread.
    var value = await Task.Run(() => 41);
    return value + 1;
}

var result = NanoTaskScheduler.Current.Run(Entry); // 42
```

## Design

- **Boxing optimization.** Following the BCL's `AsyncStateMachineBox<TStateMachine>`, a task subclass (`NanoTaskExt`) stores the state machine struct inline, so the first suspension allocates one object for both, and the continuation delegates are created once per task.
- **Mixed await.** I/O comes from the BCL: a `NanoTask` method can await `Task`-based APIs, or `NanoTask`s from other threads, and always resumes on its own thread's `NanoTaskScheduler`.

## Testing

Requires the .NET 10 SDK.

```sh
dotnet test
```
