using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One-use owner state. Never copy or concurrently change a live
    /// instance. End closes every remaining descriptor and scrubs owned buffers;
    /// it does not free caller storage or erase immutable package data.</summary>

    public struct State
    {
        internal Admission Owner;
        internal uint Initialized, Active, NextInode;
        internal ulong ThreadOwner;
#if !NOSTDLIB_EFI || M16_025_PROBE
        internal DiskState* Disk;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        internal LinuxSocketServices.State* Sockets;
        internal LinuxEpollServices.State* Epoll;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal WritableDiskState* WritableDisk;
#endif
        public readonly uint ActiveDescriptors => Initialized == 1 ? Active : 0;

        /// <summary>Retained kernel I/O borrows, including closed fds. This is
        /// diagnostic accounting, not a pin or release grant.</summary>

        public readonly uint RetainedPipeOperations
        {
            get
            {
                if (Initialized != 1)
                    return 0;
                uint count = 0;
                for (uint i = 0; i < Owner.DescriptionCount; i++)
                    count += Owner.Descriptions[i].Borrowers;
                return count;
            }
        }
    }
}
