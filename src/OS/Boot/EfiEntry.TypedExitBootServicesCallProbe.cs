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
    // Controlled ABI proof only. This never receives a firmware service table.
    // Storage is the caller's mapped, stable, serialized POD; it is never reset.
    internal static partial class TypedExitBootServicesCallProbe
    {
        private const OS.Kernel.Platform.FirmwareServicesMode Available = OS.Kernel.Platform.FirmwareServicesMode.BootServicesAvailable;

        private static Storage s_storage;
        private static ulong s_totalEntries;
        private static TypedExitBootServicesState* s_activeState;
        private static Observation* s_activeObservation;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Save(TypedExitBootServicesState* state, Snapshot* snapshot)
        {
            for (int i = 0; i < 40; i++)
                snapshot->Bytes[i] = ((byte*)state)[i];
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool Same(TypedExitBootServicesState* state, Snapshot* snapshot)
        {
            for (int i = 0; i < 40; i++)
                if (((byte*)state)[i] != snapshot->Bytes[i])
                    return false;
            return true;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static bool IsZero(Storage* storage)
        {
            for (int i = 0; i < sizeof(Storage); i++)
                if (((byte*)storage)[i] != 0)
                    return false;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong Controlled(void* imageHandle, ulong key)
        {
            s_totalEntries++;
            TypedExitBootServicesState* state = s_activeState;
            Observation* o = s_activeObservation;
            if (state == null || o == null)
                return 0x8000000000000002UL;
            o->Calls++;
            o->Handle = (ulong)imageHandle;
            o->Key = key;
            o->Phase = (ulong)state->Phase;
            if (o->Reenter != 0)
            {
                ulong entries = s_totalEntries;
                Snapshot before = default;
                Save(state, &before);
                TypedExitBootServicesResult denied = TypedNoStdlibExitBootServicesCallAdapter.Attempt(ref *state, Available, 0);
                o->ReentryAttempt = (ulong)denied.Status;
                if (denied.CallIssued || s_totalEntries != entries || !Same(state, &before))
                    return 0x8000000000000002UL;
                TypedExitBootServicesStatus preparation = TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref *state, Available, &Controlled, imageHandle);
                o->ReentryPrepare = (ulong)preparation;
                if (s_totalEntries != entries || !Same(state, &before))
                    return 0x8000000000000002UL;
                o->ReentryPreserved = Same(state, &before) ? 1UL : 0UL;
            }
            o->Status = o->ExpectedStatus;
            return o->Status;
        }

        private static bool Call(TypedExitBootServicesState* state, Observation* o, void* imageHandle,
            ulong key, ulong status, bool reenter)
        {
            ulong entries = s_totalEntries;
            if (TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref *state, Available, &Controlled, imageHandle) != TypedExitBootServicesStatus.Prepared)
                return false;
            Snapshot before = default;
            Save(state, &before);
            TypedExitBootServicesResult denied = TypedNoStdlibExitBootServicesCallAdapter.Attempt(ref *state, OS.Kernel.Platform.FirmwareServicesMode.BootServicesExited, key);
            if (denied.CallIssued || s_totalEntries != entries || denied.Status != TypedExitBootServicesStatus.FirmwareServicesUnavailable || !Same(state, &before))
                return false;
            TypedExitBootServicesStatus preparation = TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref *state, Available, &Controlled, imageHandle);
            if (preparation != TypedExitBootServicesStatus.AlreadyPrepared || !Same(state, &before) || s_totalEntries != entries)
                return false;
            o->ExpectedStatus = status;
            o->Reenter = reenter ? 1UL : 0UL;
            s_activeState = state;
            s_activeObservation = o;
            TypedExitBootServicesResult result = TypedNoStdlibExitBootServicesCallAdapter.Attempt(ref *state, Available, key);
            s_activeState = null;
            s_activeObservation = null;
            if (s_totalEntries != entries + 1 || result.Status != TypedExitBootServicesStatus.Returned || result.Mode != Available || result.FirmwareStatus != status ||
                !result.CallIssued || result.Succeeded != (status == 0) || result.MapKeyRejected != (status == 0x8000000000000002UL) ||
                o->Calls != 1 || o->Handle != (ulong)imageHandle || o->Key != key || o->Status != status || o->Phase != 2 ||
                state->Phase != TypedExitBootServicesPhase.Returned || state->ImageHandle != (ulong)imageHandle || state->MapKey != key || state->FirmwareStatus != status)
                return false;
            if (reenter && (o->ReentryAttempt != 8 || o->ReentryPrepare != 8 || o->ReentryPreserved != 1))
                return false;
            if (!reenter && (o->ReentryAttempt != 0 || o->ReentryPrepare != 0 || o->ReentryPreserved != 0))
                return false;
            Save(state, &before);
            denied = TypedNoStdlibExitBootServicesCallAdapter.Attempt(ref *state, Available, 0);
            if (denied.CallIssued || s_totalEntries != entries + 1 || !Same(state, &before))
                return false;
            o->ReuseAttempt = (ulong)denied.Status;
            preparation = TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref *state, Available, &Controlled, imageHandle);
            if (s_totalEntries != entries + 1 || !Same(state, &before))
                return false;
            o->ReusePrepare = (ulong)preparation;
            o->ReusePreserved = Same(state, &before) && o->Calls == 1 ? 1UL : 0UL;
            return o->ReuseAttempt == 9 && o->ReusePrepare == 9 && o->ReusePreserved == 1;
        }

        internal static bool Execute(Storage* storage, void* imageHandle)
        {
            // Only known mapped storage is accepted by this internal coordinator.
            // Numeric admission cannot establish an arbitrary pointer's mapping.
            if (storage == null || (ulong)storage % 8 != 0 || imageHandle == null || sizeof(TypedExitBootServicesState) != 40 ||
                s_totalEntries != 0 || s_activeState != null || s_activeObservation != null || !IsZero(storage))
                return false;
            TypedExitBootServicesState denied = default;
            Snapshot empty = default;
            Save(&denied, &empty);
            if (TypedNoStdlibExitBootServicesCallAdapter.Attempt(ref denied, Available, 0).Status != TypedExitBootServicesStatus.NotPrepared || s_totalEntries != 0 || !Same(&denied, &empty) ||
                TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref denied, Available, null, imageHandle) != TypedExitBootServicesStatus.MissingFunctionPointer || s_totalEntries != 0 || !Same(&denied, &empty) ||
                TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref denied, Available, &Controlled, null) != TypedExitBootServicesStatus.MissingImageHandle || s_totalEntries != 0 || !Same(&denied, &empty) ||
                TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref denied, OS.Kernel.Platform.FirmwareServicesMode.UefiGoneSimulated, &Controlled, imageHandle) != TypedExitBootServicesStatus.FirmwareServicesUnavailable || s_totalEntries != 0 || !Same(&denied, &empty) ||
                TypedNoStdlibExitBootServicesCallAdapter.Prepare(ref denied, OS.Kernel.Platform.FirmwareServicesMode.BootServicesExited, &Controlled, imageHandle) != TypedExitBootServicesStatus.FirmwareServicesUnavailable || s_totalEntries != 0 || !Same(&denied, &empty))
                return false;
            storage->Phase = 1;
            storage->AdmissionPreserved = 1;
            if (!Call(&storage->Zero, &storage->ZeroObservation, imageHandle, 0, 0, false) ||
                !Call(&storage->High, &storage->HighObservation, imageHandle, 0x8000000000000000UL, 0x8000000000000002UL, false) ||
                !Call(&storage->AllBits, &storage->AllBitsObservation, imageHandle, 0xFFFFFFFFFFFFFFFFUL, 0xC000000000000123UL, false) ||
                !Call(&storage->Reentry, &storage->ReentryObservation, imageHandle, 0x8000000000000000UL, 0, true))
                return false;
            if (s_totalEntries != 4)
                return false;
            storage->ReadyPreserved = 1;
            storage->Phase = 2;
            return true;
        }

        internal static bool Run(void* imageHandle, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            Token(console, serial, 0);
            fixed (Storage* s = &s_storage)
            {
                if (!Execute(s, imageHandle))
                {
                    Token(console, serial, 1);
                    return false;
                }
                Token(console, serial, 21);
                WriteUnsignedDecimal(console, serial, (ulong)imageHandle);
                Token(console, serial, 3);
                WriteUnsignedDecimal(console, serial, (ulong)(delegate* unmanaged<void*, ulong, ulong>)&Controlled);
                Token(console, serial, 4);
                WriteUnsignedDecimal(console, serial, (ulong)s);
                Token(console, serial, 5);
                WriteUnsignedDecimal(console, serial, (ulong)sizeof(Storage));
                Token(console, serial, 6);
                WriteUnsignedDecimal(console, serial, (ulong)sizeof(TypedExitBootServicesState));
                WriteCrLf(console, serial);
                Token(console, serial, 7);
                Record(console, serial, 0, &s->Zero, &s->ZeroObservation);
                Record(console, serial, 1, &s->High, &s->HighObservation);
                Record(console, serial, 2, &s->AllBits, &s->AllBitsObservation);
                Record(console, serial, 3, &s->Reentry, &s->ReentryObservation);
                Token(console, serial, 8);
                return true;
            }
        }
        private static void Record(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong id, TypedExitBootServicesState* state, Observation* o)
        {
            Token(console, serial, 9);
            WriteUnsignedDecimal(console, serial, id);
            Token(console, serial, 10);
            WriteUnsignedDecimal(console, serial, (ulong)state);
            Token(console, serial, 2);
            WriteUnsignedDecimal(console, serial, o->Handle);
            Token(console, serial, 11);
            WriteUnsignedDecimal(console, serial, o->Key);
            Token(console, serial, 12);
            WriteUnsignedDecimal(console, serial, o->Status);
            Token(console, serial, 13);
            WriteUnsignedDecimal(console, serial, o->Calls);
            Token(console, serial, 14);
            WriteUnsignedDecimal(console, serial, o->Phase);
            Token(console, serial, 15);
            WriteUnsignedDecimal(console, serial, o->ReuseAttempt);
            Token(console, serial, 16);
            WriteUnsignedDecimal(console, serial, o->ReusePrepare);
            Token(console, serial, 17);
            WriteUnsignedDecimal(console, serial, o->ReusePreserved);
            Token(console, serial, 18);
            WriteUnsignedDecimal(console, serial, o->ReentryAttempt);
            Token(console, serial, 19);
            WriteUnsignedDecimal(console, serial, o->ReentryPrepare);
            Token(console, serial, 20);
            WriteUnsignedDecimal(console, serial, o->ReentryPreserved);
            WriteCrLf(console, serial);
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
                case 0: // TEST_START m03.004\r\n
                    Packed(console, serial, 0x4154535F54534554UL, 8);
                    Packed(console, serial, 0x302E33306D205452UL, 8);
                    Packed(console, serial, 0x000000000A0D3430UL, 4);
                    return;
                case 1: // TEST_FAIL m03.004 controlled_contract\r\n
                    Packed(console, serial, 0x4941465F54534554UL, 8);
                    Packed(console, serial, 0x30302E33306D204CUL, 8);
                    Packed(console, serial, 0x6F72746E6F632034UL, 8);
                    Packed(console, serial, 0x6E6F635F64656C6CUL, 8);
                    Packed(console, serial, 0x000A0D7463617274UL, 7);
                    return;
                case 2: //  handle=
                    Packed(console, serial, 0x3D656C646E616820UL, 8);
                    return;
                case 3: //  callback=
                    Packed(console, serial, 0x6361626C6C616320UL, 8);
                    Packed(console, serial, 0x0000000000003D6BUL, 2);
                    return;
                case 4: //  storage=
                    Packed(console, serial, 0x656761726F747320UL, 8);
                    Packed(console, serial, 0x000000000000003DUL, 1);
                    return;
                case 5: //  storageBytes=
                    Packed(console, serial, 0x656761726F747320UL, 8);
                    Packed(console, serial, 0x00003D7365747942UL, 6);
                    return;
                case 6: //  stateBytes=
                    Packed(console, serial, 0x7942657461747320UL, 8);
                    Packed(console, serial, 0x000000003D736574UL, 4);
                    return;
                case 7: // M03_TYPED_EBS_ADMISSION default=6 missingFunction=3 missingHandle=4 simulated=5 exited=5 readyDenied=5 readyPrepare=7 unchanged=1 calls=0\r\n
                    Packed(console, serial, 0x455059545F33304DUL, 8);
                    Packed(console, serial, 0x44415F5342455F44UL, 8);
                    Packed(console, serial, 0x204E4F495353494DUL, 8);
                    Packed(console, serial, 0x3D746C7561666564UL, 8);
                    Packed(console, serial, 0x6E697373696D2036UL, 8);
                    Packed(console, serial, 0x6F6974636E754667UL, 8);
                    Packed(console, serial, 0x7373696D20333D6EUL, 8);
                    Packed(console, serial, 0x6C646E6148676E69UL, 8);
                    Packed(console, serial, 0x756D697320343D65UL, 8);
                    Packed(console, serial, 0x20353D646574616CUL, 8);
                    Packed(console, serial, 0x353D646574697865UL, 8);
                    Packed(console, serial, 0x6544796461657220UL, 8);
                    Packed(console, serial, 0x7220353D6465696EUL, 8);
                    Packed(console, serial, 0x7065725079646165UL, 8);
                    Packed(console, serial, 0x6E7520373D657261UL, 8);
                    Packed(console, serial, 0x3D6465676E616863UL, 8);
                    Packed(console, serial, 0x3D736C6C61632031UL, 8);
                    Packed(console, serial, 0x00000000000A0D30UL, 3);
                    return;
                case 8: // CLROS_M03_004_READY controlled=true realExitBootServices=false states=4 calls=4 reuse=4 reentry=1 mode=0\r\nTEST_PASS m03.004\r\n
                    Packed(console, serial, 0x304D5F534F524C43UL, 8);
                    Packed(console, serial, 0x45525F3430305F33UL, 8);
                    Packed(console, serial, 0x746E6F6320594441UL, 8);
                    Packed(console, serial, 0x743D64656C6C6F72UL, 8);
                    Packed(console, serial, 0x6C61657220657572UL, 8);
                    Packed(console, serial, 0x746F6F4274697845UL, 8);
                    Packed(console, serial, 0x7365636976726553UL, 8);
                    Packed(console, serial, 0x732065736C61663DUL, 8);
                    Packed(console, serial, 0x20343D7365746174UL, 8);
                    Packed(console, serial, 0x20343D736C6C6163UL, 8);
                    Packed(console, serial, 0x20343D6573756572UL, 8);
                    Packed(console, serial, 0x3D7972746E656572UL, 8);
                    Packed(console, serial, 0x303D65646F6D2031UL, 8);
                    Packed(console, serial, 0x505F545345540A0DUL, 8);
                    Packed(console, serial, 0x2E33306D20535341UL, 8);
                    Packed(console, serial, 0x0000000A0D343030UL, 5);
                    return;
                case 9: // M03_TYPED_EBS_CASE id=
                    Packed(console, serial, 0x455059545F33304DUL, 8);
                    Packed(console, serial, 0x41435F5342455F44UL, 8);
                    Packed(console, serial, 0x00003D6469204553UL, 6);
                    return;
                case 10: //  state=
                    Packed(console, serial, 0x003D657461747320UL, 7);
                    return;
                case 11: //  key=
                    Packed(console, serial, 0x0000003D79656B20UL, 5);
                    return;
                case 12: //  status=
                    Packed(console, serial, 0x3D73757461747320UL, 8);
                    return;
                case 13: //  calls=
                    Packed(console, serial, 0x003D736C6C616320UL, 7);
                    return;
                case 14: //  phase=
                    Packed(console, serial, 0x003D657361687020UL, 7);
                    return;
                case 15: //  reuseAttempt=
                    Packed(console, serial, 0x7441657375657220UL, 8);
                    Packed(console, serial, 0x00003D74706D6574UL, 6);
                    return;
                case 16: //  reusePrepare=
                    Packed(console, serial, 0x7250657375657220UL, 8);
                    Packed(console, serial, 0x00003D6572617065UL, 6);
                    return;
                case 17: //  unchanged=
                    Packed(console, serial, 0x676E6168636E7520UL, 8);
                    Packed(console, serial, 0x00000000003D6465UL, 3);
                    return;
                case 18: //  reentryAttempt=
                    Packed(console, serial, 0x7972746E65657220UL, 8);
                    Packed(console, serial, 0x3D74706D65747441UL, 8);
                    return;
                case 19: //  reentryPrepare=
                    Packed(console, serial, 0x7972746E65657220UL, 8);
                    Packed(console, serial, 0x3D65726170657250UL, 8);
                    return;
                case 20: //  reentryUnchanged=
                    Packed(console, serial, 0x7972746E65657220UL, 8);
                    Packed(console, serial, 0x65676E6168636E55UL, 8);
                    Packed(console, serial, 0x0000000000003D64UL, 2);
                    return;
                case 21: // M03_TYPED_EBS_IMAGE handle=
                    Packed(console, serial, 0x455059545F33304DUL, 8);
                    Packed(console, serial, 0x4D495F5342455F44UL, 8);
                    Packed(console, serial, 0x646E616820454741UL, 8);
                    Packed(console, serial, 0x00000000003D656CUL, 3);
                    return;
            }
        }
    }
#endif
}
#pragma warning restore 649
