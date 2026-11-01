using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Drivers;
using OS.Kernel.Platform;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using OS.Storage;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    private struct FsObservation
    {
        internal Block.State* Device;
        internal Block.Observation Candidate;
        internal ulong Submitted, Completed, CopyCandidates;
        internal bool Reject, Bad;
        internal byte* CacheBacking;
        internal uint CacheSlots;
        internal bool CheckDirectoryCache, CheckedDirectoryCache;
        internal fixed byte Payload[512];
    }
}
