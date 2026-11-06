using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied association between one positive file read and its exact
    /// original call/completion positions. Sectors are immutable logical-order
    /// physical LBAs; constructing this value grants no execution authority.</summary>

    public readonly record struct DiskReadWindow(ulong Ordinal, int CallRecordIndex, int CompletionRecordIndex, string Path, ulong Offset, uint Bytes, IReadOnlyList<ulong> Sectors);
}
