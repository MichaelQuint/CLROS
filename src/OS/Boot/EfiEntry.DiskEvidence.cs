#if M16_025_PROBE
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Boot;

#pragma warning disable 649 // Eight contiguous caller-owned records are written through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // One bounded pump can complete/copy and submit its next request. Keep
        // their original order without performing serial I/O inside callbacks.
        private struct NativeDiskRecord
        {
            internal Block.Observation Observation;
            internal fixed byte Bytes[512];
        }
        private struct NativeDiskEvidence
        {
            internal NativeDiskRecord A, B, C, D, E, F, G, H;
            internal ulong Generation, CopiedRecords;
            internal uint Count, Total;
            internal bool Enabled, Failed;
        }
        private const uint NativeDiskEvidenceCapacity = 8, NativeDiskEvidenceLimit = 65536;
        private static uint SelectedNativeDiskEvidenceLimit => OS.Kernel.Diagnostics.Probes.EnableM10018FullProbe ?
            OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumDeviceRecords : NativeDiskEvidenceLimit;

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ObserveNativeDisk(void* context, Block.Observation* observation, byte* bytes)
        {
            NativeDiskOwner* o = (NativeDiskOwner*)context;
            if (o == null || observation == null || observation->Owner != (ulong)(&o->Device) ||
                observation->Epoch != NativeDiskEpoch || observation->Generation == 0 ||
                observation->Kind is not (Block.ObservationKind.Submitted or Block.ObservationKind.CompletionValidated or Block.ObservationKind.CopyReady)) return 0;
            NativeDiskEvidence* evidence = &o->Evidence;
            if (evidence->Failed) return 0;
            if (!evidence->Enabled) return 1;
            if (observation->Kind == Block.ObservationKind.Submitted) evidence->Generation = observation->Generation;
            // A read admitted before arming retains its earlier scope.
            if (evidence->Generation != observation->Generation) return 1;
            bool copy = observation->Kind == Block.ObservationKind.CopyReady;
            if (evidence->Count == NativeDiskEvidenceCapacity || evidence->Total == SelectedNativeDiskEvidenceLimit ||
                (copy ? bytes == null || observation->Bytes != 512 : bytes != null))
            { evidence->Failed = true; return 0; }
            NativeDiskRecord* record = &evidence->A + evidence->Count;
            record->Observation = *observation;
            if (copy) for (uint i = 0; i < 512; i++) record->Bytes[i] = bytes[i];
            evidence->Count++; evidence->Total++; return 1;
        }

        private static bool ArmNativeDiskEvidence(NativeDiskOwner* o)
        {
            if (o == null || o->Phase != 7 || !o->Device.Running || !o->Mount.Ready || o->Evidence.Enabled ||
                o->Evidence.Failed || o->Evidence.Count != 0) return false;
            o->Evidence.Enabled = true; return true;
        }

#if M10_018_PROBE
        // The post-readiness boundary comes from the actual successful listen,
        // not process launch or the earlier mount. A pre-arming request keeps
        // its prior scope and is excluded by the generation observation rule.
        private static bool ArmLinuxStaticDisk(Storage* state, ulong now)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
                return o->Boot == state && ArmNativeDiskEvidence(o) &&
                    LinuxText(state, "M10_018_DISK_READY submitted=") && LinuxNumber(state, o->Device.Submitted) &&
                    LinuxText(state, " completed=") && LinuxNumber(state, o->Device.Completed) &&
                    LinuxText(state, " copied=") && LinuxNumber(state, o->Device.Copied) &&
                    LinuxText(state, " clock=") && LinuxNumber(state, now) && LinuxText(state, "\r\n");
        }
#endif

        // Invoked only after the actual bounded block/filesystem pump returns.
        // CopyReady is merely a candidate; the committed counter must match
        // before its bytes may be identified as an actual owned-buffer copy.
        private static bool FlushNativeDiskEvidence(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Evidence.Failed) return false;
                NativeDiskEvidence* evidence = &o->Evidence;
                for (uint i = 0; i < evidence->Count; i++)
                {
                    NativeDiskRecord* record = &evidence->A + i;
                    var value = record->Observation;
                    if (value.Kind == Block.ObservationKind.CopyReady && o->Device.Copied != value.Sequence ||
                        !ReadNetworkTime(state, out ulong now) || !WriteNativeDiskRecord(state, record, now))
                    { evidence->Failed = true; return false; }
                    if (value.Kind == Block.ObservationKind.CopyReady) evidence->CopiedRecords++;
                    *record = default;
                }
                evidence->Count = 0; return true;
            }
        }

        private static bool WriteNativeDiskRecord(Storage* state, NativeDiskRecord* record, ulong now)
        {
            var value = record->Observation;
            if (!LinuxText(state, "M16_023_IO kind=") || !LinuxNumber(state, (uint)value.Kind) ||
                !LinuxText(state, " owner=") || !LinuxNumber(state, value.Owner) ||
                !LinuxText(state, " epoch=") || !LinuxNumber(state, value.Epoch) ||
                !LinuxText(state, " generation=") || !LinuxNumber(state, value.Generation) ||
                !LinuxText(state, " sector=") || !LinuxNumber(state, value.Sector) ||
                !LinuxText(state, " sequence=") || !LinuxNumber(state, value.Sequence) ||
                !LinuxText(state, " bytes=") || !LinuxNumber(state, value.Bytes) ||
                !LinuxText(state, " available=") || !LinuxNumber(state, value.AvailableIndex) ||
                !LinuxText(state, " consumed=") || !LinuxNumber(state, value.ConsumedIndex) ||
                !LinuxText(state, " clock=") || !LinuxNumber(state, now)) return false;
            if (value.Kind == Block.ObservationKind.CopyReady)
            {
                if (!LinuxText(state, " copied=committed data=")) return false;
                const string hex = "0123456789abcdef";
                for (uint i = 0; i < 512; i++)
                    if (!LinuxByte(state, (byte)hex[record->Bytes[i] >> 4]) ||
                        !LinuxByte(state, (byte)hex[record->Bytes[i] & 15])) return false;
            }
            return LinuxText(state, "\r\n");
        }
    }
}
#pragma warning restore 649
#endif
