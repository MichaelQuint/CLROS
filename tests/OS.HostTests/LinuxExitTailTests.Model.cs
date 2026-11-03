using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe partial class LinuxExitTailTests
{
    private struct Model
    {
        internal LinuxSyscallGateState* Gate;
        internal byte* Code;
        internal bool Removed, Remapped, BadCode, WritableCode, WrongGeneration, Unmasked, Handler, CopyFails, Partial;
        internal uint Mutations, ThreadCalls;
        internal long Result;
    }
}
