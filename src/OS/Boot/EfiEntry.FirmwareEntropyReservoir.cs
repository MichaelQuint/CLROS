#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
#endif
#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;
#endif
#if M04_LAYOUT_CONTRACT_PROBE
using OS.Kernel.Runtime;
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Memory;
#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
#if M16_004_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
#if M05_PCI_ENUMERATION_PROBE || M16_006_PROBE
using OS.Drivers;
#endif
#if M07_ARP_RECEIVE_PROBE
using OS.Net;
#if !M07_VIRTIO_NET_PROBE
#error The ARP receive profile requires the accepted original Virtio-net profile.
#endif
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
using OS.Storage;
#endif
#if M06_FAT_READ_PROBE
using FatChain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using FatDirectory = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using FatFile = OS.Storage.KnownFileSHAN256EquivalentKnownFixtureHash;
#endif
#if !(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#error The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.
#endif
#if M01_OWNED_PAGING_PROBE || M04_MANAGED_HEAP_PAGE_RANGE_PROBE || M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE || M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
#error The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE || M02_UD2_VECTOR_STATE_PROBE || M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE || M02_DOUBLE_FAULT_IST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE
#error The first real exit cannot be combined with another terminal exception probe.
#endif
#endif
namespace OS.Boot;
#pragma warning disable 649
public static unsafe partial class EfiEntry
{
#if M03_FIRMWARE_ENTROPY_SEED_PROBE || M16_004_PROBE
    // One owned, non-refillable pre-EBS reservoir. Every block uses a fresh
    // instance of the original guarded firmware acquisition, with no retained
    // firmware pointer or copied RNG seed. Caller supplies exclusive mapped
    // storage disjoint from firmware; malformed/stale admission writes nothing.
    internal static partial class FirmwareEntropyReservoir
    {
#if M10_004_PROBE
        internal const uint Bytes = 4096;
#else
        internal const uint Bytes = 256;
#endif

        private static bool Span(ulong address, ulong bytes) =>
            address != 0 && (address & 7) == 0 && address <= ulong.MaxValue - bytes;
        private static bool Apart(ulong a, ulong an, ulong b, ulong bn) => a + an <= b || b + bn <= a;

        internal static bool Acquire(State* state, EfiSystemTable* table)
        {
            if (!Span((ulong)state, (ulong)sizeof(State)) || !Span((ulong)table, (ulong)sizeof(EfiSystemTable)) ||
                !Apart((ulong)state, (ulong)sizeof(State), (ulong)table, (ulong)sizeof(EfiSystemTable)))
                return false;
            var services = table->BootServices;
            if (!Span((ulong)services, (ulong)sizeof(EfiBootServices)) ||
                !Apart((ulong)state, (ulong)sizeof(State), (ulong)services, (ulong)sizeof(EfiBootServices)) ||
                !Apart((ulong)table, (ulong)sizeof(EfiSystemTable), (ulong)services, (ulong)sizeof(EfiBootServices)) ||
                state->Phase != 0 || state->PublishedBytes != 0 || !FirmwareEntropyCapture.Zero(state->Data, 0, (int)Bytes))
                return false;
            state->Phase = 1;
            for (uint offset = 0; offset < Bytes; offset += (uint)FirmwareEntropyCapture.RequestedBytes)
                if (!Block(table, state->Data + offset))
                {
                    Clear(state);
                    return false;
                }
            state->PublishedBytes = Bytes;
            state->Phase = 2;
            return true;
        }

        // Clear consumes the lifetime, including a failed or unused acquisition.
        // It never restores the all-zero state required by Acquire.
        internal static bool Clear(State* state)
        {
            if (!Span((ulong)state, (ulong)sizeof(State)))
                return false;
            for (uint i = 0; i < Bytes; i++)
                state->Data[i] = 0;
            state->PublishedBytes = 0;
            state->Phase = 3;
            return FirmwareEntropyCapture.Zero(state->Data, 0, (int)Bytes);
        }

        private static bool Block(EfiSystemTable* table, byte* destination)
        {
            FirmwareEntropyCapture.State capture = default;
            FirmwareEntropyCapture.Buffer scratch = default, retained = default;
            bool acquired = FirmwareEntropyCapture.Begin(&capture, &scratch, &retained, table) && FirmwareEntropyCapture.Acquire(&capture);
            capture.SystemTable = null;
            capture.BootServices = null;
            capture.Locate = null;
            capture.Rng = null;
            capture.GetRng = null;
            bool valid = acquired && capture.Phase == 3 && capture.PublishedBytes == FirmwareEntropyCapture.RequestedBytes &&
                FirmwareEntropyCapture.Guards(&scratch) && FirmwareEntropyCapture.Guards(&retained) &&
                FirmwareEntropyCapture.Zero(scratch.Data, 0, 64) && FirmwareEntropyCapture.Zero(retained.Data, 32, 32);
            if (valid)
                for (uint i = 0; i < FirmwareEntropyCapture.RequestedBytes; i++)
                    destination[i] = FirmwareEntropyCapture.ReadByte(retained.Data + i);
            for (uint i = 0; i < 64; i++)
            {
                scratch.Data[i] = 0;
                retained.Data[i] = 0;
            }
            capture.Scratch = null;
            capture.Retained = null;
            return valid && FirmwareEntropyCapture.Zero(scratch.Data, 0, 64) && FirmwareEntropyCapture.Zero(retained.Data, 0, 64);
        }
    }

#endif
}
#pragma warning restore 649
