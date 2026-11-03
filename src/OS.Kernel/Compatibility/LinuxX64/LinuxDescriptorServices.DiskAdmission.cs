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
    public struct DiskAdmission
    {
        public Fs.Mount* Mount;
        public DiskBinding* Bindings;
        public DiskRequest* Requests;
        public ulong Epoch;
        public bool FirmwareExited, SingleOwnerValidated;
    }
}
#endif
