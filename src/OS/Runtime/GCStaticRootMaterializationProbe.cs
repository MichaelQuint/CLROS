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

            s_gcPhase = 2;
            RhpPlainFixture value = NewPlain(GcCookie);
            if (s_gcPhase != 3 || Bits(value) != s_gcValue.ObjectReference ||
                ReadPlain(value) != GcCookie || c->ConstructorCount != 1 || b->AllocationCount != 2)
                RejectGc(9);
            GcLine(4, s_gcValue.ObjectReference, s_gcValue.MethodTableToken,
                s_gcValue.UsedBefore, s_gcValue.UsedAfter);
            GcStaticFixture.Set(value);
            if (s_gcCalls != 1 || Bits(GcStaticFixture.Get()) != Bits(value) ||
                GcReadWord(s_gcField) != Bits(value) || !ReadGcRoot().Succeeded) RejectGc(10);
            GcLine(5, s_gcOperation, s_gcCalls, GcReadWord(s_gcField), ReadPlain(value));
            GcStaticFixture.Set(null);
            if (s_gcCalls != 2 || Bits(GcStaticFixture.Get()) != 0 ||
                GcReadWord(s_gcField) != 0 || !ReadGcRoot().Succeeded ||
                ReadPlain(value) != GcCookie || b->AllocationCount != 2) RejectGc(11);
            s_gcPhase = 4;
            GcLine(6, s_gcOperation, s_gcCalls, b->AllocationCount, b->UsedBytes);
        }
    }

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
