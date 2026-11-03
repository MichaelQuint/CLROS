using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe partial class LinuxDescriptorProfileTests
{
    private struct Model
    {
        public uint StartupCalls, DescriptorCalls;
        public bool RemoveReturn;
        public long Value;
    }
}
