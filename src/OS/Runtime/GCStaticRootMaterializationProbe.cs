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
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcRootStorage* root = &s_gcRoot)
        {
            if (RhpNewFastAllocationCore.Prepare(original, a, b, c, r,
                    (byte*)image, capture.Range.ImageSize) != RhpAdmissionStatus.Success ||
                !RhpNewFastAllocationCore.Bind(a, b, c, r).Succeeded)
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
                s_gcImage, s_gcImage, GcObjectType(), *a, b, GuestTestRhpNewFast.RequiredStateBytes,
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
                s_gcImage, s_gcImage, GcObjectType(), *a, b, GuestTestRhpNewFast.RequiredStateBytes,
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
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            // Ready authenticates every original pointer and exact capacity
            // before the disjointness checks read the retained access fields.
            return RhpNewFastAllocationCore.Ready(a, b, c, r) == RhpAdmissionStatus.Success &&
                GcStackSeparate(start, end, (ulong)a, (ulong)sizeof(RhpNewFastHeapAccess)) &&
                GcStackSeparate(start, end, (ulong)b, (ulong)sizeof(RhpNewFastBumpState)) &&
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
    // share s_bump; the existing six-role reference diagnostic remains separate.
    private static ulong AllocateGcValue(ulong token)
    {
        if (s_gcPhase != 2 || token != TypePlain() || s_gcValue.ObjectReference != 0 ||
            !ReadGcRoot().Succeeded) RejectGc(12);
        if (!RhpNewFastAllocationCore.TryReadPrefix((byte*)s_gcImage.Start, s_gcImage.Bytes,
                token, TypePlain(), out NativeAotMethodTablePrefix prefix, out _, out _, out _))
            RejectGc(13);
        fixed (RhpNewFastBumpState* bump = &s_bump)
        {
            RhpNewFastResult result = GuestTestRhpNewFast.TryAllocate(s_access, bump,
                GuestTestRhpNewFast.RequiredStateBytes, NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian,
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
        // Admit only the two actual scalar stores. No bulk, stack, array or
        // foreign-reference route falls through to an unchecked memory write.
        if (s_gcPhase != 3 || s_gcCalls > 1 || (operation != 1 && operation != 2) || bytes != 8 ||
            destination != s_gcField || destination != GcFieldAddress() ||
            operand != (s_gcCalls == 0 ? s_gcValue.ObjectReference : 0)) return 0;
        GCStaticRootResult root = ReadGcRoot();
        if (!root.Succeeded || root.SeriesStart != destination || root.SeriesBytes != 8 ||
            s_gcValue.ObjectReference == 0 || s_gcValue.MethodTableToken != TypePlain()) return 0;
        fixed (RhpNewFastBumpState* bump = &s_bump)
        {
            if (GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, bump,
                    GuestTestRhpNewFast.RequiredStateBytes, s_gcValue) != RhpNewFastStatus.Success ||
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
        fixed (RhpNewFastBumpState* bump = &s_bump)
            return GCStaticRootsMalformedMetadata.TryGetRetainedRoot(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, s_gcImage, GcObjectType(),
                s_access, bump, GuestTestRhpNewFast.RequiredStateBytes, (byte*)root, (ulong)sizeof(GcRootStorage));
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
