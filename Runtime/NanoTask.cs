using System.Runtime.CompilerServices;

namespace NanoTask;

internal readonly struct VoidResult { }

[AsyncMethodBuilder(typeof(NanoTaskMethodBuilder<>))]
public class NanoTask<T>
{
    
}
