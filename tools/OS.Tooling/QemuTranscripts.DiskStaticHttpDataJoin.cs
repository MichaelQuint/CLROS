using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied unique sequential file/HTTP association for the small
    /// development contract. Stream offsets come from actual queued sends;
    /// packet records come independently from the original TCP reconstruction.</summary>

    public readonly record struct DiskStaticHttpDataJoin(int Ordinal, string Path, ulong Inode, ulong DescriptorGeneration, ulong RequestGeneration, int ReadCompleteRecord, int FirstBodySendRecord, int RequestFirstPacket, int ResponseLastPacket);
}
