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
    private struct Hardware
    {
        internal byte* Data;
        internal ulong Capacity, Descriptor, Available, Used, Sector;
        internal uint Produced;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector;
        internal byte Status;
        internal bool Bus, Pending;
        internal bool CompleteOnPoll;
        internal nint DiskHandle;
    }
}
