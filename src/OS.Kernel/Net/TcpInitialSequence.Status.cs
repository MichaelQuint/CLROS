using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Net;

public static unsafe partial class TcpInitialSequence
{
    public enum Status : byte
    {
        Success,
        Quiet,
        InvalidState,
        InvalidEndpoint,
        ClockFailure,
        Exhausted
    }
}
