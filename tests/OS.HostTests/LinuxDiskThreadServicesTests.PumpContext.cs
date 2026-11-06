using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxDiskThreadServicesTests
{
    private struct PumpContext
    {
        internal Block.State* Device;
        internal ulong Now, Increment, Submitted, Completed, Copied;
        internal uint Flushes;
        internal uint ClockCalls, FailClockAt, FailFlushAt;
        internal bool Bad, Regress;
    }
}
