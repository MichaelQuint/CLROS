using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Net;
public static unsafe partial class TcpInitialSequence
{
    /// <summary>Returned by value so output cannot alias retained secret state.
    /// Sequence is meaningful only on Success; zero is a valid modular TCP ISN.</summary>

    public readonly struct Result
    {
        internal Result(Status status, uint sequence = 0)
        {
            Status = status;
            Sequence = sequence;
        }

        public Status Status { get; }
        public uint Sequence { get; }
    }
}
