using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

internal static unsafe partial class LinuxMemoryServicesTests
{
    private struct GuardOwner
    {
        internal ulong Pin, LastStart, LastBytes;
        internal uint Calls;
        internal bool Deny;
    }
}
