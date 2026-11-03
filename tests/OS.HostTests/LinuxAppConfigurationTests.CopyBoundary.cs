using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe partial class LinuxAppConfigurationTests
{
    private struct CopyBoundary
    {
        public LinuxDescriptorServices.State* State;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> Copy;
    }
}
