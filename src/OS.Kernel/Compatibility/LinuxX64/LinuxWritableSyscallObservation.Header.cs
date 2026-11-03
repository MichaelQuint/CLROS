using System;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxWritableSyscallObservation
{
    /// <summary>Copied record header. Address/base values are never dereferenced.
    /// Input records prove only copied bytes, not mutation success. Output records
    /// require the caller's actual committed-copy boundary; the carrier cannot
    /// authenticate that caller fact. PayloadOffset is local batch storage.</summary>

    public struct Header
    {
        public ulong Sequence;
        public CallIdentity Call;
        public RequestIdentity Request;
        public ulong UserAddress, VectorBase, RequestedBytes, FileOffset, ValueBits;
        public uint PayloadOffset, PayloadBytes, OutputPrefix;
        public Kind Kind;
        public Boundary Boundary;
        internal ushort Reserved;
    }
}
