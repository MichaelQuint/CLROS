using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>All mutable extents must be disjoint, zeroed and inaccessible
    /// to the user. Scratch is 8..64KiB; each open description has one 8KiB proc
    /// snapshot slot. Pipe buffers have a common power-of-two capacity. Resource
    /// facts are actual owned snapshots; AvailableBytes includes the memory
    /// pool, whose current commitments are deducted. SIGPIPE policy is explicit
    /// mechanical admission only, never a fabricated sigaction implementation.</summary>

    public struct Admission
    {
        public Slot* Slots;
        public Description* Descriptions;
        public LinuxPipe* Pipes;
        public byte* PipeBuffers;
        public byte* Snapshots;
        public byte* Scratch;
        public uint DescriptorCount, DescriptionCount, PipeCount, PipeCapacity, ScratchBytes;
        public View* Views;
        public uint ViewCount;
        /// <summary>Nonzero enables bounded package path observations for this
        /// actual process ID. Views must be produced in package order, executable
        /// first with inodes256+i. The caller binds this ID to its thread owner;
        /// zero retains the earlier descriptor-only contract. CWD is fixed /.</summary>

        public uint PackageProcessId;
        public LinuxMemoryServices.State* Memory;
        public ulong PhysicalBytes, AvailableBytes;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public bool FirmwareExited, SingleOwnerValidated, SigpipeIgnored;
    }
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#else

#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if (!NOSTDLIB_EFI || M16_025_PROBE) && (!NOSTDLIB_EFI || M16_027_PROBE)
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#endif
#if (!NOSTDLIB_EFI || M16_027_PROBE) && (!NOSTDLIB_EFI || CLROS_LINUX_SOCKETS)
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#endif
}
