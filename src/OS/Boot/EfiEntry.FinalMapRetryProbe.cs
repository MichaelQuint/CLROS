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
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
    // Real map captures, controlled exit callbacks. Reporting is outside every
    // final-map window; the original firmware exit pointer is never accessed.
    internal static partial class FinalMapRetryProbe
    {
        private const OS.Kernel.Platform.FirmwareServicesMode Available = OS.Kernel.Platform.FirmwareServicesMode.BootServicesAvailable;
        private const ulong InvalidParameter = 0x8000000000000002UL;

        private static Case s_single, s_success, s_exhausted;
        private static OwnedMemoryMapBuffer s_buffer;
        private static Case* s_active;
        private static delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> s_getMap;
        private static byte* s_bytes;
        private static ulong s_capacity, s_handle, s_phase, s_captureEntries, s_exitEntries;

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong Capture(ulong* size, void* map, ulong* key, ulong* stride, uint* version)
        {
            s_captureEntries++;
            Case* c = s_active;
            if (c == null || c->Captures >= 2 || c->Captures != c->Exits ||
                size == null || key == null || stride == null || version == null ||
                map != s_bytes || *size != s_capacity || s_getMap == null)
                return InvalidParameter;
            Attempt* a = c->Captures == 0 ? &c->First : &c->Second;
            c->Captures++;
            a->EnterOrder = ++c->Sequence;
            ulong status = s_getMap(size, map, key, stride, version);
            a->ReturnOrder = ++c->Sequence;
            a->CaptureStatus = status;
            a->Key = *key;
            a->MapBytes = *size;
            a->DescriptorBytes = *stride;
            a->DescriptorVersion = *version;
            return status;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong ControlledExit(void* imageHandle, ulong key)
        {
            s_exitEntries++;
            Case* c = s_active;
            if (c == null || c->Exits >= 2 || c->Captures != c->Exits + 1)
                return InvalidParameter;
            Attempt* a = c->Exits == 0 ? &c->First : &c->Second;
            c->Exits++;
            a->ExitOrder = ++c->Sequence;
            if ((ulong)imageHandle != s_handle || key != a->Key || a->CaptureStatus != 0 ||
                a->EnterOrder != c->Exits * 3 - 2 || a->ReturnOrder != c->Exits * 3 - 1 ||
                a->ExitOrder != c->Exits * 3)
                c->Error = 1;
            a->ExitStatus = c->Mode == 2 || (c->Mode == 1 && c->Exits == 1) ? InvalidParameter : 0;
            return a->ExitStatus;
        }

        private static bool RunCase(Case* c, ulong mode)
        {
            c->Mode = mode;
            s_active = c;
            bool expected;
            byte* state;
            int stateBytes;
            if (mode == 0)
            {
                FinalMapExitWindowResult result = NoAllocationOrUnrelatedFirmwareCallOccursBetween.Execute(
                    ref c->Window, Available, &Capture, &ControlledExit, (void*)s_handle, s_bytes, s_capacity);
                c->Status = (ulong)result.Status;
                c->Windows = 1;
                expected = result.Succeeded && result.Map.MapKey == c->First.Key && c->Window.IsConsumed;
                state = (byte*)&c->Window;
                stateBytes = sizeof(FinalMapExitWindowState);
            }
            else
            {
                BoundedExitRetryResult result = BoundedRetryStaleKeyStatus.Execute(
                    ref c->Retry, Available, &Capture, &ControlledExit, (void*)s_handle, s_bytes, s_capacity);
                c->Status = (ulong)result.Status;
                c->Windows = result.WindowsStarted;
                expected = result.ExitCalls == 2 && result.WindowsStarted == 2 && c->Retry.IsConsumed &&
                    result.Last.Map.MapKey == c->Second.Key &&
                    (mode == 1 ? result.Succeeded : result.Status == BoundedExitRetryStatus.RetryExhausted);
                state = (byte*)&c->Retry;
                stateBytes = sizeof(BoundedExitRetryState);
            }
            ulong calls = mode == 0 ? 1UL : 2UL;
            expected = expected && c->Captures == calls && c->Exits == calls && c->Error == 0 && c->Sequence == 3 * calls;
            Snapshot snapshot = default;
            for (int i = 0; i < stateBytes; i++)
                snapshot.Bytes[i] = state[i];
            ulong captures = s_captureEntries, exits = s_exitEntries;
            if (mode == 0)
                c->Reuse = (ulong)NoAllocationOrUnrelatedFirmwareCallOccursBetween.Execute(
                    ref c->Window, Available, &Capture, &ControlledExit, (void*)s_handle, s_bytes, s_capacity).Status;
            else
                c->Reuse = (ulong)BoundedRetryStaleKeyStatus.Execute(
                    ref c->Retry, Available, &Capture, &ControlledExit, (void*)s_handle, s_bytes, s_capacity).Status;
            bool same = captures == s_captureEntries && exits == s_exitEntries;
            for (int i = 0; i < stateBytes; i++)
                if (snapshot.Bytes[i] != state[i])
                    same = false;
            c->Preserved = same ? 1UL : 0UL;
            s_active = null;
            return expected && same && c->Reuse == (mode == 0 ? (ulong)FinalMapExitWindowStatus.AlreadyConsumed : (ulong)BoundedExitRetryStatus.AlreadyConsumed);
        }

        internal static bool Execute(void* handle,
            delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMap,
            byte* bytes, ulong capacity)
        {
            if (s_phase != 0 || handle == null || getMap == null || bytes == null || capacity < 40 ||
                capacity > ulong.MaxValue - (ulong)bytes || sizeof(FinalMapExitWindowState) > 128 ||
                sizeof(BoundedExitRetryState) > 128)
                return false;
            s_phase = 1;
            s_handle = (ulong)handle;
            s_getMap = getMap;
            s_bytes = bytes;
            s_capacity = capacity;
            fixed (Case* one = &s_single)
            fixed (Case* success = &s_success)
            fixed (Case* exhausted = &s_exhausted)
            {
                if (!RunCase(one, 0) || !RunCase(success, 1) || !RunCase(exhausted, 2) ||
                    s_captureEntries != 5 || s_exitEntries != 5)
                    return false;
            }
            s_phase = 2;
            return true;
        }

        internal static bool Run(void* handle, EfiBootServices* services, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            Token(console, serial, 0);
            fixed (byte* bytes = &s_buffer.Bytes[0])
            {
                if (services == null || !Execute(handle, services->GetMemoryMap, bytes, OwnedMemoryMapBufferSize))
                {
                    Token(console, serial, 1);
                    return false;
                }
            }
            Token(console, serial, 2);
            WriteUnsignedDecimal(console, serial, s_handle);
            Value(console, serial, 3, (ulong)s_getMap);
            Value(console, serial, 4, (ulong)s_bytes);
            Value(console, serial, 5, s_capacity);
            WriteCrLf(console, serial);
            fixed (Case* one = &s_single)
                Record(console, serial, one, 0);
            Token(console, serial, 6);
            fixed (Case* success = &s_success)
                Record(console, serial, success, 1);
            fixed (Case* exhausted = &s_exhausted)
                Record(console, serial, exhausted, 2);
            Token(console, serial, 7);
            return true;
        }

        private static void Record(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, Case* c, ulong id)
        {
            for (ulong i = 0; i < c->Captures; i++)
            {
                Attempt* a = i == 0 ? &c->First : &c->Second;
                Token(console, serial, 8);
                WriteUnsignedDecimal(console, serial, id);
                Value(console, serial, 9, i);
                Value(console, serial, 10, a->EnterOrder);
                Value(console, serial, 11, a->ReturnOrder);
                Value(console, serial, 12, a->ExitOrder);
                Value(console, serial, 13, a->CaptureStatus);
                Value(console, serial, 14, a->Key);
                Value(console, serial, 15, a->MapBytes);
                Value(console, serial, 16, a->DescriptorBytes);
                Value(console, serial, 17, a->DescriptorVersion);
                Value(console, serial, 18, a->ExitStatus);
                WriteCrLf(console, serial);
            }
            Token(console, serial, 19);
            WriteUnsignedDecimal(console, serial, id);
            Value(console, serial, 20, c->Status);
            Value(console, serial, 21, c->Windows);
            Value(console, serial, 22, c->Captures);
            Value(console, serial, 23, c->Exits);
            Value(console, serial, 24, c->Reuse);
            Value(console, serial, 25, c->Preserved);
            WriteCrLf(console, serial);
        }
        private static void Value(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, int token, ulong value)
        {
            Token(console, serial, token);
            WriteUnsignedDecimal(console, serial, value);
        }
        private static void Packed(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong word, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Write(console, serial, (byte)word);
                word >>= 8;
            }
        }
        private static void Token(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, int token)
        {
            switch (token)
            {
                case 0: // TEST_START m03.005\r\n
                    Packed(console, serial, 0x4154535F54534554UL, 8);
                    Packed(console, serial, 0x302E33306D205452UL, 8);
                    Packed(console, serial, 0x000000000A0D3530UL, 4);
                    return;
                case 1: // TEST_FAIL m03.005 final_map_retry\r\n
                    Packed(console, serial, 0x4941465F54534554UL, 8);
                    Packed(console, serial, 0x30302E33306D204CUL, 8);
                    Packed(console, serial, 0x5F6C616E69662035UL, 8);
                    Packed(console, serial, 0x727465725F70616DUL, 8);
                    Packed(console, serial, 0x00000000000A0D79UL, 3);
                    return;
                case 2: // M03_FINAL_MAP_IMAGE handle=
                    Packed(console, serial, 0x414E49465F33304DUL, 8);
                    Packed(console, serial, 0x4D495F50414D5F4CUL, 8);
                    Packed(console, serial, 0x646E616820454741UL, 8);
                    Packed(console, serial, 0x00000000003D656CUL, 3);
                    return;
                case 3: //  getMemoryMap=
                    Packed(console, serial, 0x6F6D654D74656720UL, 8);
                    Packed(console, serial, 0x00003D70614D7972UL, 6);
                    return;
                case 4: //  buffer=
                    Packed(console, serial, 0x3D72656666756220UL, 8);
                    return;
                case 5: //  capacity=
                    Packed(console, serial, 0x7469636170616320UL, 8);
                    Packed(console, serial, 0x0000000000003D79UL, 2);
                    return;
                case 6: // CLROS_M03_005_READY controlled=true realExitBootServices=false captures=1 exits=1 reuseRejected=true\r\nTEST_PASS m03.005\r\nTEST_START m03.006\r\n
                    Packed(console, serial, 0x304D5F534F524C43UL, 8);
                    Packed(console, serial, 0x45525F3530305F33UL, 8);
                    Packed(console, serial, 0x746E6F6320594441UL, 8);
                    Packed(console, serial, 0x743D64656C6C6F72UL, 8);
                    Packed(console, serial, 0x6C61657220657572UL, 8);
                    Packed(console, serial, 0x746F6F4274697845UL, 8);
                    Packed(console, serial, 0x7365636976726553UL, 8);
                    Packed(console, serial, 0x632065736C61663DUL, 8);
                    Packed(console, serial, 0x3D73657275747061UL, 8);
                    Packed(console, serial, 0x3D73746978652031UL, 8);
                    Packed(console, serial, 0x5265737565722031UL, 8);
                    Packed(console, serial, 0x3D64657463656A65UL, 8);
                    Packed(console, serial, 0x45540A0D65757274UL, 8);
                    Packed(console, serial, 0x20535341505F5453UL, 8);
                    Packed(console, serial, 0x0D3530302E33306DUL, 8);
                    Packed(console, serial, 0x54535F545345540AUL, 8);
                    Packed(console, serial, 0x2E33306D20545241UL, 8);
                    Packed(console, serial, 0x0000000A0D363030UL, 5);
                    return;
                case 7: // CLROS_M03_006_READY controlled=true realExitBootServices=false cases=2 maxAttempts=2 captures=4 exits=4 exhausted=1\r\nTEST_PASS m03.006\r\n
                    Packed(console, serial, 0x304D5F534F524C43UL, 8);
                    Packed(console, serial, 0x45525F3630305F33UL, 8);
                    Packed(console, serial, 0x746E6F6320594441UL, 8);
                    Packed(console, serial, 0x743D64656C6C6F72UL, 8);
                    Packed(console, serial, 0x6C61657220657572UL, 8);
                    Packed(console, serial, 0x746F6F4274697845UL, 8);
                    Packed(console, serial, 0x7365636976726553UL, 8);
                    Packed(console, serial, 0x632065736C61663DUL, 8);
                    Packed(console, serial, 0x6D20323D73657361UL, 8);
                    Packed(console, serial, 0x706D657474417861UL, 8);
                    Packed(console, serial, 0x70616320323D7374UL, 8);
                    Packed(console, serial, 0x20343D7365727574UL, 8);
                    Packed(console, serial, 0x20343D7374697865UL, 8);
                    Packed(console, serial, 0x6574737561687865UL, 8);
                    Packed(console, serial, 0x5345540A0D313D64UL, 8);
                    Packed(console, serial, 0x6D20535341505F54UL, 8);
                    Packed(console, serial, 0x0A0D3630302E3330UL, 8);
                    return;
                case 8: // M03_FINAL_MAP_CALL case=
                    Packed(console, serial, 0x414E49465F33304DUL, 8);
                    Packed(console, serial, 0x41435F50414D5F4CUL, 8);
                    Packed(console, serial, 0x3D65736163204C4CUL, 8);
                    return;
                case 9: //  index=
                    Packed(console, serial, 0x003D7865646E6920UL, 7);
                    return;
                case 10: //  enter=
                    Packed(console, serial, 0x003D7265746E6520UL, 7);
                    return;
                case 11: //  returned=
                    Packed(console, serial, 0x656E727574657220UL, 8);
                    Packed(console, serial, 0x0000000000003D64UL, 2);
                    return;
                case 12: //  exit=
                    Packed(console, serial, 0x00003D7469786520UL, 6);
                    return;
                case 13: //  captureStatus=
                    Packed(console, serial, 0x6572757470616320UL, 8);
                    Packed(console, serial, 0x003D737574617453UL, 7);
                    return;
                case 14: //  key=
                    Packed(console, serial, 0x0000003D79656B20UL, 5);
                    return;
                case 15: //  mapBytes=
                    Packed(console, serial, 0x6574794270616D20UL, 8);
                    Packed(console, serial, 0x0000000000003D73UL, 2);
                    return;
                case 16: //  descriptorBytes=
                    Packed(console, serial, 0x7069726373656420UL, 8);
                    Packed(console, serial, 0x7365747942726F74UL, 8);
                    Packed(console, serial, 0x000000000000003DUL, 1);
                    return;
                case 17: //  descriptorVersion=
                    Packed(console, serial, 0x7069726373656420UL, 8);
                    Packed(console, serial, 0x6973726556726F74UL, 8);
                    Packed(console, serial, 0x00000000003D6E6FUL, 3);
                    return;
                case 18: //  exitStatus=
                    Packed(console, serial, 0x6174537469786520UL, 8);
                    Packed(console, serial, 0x000000003D737574UL, 4);
                    return;
                case 19: // M03_FINAL_MAP_RESULT case=
                    Packed(console, serial, 0x414E49465F33304DUL, 8);
                    Packed(console, serial, 0x45525F50414D5F4CUL, 8);
                    Packed(console, serial, 0x73616320544C5553UL, 8);
                    Packed(console, serial, 0x0000000000003D65UL, 2);
                    return;
                case 20: //  status=
                    Packed(console, serial, 0x3D73757461747320UL, 8);
                    return;
                case 21: //  windows=
                    Packed(console, serial, 0x73776F646E697720UL, 8);
                    Packed(console, serial, 0x000000000000003DUL, 1);
                    return;
                case 22: //  captures=
                    Packed(console, serial, 0x6572757470616320UL, 8);
                    Packed(console, serial, 0x0000000000003D73UL, 2);
                    return;
                case 23: //  exits=
                    Packed(console, serial, 0x003D737469786520UL, 7);
                    return;
                case 24: //  reuse=
                    Packed(console, serial, 0x003D657375657220UL, 7);
                    return;
                case 25: //  preserved=
                    Packed(console, serial, 0x7672657365727020UL, 8);
                    Packed(console, serial, 0x00000000003D6465UL, 3);
                    return;
            }
        }
    }
#endif
}
#pragma warning restore 649
