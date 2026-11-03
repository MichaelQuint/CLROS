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
    public readonly record struct MetadataSector(ulong Lba, MetadataKind Kind, string DirectoryPath);
}
