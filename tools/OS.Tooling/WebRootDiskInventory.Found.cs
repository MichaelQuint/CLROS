using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Time;
using OS.Storage;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Tooling;

public static partial class WebRootDiskInventory
{
    internal sealed record Found(string Name, string Alias, uint Cluster, uint Bytes, byte Attributes, ushort Date, ushort Time, long Modified, ulong Lba, uint Slot);
}
