using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Std.Hash;

internal static unsafe partial class LinuxStartupServicesTests
{
    private struct Model
    {
        public LinuxMemoryServices.Admission Memory;
        public ulong Ticks;
        public uint Reads, Copies, Fences, PartialCopyBytes;
        public bool FailCopy, FailClock, Regress;
    }
}
