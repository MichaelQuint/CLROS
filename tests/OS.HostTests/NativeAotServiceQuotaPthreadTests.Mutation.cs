using System.Buffers.Binary;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaPthreadTests
{
    private enum Mutation
    {
        None,
        RetainThenDiscard,
        Fixed,
        Shrink,
        Hole,
        Grow
    }
}
