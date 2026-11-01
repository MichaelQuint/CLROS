#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSocketServices
{
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>One-use, noncopyable owner with exclusive zeroed endpoint storage.
    /// InterfaceAddress is the actual configured unicast address. Only that
    /// address or INADDR_ANY and the exact AllowedPort may bind. Begin retains
    /// descriptors and slots; Finish detaches only after all TCP ownership drains.</summary>

    public struct State
    {
        internal ulong Self, NextOrder;
        internal LinuxDescriptorServices.State* Descriptors;
        internal Endpoint* Slots;
        internal uint Count, Initialized;
        internal LinuxSocketNetwork.State* Network;
        internal Ipv4Address InterfaceAddress;
        internal ushort AllowedPort;
        // Optional trusted diagnostics, bound by the native owner. The callback
        // may only read borrowed stack vectors during this call; it must not
        // retain pointers, change descriptors/transport or reenter dispatch.
        // Context outlives Finish. A failed observation queues/consumes no bytes.
        internal void* MessageContext;
        internal delegate* unmanaged<void*, ulong, uint, ulong, ulong, ulong, ulong*, uint, uint, byte> ObserveMessage;
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal void* OriginalMessageContext;
        internal uint MessageContextBytes;
        internal delegate* unmanaged<void*, ulong, uint, ulong, ulong, ulong, ulong*, uint, uint, byte> OriginalObserveMessage;
#endif
        /// <summary>Whether the serialized owner attached message diagnostics.
        /// This reports configuration only, not transport or execution evidence.</summary>

        public readonly bool HasMessageObserver => MessageContext != null && ObserveMessage != null;

        public readonly uint RetainedConnections
        {
            get
            {
                uint count = 0;
                if (Initialized == 1)
                    for (uint i = 0; i < Count; i++)
                        if (Slots[i].Phase >= Pending)
                            count++;
                return count;
            }
        }
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
#endif
