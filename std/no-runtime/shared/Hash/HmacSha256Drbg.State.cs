using OS.Std.Memory;

namespace OS.Std.Hash;

public static unsafe partial class HmacSha256Drbg
{
    /// <summary>Initially zero, exclusive and supervisor-owned through Erase.
    /// A live value cannot be copied or moved. Counter is an observation only;
    /// other fields are private generator state, never application output.</summary>

    public struct State
    {
        internal ulong Self, Counter;
        internal fixed byte Key[32], Value[32];
        public readonly ulong RequestsSinceReseed => Counter == 0 ? 0 : Counter - 1;
        public readonly bool RequiresReseed => Counter > ReseedInterval;
    }
}
