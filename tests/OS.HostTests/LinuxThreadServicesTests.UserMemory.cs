using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using KernelModel = KernelThreadTableTests.Model;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe partial class LinuxThreadServicesTests
{
    private struct UserMemory
    {
        internal byte* Bytes;
        internal Services* Service;
        internal LinuxMemoryServices.Admission Memory;
        internal ulong FailWriteAddress, FailReadAddress, LastRead, LastWrite;
        internal uint Reads, Writes, FailedWrites;
        internal ulong Now, Lifetime, ClockStep;
        internal bool ClockFails;
    }
}
