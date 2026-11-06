#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
#if !NOSTDLIB_EFI || M16_027_PROBE
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One original open description's filesystem handle. Duplicated
    /// fds share this binding and Description.Position. Only final reference /
    /// borrower retirement closes it. All fields initially zero.</summary>

    public struct DiskBinding
    {
        internal Fs.FileToken File;
        internal Fs.FileInspection Metadata;
        internal ulong Generation;
        internal uint Description, Active;
    }
}
#endif
