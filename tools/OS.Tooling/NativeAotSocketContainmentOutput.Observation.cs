using System.Globalization;

namespace OS.Tooling;
public static partial class NativeAotSocketContainmentOutput
{
    public sealed record Observation(ulong Queued, ulong TimeoutMilliseconds, ulong WorkerBefore, ulong WorkerAtTimeout, ulong Worker, ulong ElapsedMilliseconds, IReadOnlyList<Healthy> Peers);
}
