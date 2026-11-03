using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class LinuxDiskDescriptorServicesTests
{
    private delegate int ManagedOffset<T>(ref T value)
        where T : unmanaged;
}
