#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// One compiler-emitted GC-static cell. It has no initializer or class constructor;
// RunGcStatics publishes its retained base before any accessor is called.
internal static class GcStaticFixture
{
    private static object s_value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Set(object value) => s_value = value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static object Get() => s_value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ref object Address() => ref s_value;
}

// A genuine compiler-described fixed class with one reference series. Its
// constructor and accessors run only after the original allocator is bound.
internal sealed class GcCollectorNode
{
    private GcCollectorNode child;
    private readonly ulong tag;
    internal GcCollectorNode(ulong value) { RhpNewFastAllocation.ConstructorEntered(); tag = value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void SetChild(GcCollectorNode value) => child = value;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal GcCollectorNode GetChild() => child;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ulong GetTag() => tag;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ref GcCollectorNode ChildAddress() => ref child;
}

internal static unsafe partial class RhpNewFastAllocation
{
    private const ulong GcCookie = 0x7143B6A952D08E2F;
    private const ulong GcStackBytes = 33 * 4096, GcStackBudget = 16384;
    private const ulong GcStackWord0 = 0x195A86B4C327D0EF, GcStackWord1 = 0x62C79E1035AB48D6;
    private struct GcStackState
    {
        internal ulong Authority, Start, End, InitialRsp, Witness, MinimumWitness;
        internal ulong Checks, Final0, Final1;
    }
    private static GcStackState s_gcStack;
    private struct GcTypeStorage { internal fixed ulong Words[512]; }
    private struct GcRootStorage { internal fixed ulong Words[128]; }
    private static GcTypeStorage s_gcType;
    private static GcRootStorage s_gcRoot;
    private static EfiLoadedImageRangeCapturePlan s_gcCapture;
    private static M01LoadedImageVirtualMappingEvidence s_gcMapping;
    private static GCStaticReadableWindow s_gcImage;
    private static ulong s_gcHandle, s_gcPhase, s_gcCalls, s_gcField, s_gcOperation;
    private static RhpNewFastResult s_gcValue;
    private struct GcCollectingStorage
    {
        internal RhpNewFastBumpState State;
        internal fixed ulong Records[680];
    }
    private static GcCollectingStorage s_gcCollecting;

    internal static void RunGcStatics(RhpNewFastHeapAccess original,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping)
    {
        ulong stackWitness = 0;
        ulong image = 0;
        if (s_gcPhase != 0 || !capture.Succeeded || !mapping.IsValidFor(capture) ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out image) != AddressValueStatus.Success)
            RejectGc(1);
        s_gcPhase = 1; // Never retry a partially completed registration/allocation.
        s_gcCapture = capture; s_gcMapping = mapping;
        s_gcImage = new(image, capture.Range.ImageSize);
        GcLine(0, image, capture.Range.ImageSize, 0, 0);
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcRootStorage* root = &s_gcRoot)
        {
            RhpNewFastBumpState* b = &collecting->State;
            if (RhpNewFastAllocationCore.Prepare(original, a, b, c, r,
                    (byte*)image, capture.Range.ImageSize) != RhpAdmissionStatus.Success ||
                (ulong)sizeof(GcCollectingStorage) != GuestTestRhpNewFast.RequiredCollectingStateBytes ||
                !GuestTestRhpNewFast.InitializeOnce(*a, b, (ulong)sizeof(GcCollectingStorage)).Succeeded ||
                GuestTestRhpNewFast.EnableCollection(*a, b, (ulong)sizeof(GcCollectingStorage)) != RhpNewFastStatus.Success)
                RejectGc(2);
            TypeManagerModuleResult registered = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(
                capture, mapping, (byte*)type, (ulong)sizeof(GcTypeStorage));
            if (!registered.Succeeded) RejectGc(3);
            s_gcHandle = registered.Handle;
            TypeManagerModuleResult entries = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
                capture, mapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, 201);
            GcLine(1, (ulong)entries.Status, entries.Section, unchecked((ulong)entries.Length), s_gcHandle);
            if (!entries.Succeeded || entries.Length != 4) RejectGc(4);
            ArmGcStack((ulong)&stackWitness);
            GCStaticRootResult materialized = GCStaticRootsMalformedMetadata.MaterializeOnce(
                capture, mapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_gcImage, s_gcImage, GcObjectType(), *a, b, GuestTestRhpNewFast.RequiredCollectingStateBytes,
                (byte*)root, (ulong)sizeof(GcRootStorage));
            if (!materialized.Succeeded)
            {
                GcLine(2, (ulong)materialized.Status, (ulong)materialized.MetadataStatus,
                    (ulong)materialized.ModuleStatus, (ulong)materialized.HeapStatus);
                RejectGc(5);
            }
            CheckGcStack(0);
            if (b->AllocationCount != 1 || materialized.SeriesBytes != 8 ||
                Bits(GcStaticFixture.Get()) != 0) RejectGc(6);
            s_gcField = GcFieldAddress();
            if (s_gcField != materialized.SeriesStart || (s_gcField & 7) != 0) RejectGc(7);
            GcLine(3, materialized.Cell, materialized.ObjectReference, s_gcField, materialized.AllocationBytes);
            GCStaticRootResult repeated = GCStaticRootsMalformedMetadata.MaterializeOnce(
                capture, mapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_gcImage, s_gcImage, GcObjectType(), *a, b, GuestTestRhpNewFast.RequiredCollectingStateBytes,
                (byte*)root, (ulong)sizeof(GcRootStorage));
            if (!repeated.Succeeded || repeated.ObjectReference != materialized.ObjectReference ||
                b->AllocationCount != 1) RejectGc(8);
            CheckGcStack(1);

            s_gcPhase = 2;
            RhpPlainFixture value = NewPlain(GcCookie);
            if (s_gcPhase != 3 || Bits(value) != s_gcValue.ObjectReference ||
                ReadPlain(value) != GcCookie || c->ConstructorCount != 1 || b->AllocationCount != 2)
                RejectGc(9);
            CheckGcStack(2);
            GcLine(4, s_gcValue.ObjectReference, s_gcValue.MethodTableToken,
                s_gcValue.UsedBefore, s_gcValue.UsedAfter);
            GcStaticFixture.Set(value);
            if (s_gcCalls != 1 || Bits(GcStaticFixture.Get()) != Bits(value) ||
                GcReadWord(s_gcField) != Bits(value) || !ReadGcRoot().Succeeded) RejectGc(10);
            CheckGcStack(3);
            GcLine(5, s_gcOperation, s_gcCalls, GcReadWord(s_gcField), ReadPlain(value));
            GcStaticFixture.Set(null);
            if (s_gcCalls != 2 || Bits(GcStaticFixture.Get()) != 0 ||
                GcReadWord(s_gcField) != 0 || !ReadGcRoot().Succeeded ||
                ReadPlain(value) != GcCookie || b->AllocationCount != 2) RejectGc(11);
            CheckGcStack(4);
            s_gcPhase = 4;
            GcLine(6, s_gcOperation, s_gcCalls, b->AllocationCount, b->UsedBytes);
            GcLine(8, s_gcStack.Checks, s_gcStack.Final0, s_gcStack.Final1, s_gcStack.MinimumWitness);
            RunCollectorGraph();
        }
    }

    // Authenticate the existing owned stack before its only two sentinel writes.
    // The witness is a real local address, not a claimed current RSP. Native
    // review separately bounds the deepest reachable frames below this point.
    private static void ArmGcStack(ulong witness)
    {
        ulong authority = 0, start = 0, end = 0, initialRsp = 0;
        if (s_gcPhase != 1 || s_gcStack.Start != 0 ||
            !GcStackContext(witness, out authority, out start, out end, out initialRsp))
            RejectGc(15);
        s_gcStack = new GcStackState { Authority = authority, Start = start, End = end,
            InitialRsp = initialRsp, Witness = witness, MinimumWitness = witness };
        *(ulong*)start = GcStackWord0;
        *(ulong*)(start + 8) = GcStackWord1;
        if (GcReadWord(start) != GcStackWord0 || GcReadWord(start + 8) != GcStackWord1) RejectGc(16);
        GcLine(7, start, end, initialRsp, witness);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckGcStack(ulong expected)
    {
        ulong witness = 0, address = (ulong)&witness;
        ulong authority = 0, start = 0, end = 0, initialRsp = 0;
        if (expected >= 5 || s_gcStack.Checks != expected ||
            s_gcPhase != (expected < 2 ? 1UL : 3UL) ||
            s_gcCalls != (expected < 3 ? 0UL : expected - 2) ||
            !GcStackContext(address, out authority, out start, out end, out initialRsp) ||
            authority != s_gcStack.Authority || start != s_gcStack.Start || end != s_gcStack.End ||
            initialRsp != s_gcStack.InitialRsp || address > s_gcStack.Witness)
            RejectGc(17);
        // Read-only checks: a corrupted sentinel is never silently rearmed.
        ulong word0 = GcReadWord(start), word1 = GcReadWord(start + 8);
        if (word0 != GcStackWord0 || word1 != GcStackWord1) RejectGc(18);
        s_gcStack.Final0 = word0; s_gcStack.Final1 = word1; s_gcStack.Checks++;
        if (address < s_gcStack.MinimumWitness) s_gcStack.MinimumWitness = address;
    }

    private static bool GcStackContext(ulong witness, out ulong authority,
        out ulong start, out ulong end, out ulong initialRsp)
    {
        authority = start = end = initialRsp = 0;
        if (!EfiEntry.TryGetOriginalReferenceStack(out authority, out start, out end, out initialRsp) ||
            start == 0 || (start & 4095) != 0 || start > ulong.MaxValue - GcStackBytes ||
            end != start + GcStackBytes || initialRsp != end - 40 ||
            (witness & 7) != 0 || witness < start + 16 + GcStackBudget || witness > initialRsp - 8 ||
            !GcStackSeparate(start, end, authority, 768) ||
            !GcStackSeparate(start, end, s_gcImage.Start, s_gcImage.Bytes)) return false;
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpNewFastBumpState* b = &collecting->State;
            // Ready authenticates every original pointer and exact capacity
            // before the disjointness checks read the retained access fields.
            return RhpNewFastAllocationCore.Ready(a, b, c, r) == RhpAdmissionStatus.Success &&
                GcStackSeparate(start, end, (ulong)a, (ulong)sizeof(RhpNewFastHeapAccess)) &&
                GcStackSeparate(start, end, (ulong)b, (ulong)sizeof(GcCollectingStorage)) &&
                GcStackSeparate(start, end, (ulong)c, (ulong)sizeof(RhpAllocationControl)) &&
                GcStackSeparate(start, end, (ulong)r, (ulong)sizeof(RhpObservationStorage)) &&
                GcStackSeparate(start, end, (ulong)a->Allocator, 56) &&
                GcStackSeparate(start, end, (ulong)a->Nodes, 48) &&
                GcStackSeparate(start, end, (ulong)a->Reservations, 200) &&
                GcStackSeparate(start, end, (ulong)a->Heap, a->HeapCapacity) &&
                GcStackSeparate(start, end, (ulong)a->Backing, a->BackingCapacity);
        }
    }

    private static bool GcStackSeparate(ulong start, ulong end, ulong at, ulong bytes) =>
        at != 0 && bytes != 0 && at <= ulong.MaxValue - bytes && (at + bytes <= start || at >= end);

    // This is the only new allocation role. The static base and this real value
    // share the collecting owner; the six-role reference diagnostic stays separate.
    private static ulong AllocateGcValue(ulong token)
    {
        if (s_gcPhase == 4) return AllocateCollectorNode(token);
        if (s_gcPhase != 2 || token != TypePlain() || s_gcValue.ObjectReference != 0 ||
            !ReadGcRoot().Succeeded) RejectGc(12);
        if (!RhpNewFastAllocationCore.TryReadPrefix((byte*)s_gcImage.Start, s_gcImage.Bytes,
                token, TypePlain(), out NativeAotMethodTablePrefix prefix, out _, out _, out _))
            RejectGc(13);
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            RhpNewFastBumpState* bump = &collecting->State;
            RhpNewFastResult result = GuestTestRhpNewFast.TryAllocate(s_access, bump,
                GuestTestRhpNewFast.RequiredCollectingStateBytes, NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian,
                prefix, token);
            if (!result.AllocationSucceeded || result.AllocationCount != 2 || result.BytesConsumed != 24 ||
                GcReadWord(result.MappedAllocationStart) != 0 ||
                GcReadWord(result.ObjectReference) != token || GcReadWord(result.ObjectReference + 8) != 0)
                RejectGc(14);
            s_gcValue = result;
            s_gcPhase = 3;
            return result.ObjectReference;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ApplyGcReference(ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        if (s_gcPhase == 4) return ApplyCollectorReference(operation, destination, operand, bytes);
        // Admit only the two actual scalar stores. No bulk, stack, array or
        // foreign-reference route falls through to an unchecked memory write.
        if (s_gcPhase != 3 || s_gcCalls > 1 || (operation != 1 && operation != 2) || bytes != 8 ||
            destination != s_gcField || destination != GcFieldAddress() ||
            operand != (s_gcCalls == 0 ? s_gcValue.ObjectReference : 0)) return 0;
        GCStaticRootResult root = ReadGcRoot();
        if (!root.Succeeded || root.SeriesStart != destination || root.SeriesBytes != 8 ||
            s_gcValue.ObjectReference == 0 || s_gcValue.MethodTableToken != TypePlain()) return 0;
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            RhpNewFastBumpState* bump = &collecting->State;
            if (GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, bump,
                    GuestTestRhpNewFast.RequiredCollectingStateBytes, s_gcValue) != RhpNewFastStatus.Success ||
                GcReadWord(s_gcValue.ObjectReference + 8) != GcCookie) return 0;
            CheckedReferenceBulkResult result = CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryStore(
                s_access, CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage,
                destination - (ulong)s_access.Backing, operand);
            if (!result.Succeeded || result.BytesWritten != 8) return 0;
            s_gcOperation = operation; s_gcCalls++;
            return 1;
        }
    }

    private static GCStaticRootResult ReadGcRoot()
    {
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcRootStorage* root = &s_gcRoot)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            RhpNewFastBumpState* bump = &collecting->State;
            return GCStaticRootsMalformedMetadata.TryGetRetainedRoot(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, s_gcImage, GcObjectType(),
                s_access, bump, GuestTestRhpNewFast.RequiredCollectingStateBytes, (byte*)root, (ulong)sizeof(GcRootStorage));
        }
    }

    private const ulong CollectorCookie = 0xC011EC7000000000;
    private struct GcNodeReceipts
    {
        internal RhpNewFastResult Node0;
#pragma warning disable CS0649 // Written through bounded sequential POD slots.
        internal RhpNewFastResult Node1, Node2, Node3, Node4, Node5;
#pragma warning restore CS0649
        internal ulong Count;
    }
    private static GcNodeReceipts s_gcNodes;
    private static ulong s_collectorPhase, s_collectorStores, s_collectorFieldOffset;

    private static void RunCollectorGraph()
    {
        if (s_gcPhase != 4 || s_gcCalls != 2 || s_collectorPhase != 0 || s_gcNodes.Count != 0 ||
            Bits(GcStaticFixture.Get()) != 0 || !ReadGcRoot().Succeeded) RejectGc(101);
        fixed (GcTypeStorage* type = &s_gcType)
        {
            CollectorTypeMapResult map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(
                NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, CollectorNodeType());
            if (!map.Succeeded || map.Prefix.BaseSize != 32 || map.SeriesCount != 1 ||
                !map.TryGetSeries(0, out CollectorReferenceSeries series) || series.Bytes != 8)
                RejectGc(102);
            map.TryGetSeries(0, out CollectorReferenceSeries admitted);
            s_collectorFieldOffset = admitted.OffsetFromReference;
            CollectorLine(0, map.MethodTable, map.Prefix.BaseSize, s_collectorFieldOffset, admitted.Bytes);
        }
        s_collectorPhase = 1;
        BuildCollectorGraph(); // All temporary managed locals leave scope here.
        if (s_gcNodes.Count != 4 || s_collectorStores != 4 || s_control.ConstructorCount != 5 ||
            Bits(GcStaticFixture.Get()) != NodeReceipt(0).ObjectReference ||
            ReadStaticCollectorTag(false) != CollectorCookie + 1 ||
            ReadStaticCollectorTag(true) != CollectorCookie + 2) RejectGc(103);
        CollectorLine(1, NodeReceipt(0).ObjectReference, NodeReceipt(1).ObjectReference,
            NodeReceipt(2).ObjectReference, NodeReceipt(3).ObjectReference);
        CheckCollectorStack(5);

        s_collectorPhase = 2;
        // Preserve the earlier scalar fixture through an explicit original receipt.
        RhpNewFastResult prior = s_gcValue;
        BoundedCollectionResult first = CollectGcGraph(&prior, 1);
        if (!first.Succeeded || first.LiveObjects != 4 || first.ReclaimedObjects != 2 ||
            first.ReclaimedBytes != 64 || !CollectorReceiptValid(0) || !CollectorReceiptValid(1) ||
            CollectorReceiptValid(2) || CollectorReceiptValid(3) ||
            ReadStaticCollectorTag(false) != CollectorCookie + 1 ||
            ReadStaticCollectorTag(true) != CollectorCookie + 2 || !ReadGcRoot().Succeeded)
            RejectGc(104);
        EmitCollection(2, first);
        CheckCollectorStack(6);

        s_collectorPhase = 3;
        ReplaceCollectorRoot(CollectorCookie + 5);
        RhpNewFastResult reused = NodeReceipt(4), dead = NodeReceipt(2);
        if (s_gcNodes.Count != 5 || s_collectorStores != 5 ||
            reused.ObjectReference != dead.ObjectReference || reused.MethodTableToken != dead.MethodTableToken ||
            reused.AllocationCount <= dead.AllocationCount || CollectorReceiptValid(2) ||
            !CollectorReceiptValid(4) || ReadStaticCollectorTag(false) != CollectorCookie + 5)
            RejectGc(105);
        CollectorLine(3, dead.ObjectReference, reused.ObjectReference, dead.AllocationCount, reused.AllocationCount);

        s_collectorPhase = 4;
        BoundedCollectionResult second = CollectGcGraph(null, 0);
        if (!second.Succeeded || second.LiveObjects != 2 || second.ReclaimedObjects != 3 ||
            second.ReclaimedBytes != 88 || CollectorReceiptValid(0) || CollectorReceiptValid(1) ||
            !CollectorReceiptValid(4) || ReadStaticCollectorTag(false) != CollectorCookie + 5)
            RejectGc(106);
        EmitCollection(4, second);
        CheckCollectorStack(7);
        GcStaticFixture.Set(null);
        if (s_collectorStores != 6 || Bits(GcStaticFixture.Get()) != 0) RejectGc(107);
        BoundedCollectionResult third = CollectGcGraph(null, 0);
        if (!third.Succeeded || third.LiveObjects != 1 || third.ReclaimedObjects != 1 ||
            third.ReclaimedBytes != 32 || CollectorReceiptValid(4) || !ReadGcRoot().Succeeded)
            RejectGc(108);
        EmitCollection(5, third);
        CheckCollectorStack(8);

        s_collectorPhase = 5;
        ReplaceCollectorRoot(CollectorCookie + 6);
        RhpNewFastResult final = NodeReceipt(5);
        if (s_gcNodes.Count != 6 || s_collectorStores != 7 || final.UsedBefore != 24 ||
            final.AllocationCount != 8 || final.ObjectReference != s_gcValue.ObjectReference ||
            s_control.ConstructorCount != 7 || ReadStaticCollectorTag(false) != CollectorCookie + 6)
            RejectGc(109);
        s_collectorPhase = 6;
        BoundedCollectionResult repeat = CollectGcGraph(null, 0);
        if (!repeat.Succeeded || repeat.LiveObjects != 2 || repeat.ReclaimedObjects != 0 ||
            repeat.ReclaimedBytes != 0 || !CollectorReceiptValid(5) || !ReadGcRoot().Succeeded)
            RejectGc(110);
        EmitCollection(6, repeat);
        CheckCollectorStack(9);
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            if (collecting->State.UsedBytes != 176 || collecting->State.AllocationCount != 8 ||
                GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &collecting->State,
                    (ulong)sizeof(GcCollectingStorage), s_gcValue) == RhpNewFastStatus.Success)
                RejectGc(111);
            CollectorLine(7, final.ObjectReference, collecting->State.UsedBytes,
                collecting->State.AllocationCount, s_collectorStores);
        }
        CollectorLine(8, s_gcStack.Checks, s_gcStack.Final0, s_gcStack.Final1, s_gcStack.MinimumWitness);
        s_collectorPhase = 7; s_gcPhase = 5;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BuildCollectorGraph()
    {
        GcCollectorNode parent = NewCollectorNode(CollectorCookie + 1);
        GcCollectorNode child = NewCollectorNode(CollectorCookie + 2);
        GcCollectorNode deadA = NewCollectorNode(CollectorCookie + 3);
        GcCollectorNode deadB = NewCollectorNode(CollectorCookie + 4);
        if (CollectorField(parent) != Bits(parent) + s_collectorFieldOffset ||
            CollectorField(child) != Bits(child) + s_collectorFieldOffset ||
            CollectorField(deadA) != Bits(deadA) + s_collectorFieldOffset ||
            CollectorField(deadB) != Bits(deadB) + s_collectorFieldOffset) RejectGc(112);
        parent.SetChild(child);
        deadA.SetChild(deadB);
        deadB.SetChild(deadA);
        GcStaticFixture.Set(parent);
        if (Bits(parent.GetChild()) != Bits(child) || Bits(deadA.GetChild()) != Bits(deadB) ||
            Bits(deadB.GetChild()) != Bits(deadA) || child.GetTag() != CollectorCookie + 2)
            RejectGc(113);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GcCollectorNode NewCollectorNode(ulong tag) => new GcCollectorNode(tag);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReplaceCollectorRoot(ulong tag)
    {
        GcCollectorNode replacement = NewCollectorNode(tag);
        if (Bits(replacement.GetChild()) != 0 || replacement.GetTag() != tag ||
            CollectorField(replacement) != Bits(replacement) + s_collectorFieldOffset) RejectGc(114);
        GcStaticFixture.Set(replacement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ReadStaticCollectorTag(bool child)
    {
        object value = GcStaticFixture.Get();
        GcCollectorNode node = Unsafe.As<object, GcCollectorNode>(ref value);
        return child ? node.GetChild().GetTag() : node.GetTag();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong CollectorNodeType() => (ulong)Internal.Runtime.MethodTable.Of<GcCollectorNode>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong CollectorField(GcCollectorNode node)
    {
        fixed (byte* field = &Unsafe.As<GcCollectorNode, byte>(ref node.ChildAddress())) return (ulong)field;
    }

    private static RhpNewFastResult NodeReceipt(uint index)
    {
        if (index >= 6 || index >= s_gcNodes.Count) RejectGc(115);
        fixed (GcNodeReceipts* records = &s_gcNodes) return (&records->Node0)[index];
    }

    private static bool CollectorReceiptValid(uint index)
    {
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
            return GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &collecting->State,
                (ulong)sizeof(GcCollectingStorage), NodeReceipt(index)) == RhpNewFastStatus.Success;
    }

    private static ulong AllocateCollectorNode(ulong token)
    {
        ulong expectedCount = s_collectorPhase == 1 ? s_gcNodes.Count < 4 ? s_gcNodes.Count : 99UL
            : s_collectorPhase == 3 ? 4UL : s_collectorPhase == 5 ? 5UL : 99UL;
        if (expectedCount >= 6 || expectedCount != s_gcNodes.Count ||
            token != CollectorNodeType() || !ReadGcRoot().Succeeded)
            RejectGc(116);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        fixed (GcNodeReceipts* records = &s_gcNodes)
        {
            CollectorTypeMapResult map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(
                NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian, s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, token);
            if (!map.Succeeded || map.Prefix.BaseSize != 32 || map.SeriesCount != 1) RejectGc(117);
            RhpNewFastResult result = GuestTestRhpNewFast.TryAllocate(s_access, &collecting->State,
                (ulong)sizeof(GcCollectingStorage), NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian,
                map.Prefix, token);
            if (!result.AllocationSucceeded || result.AllocationCount != expectedCount + 3 ||
                result.BytesConsumed != 32 || GcReadWord(result.MappedAllocationStart) != 0 ||
                GcReadWord(result.ObjectReference) != token || GcReadWord(result.ObjectReference + 8) != 0 ||
                GcReadWord(result.ObjectReference + 16) != 0) RejectGc(118);
            (&records->Node0)[expectedCount] = result;
            records->Count++;
            return result.ObjectReference;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ApplyCollectorReference(ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        if ((operation != 1 && operation != 2) || bytes != 8 || s_collectorStores >= 7 ||
            !ReadGcRoot().Succeeded) return 0;
        ulong expectedDestination = s_gcField, expectedOperand = 0;
        uint destinationNode = 99, operandNode = 99;
        if (s_collectorPhase == 1 && s_gcNodes.Count == 4)
        {
            if (s_collectorStores == 0) { destinationNode = 0; operandNode = 1; }
            else if (s_collectorStores == 1) { destinationNode = 2; operandNode = 3; }
            else if (s_collectorStores == 2) { destinationNode = 3; operandNode = 2; }
            else if (s_collectorStores == 3) operandNode = 0;
            else return 0;
        }
        else if (s_collectorPhase == 3 && s_collectorStores == 4 && s_gcNodes.Count == 5) operandNode = 4;
        else if (s_collectorPhase == 4 && s_collectorStores == 5) { }
        else if (s_collectorPhase == 5 && s_collectorStores == 6 && s_gcNodes.Count == 6) operandNode = 5;
        else return 0;
        if (destinationNode != 99)
        {
            if (!CollectorReceiptValid(destinationNode)) return 0;
            expectedDestination = NodeReceipt(destinationNode).ObjectReference + s_collectorFieldOffset;
        }
        else if (GcFieldAddress() != s_gcField) return 0;
        if (operandNode != 99)
        {
            if (!CollectorReceiptValid(operandNode)) return 0;
            expectedOperand = NodeReceipt(operandNode).ObjectReference;
        }
        if (destination != expectedDestination || operand != expectedOperand) return 0;
        // Mutator stores and collection never overlap; this existing checked
        // scalar store is not a concurrent/generational barrier or a card table.
        CheckedReferenceBulkResult result = CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryStore(
            s_access, CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage,
            destination - (ulong)s_access.Backing, operand);
        if (!result.Succeeded || result.BytesWritten != 8) return 0;
        s_collectorStores++;
        return 1;
    }

    private static BoundedCollectionResult CollectGcGraph(RhpNewFastResult* roots, uint count)
    {
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcRootStorage* root = &s_gcRoot)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
            return BoundedNonMovingCollectorExplicitRootEnumeration.Collect(
                NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian,
                new CollectorModuleAccess { Capture = s_gcCapture, Mapping = s_gcMapping, Owner = (byte*)type,
                    Capacity = (ulong)sizeof(GcTypeStorage), Handle = s_gcHandle, Metadata = s_gcImage },
                s_access, &collecting->State, (ulong)sizeof(GcCollectingStorage), roots, count, count,
                new CollectorStaticRootAccess { Owner = (byte*)root, Capacity = (ulong)sizeof(GcRootStorage),
                    Cells = s_gcImage, SystemObjectMethodTable = GcObjectType() });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckCollectorStack(ulong expected)
    {
        ulong witness = 0, address = (ulong)&witness;
        ulong authority = 0, start = 0, end = 0, initialRsp = 0;
        if (s_gcPhase != 4 || expected < 5 || expected > 9 || s_gcStack.Checks != expected ||
            !GcStackContext(address, out authority, out start, out end, out initialRsp) ||
            authority != s_gcStack.Authority || start != s_gcStack.Start || end != s_gcStack.End ||
            initialRsp != s_gcStack.InitialRsp || address > s_gcStack.Witness ||
            GcReadWord(start) != GcStackWord0 || GcReadWord(start + 8) != GcStackWord1) RejectGc(119);
        s_gcStack.Checks++;
        if (address < s_gcStack.MinimumWitness) s_gcStack.MinimumWitness = address;
    }

    private static void EmitCollection(ulong stage, BoundedCollectionResult result) =>
        CollectorLine(stage, (ulong)result.Status, result.LiveObjects, result.ReclaimedObjects, result.ReclaimedBytes);

    private static void CollectorLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Write((byte)'M'); Write((byte)'0'); Write((byte)'4'); Write((byte)'_');
        Write((byte)'C'); Write((byte)'O'); Write((byte)'L'); Write((byte)'L'); Write((byte)'E');
        Write((byte)'C'); Write((byte)'T'); Write((byte)' ');
        Decimal(stage); Write(32); Hex(a); Write(32); Hex(b); Write(32); Hex(c); Write(32); Hex(d); End();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong GcObjectType() => (ulong)Internal.Runtime.MethodTable.Of<object>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong GcFieldAddress()
    {
        fixed (byte* field = &Unsafe.As<object, byte>(ref GcStaticFixture.Address())) return (ulong)field;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong GcReadWord(ulong address) => *(ulong*)address;

    private static void RejectGc(ulong reason)
    {
        GcLine(255, reason, s_gcPhase, s_gcCalls, 0);
        while (true) System.RuntimeFailFast.Halt();
    }

    private static void GcLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Write((byte)'M'); Write((byte)'0'); Write((byte)'4'); Write((byte)'_');
        Write((byte)'G'); Write((byte)'C'); Write((byte)'_'); Write((byte)'R');
        Write((byte)'O'); Write((byte)'O'); Write((byte)'T'); Write((byte)' ');
        Decimal(stage); Write(32); Hex(a); Write(32); Hex(b); Write(32); Hex(c); Write(32); Hex(d); End();
    }
}
#endif
