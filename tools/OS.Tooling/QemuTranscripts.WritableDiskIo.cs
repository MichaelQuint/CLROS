using System.Globalization;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using B = OS.Storage.WritableBlockSession;
using Fixture = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied serial values, never a live block token or permission to
    /// dereference Owner. Payload is immutable lowercase hex; CopyPayload returns
    /// a new array. Original indexes are retained for independent parent joins.</summary>

    public sealed record WritableDiskIo(int RecordIndex, ulong Owner, ulong Epoch, ulong Generation, B.Operation Kind, ulong Sector, byte Flags, B.Status Status, bool Cancelled, ulong Submitted, ulong Completed, ulong SubmittedBytes, ulong CompletedBytes, ulong FlushTargetBytes, ulong FlushAcknowledgedBytes, ushort Available, ushort Consumed, uint CompletionBytes, uint PayloadBytes, ulong Clock, string DataHex)
    {
        public bool IsComplete => (Flags & 2) != 0;
        public bool IsCopied => (Flags & 4) != 0;

        public byte[] CopyPayload() => Convert.FromHexString(DataHex);
    }
}
