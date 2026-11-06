#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE && M04_SEEDED_COLLECTOR_CHURN_PROBE
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// Explicit diagnostic substep of the GC profile. It mutates the original
// collector heap before EBS, after the retained graph and stack checks pass.
internal static unsafe partial class RhpNewFastAllocation
{
    private static ulong s_seededPhase, s_seededChecks, s_seededMinimumWitness;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunSeededCollectorChurn()
    {
        if (s_seededPhase != 0 || s_seededChecks != 0 || s_gcPhase != 5 ||
            s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) ||
            Bits(GcStaticFixture.Get()) != NodeReceipt(5).ObjectReference ||
            ReadStaticCollectorTag(false) != CollectorCookie + 6)
            RejectSeededChurn(1);
        s_seededPhase = 1;
        CheckSeededChurnStack(0);
        ulong child = NodeReceipt(5).ObjectReference;
        ChurnLine(0, TypePlain(), ReadGcRoot().ObjectReference, child, ReadStaticCollectorTag(false));
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcRootStorage* root = &s_gcRoot)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            CollectorModuleAccess module = new()
            {
                Capture = s_gcCapture,
                Mapping = s_gcMapping,
                Owner = (byte*)type,
                Capacity = (ulong)sizeof(GcTypeStorage),
                Handle = s_gcHandle,
                Metadata = s_gcImage
            };
            CollectorStaticRootAccess retained = new()
            {
                Owner = (byte*)root,
                Capacity = (ulong)sizeof(GcRootStorage),
                Cells = s_gcImage,
                SystemObjectMethodTable = GcObjectType()
            };
            for (uint index = 0; index < 3; index++)
            {
                uint seed = index == 0 ? 0U : index == 1 ? 1U : 0xDEADBEEF;
                uint allocations = index == 1 ? 154U : 161U;
                ulong fingerprint = index == 0 ? 0x6159605787538563UL :
                    index == 1 ? 0xF6D01A7B41E92C30UL : 0x747C5A6156456DA2UL;
                s_seededPhase = index + 2;
                ulong before = collecting->State.AllocationCount;
                SeededChurnResult result = SeededAllocationChurnCollectionRootRetentionGuestProbes.Run(
                    module, s_access, &collecting->State, (ulong)sizeof(GcCollectingStorage), TypePlain(), seed, 64,
                    default, retained);
                if (!result.Succeeded || result.CompletedRounds != 64 || result.Collections != 66 ||
                    result.Allocations != allocations || result.Reclaimed != allocations ||
                    result.InitialReclaimed != 0 || result.InitialReclaimedBytes != 0 ||
                    result.BaselineLiveObjects != 2 || result.ReusedAddresses == 0 ||
                    result.Fingerprint != fingerprint || collecting->State.AllocationCount != before + allocations)
                    RejectSeededChurn(2 + index);
                if (!ReadGcRoot().Succeeded || !CollectorReceiptValid(5) ||
                    Bits(GcStaticFixture.Get()) != child || ReadStaticCollectorTag(false) != CollectorCookie + 6 ||
                    s_control.ConstructorCount != 7 || s_collectorStores != 7)
                    RejectSeededChurn(5 + index);
                CheckSeededChurnStack(index + 1);
                ChurnLine(1 + index * 3, seed, result.CompletedRounds, result.Allocations, result.Reclaimed);
                ChurnLine(2 + index * 3, result.BaselineLiveObjects, result.Collections,
                    result.ReusedAddresses, result.Fingerprint);
                ChurnLine(3 + index * 3, collecting->State.AllocationCount, child,
                    ReadStaticCollectorTag(false), s_seededMinimumWitness);
            }
        }
        if (s_seededChecks != 4)
            RejectSeededChurn(8);
        ChurnLine(10, s_seededChecks, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8), s_seededMinimumWitness);
        s_seededPhase = 5;
        Write((byte)'C');
        Write((byte)'L');
        Write((byte)'R');
        Write((byte)'O');
        Write((byte)'S');
        Write((byte)'_');
        Write((byte)'M');
        Write((byte)'0');
        Write((byte)'4');
        Write((byte)'_');
        Write((byte)'0');
        Write((byte)'1');
        Write((byte)'4');
        Write((byte)'_');
        Write((byte)'R');
        Write((byte)'E');
        Write((byte)'A');
        Write((byte)'D');
        Write((byte)'Y');
        End();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckSeededChurnStack(ulong expected)
    {
        ulong witness = 0, address = (ulong)&witness;
        ulong authority = 0, start = 0, end = 0, initialRsp = 0;
        if (s_seededPhase == 0 || s_seededPhase > 4 || s_seededChecks != expected || expected > 3 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            !GcStackContext(address, out authority, out start, out end, out initialRsp) ||
            authority != s_gcStack.Authority || start != s_gcStack.Start || end != s_gcStack.End ||
            initialRsp != s_gcStack.InitialRsp || address > s_gcStack.Witness ||
            GcReadWord(start) != GcStackWord0 || GcReadWord(start + 8) != GcStackWord1)
            RejectSeededChurn(9);
        if (s_seededMinimumWitness == 0 || address < s_seededMinimumWitness)
            s_seededMinimumWitness = address;
        s_seededChecks++;
    }

    private static void RejectSeededChurn(ulong reason)
    {
        ChurnLine(255, reason, s_seededPhase, s_seededChecks, 0);
        while (true)
            System.RuntimeFailFast.Halt();
    }

    private static void ChurnLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Write((byte)'M');
        Write((byte)'0');
        Write((byte)'4');
        Write((byte)'_');
        Write((byte)'C');
        Write((byte)'H');
        Write((byte)'U');
        Write((byte)'R');
        Write((byte)'N');
        Write(32);
        Decimal(stage);
        Write(32);
        Hex(a);
        Write(32);
        Hex(b);
        Write(32);
        Hex(c);
        Write(32);
        Hex(d);
        End();
    }
}
#endif
