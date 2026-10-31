using OS.Kernel.Runtime;

namespace OS.Runtime;

// Issue-specific POD core. Host callers supply readable buffers, never CLR object bits.
internal enum RhpAdmissionStatus : byte
{
    Uninitialized = 0, Success = 1, InvalidStorage = 2, StorageOverlap = 3,
    AlreadyPrepared = 4, NotPrepared = 5, Reentry = 6, UnknownToken = 7,
    InvalidImage = 8, InvalidPrefixExtent = 9, PrefixChanged = 10,
    AllocationRejected = 11, ObservationRejected = 12, PriorObjectChanged = 13,
    RetainedRejected = 14, InvalidFixture = 15
}

internal struct RhpAllocationControl
{
    internal ulong CanaryBefore, Self, Access, Bump, Records, Image, ImageBytes;
    internal ulong Prepared, InProgress, Completed;
    // Whole-POD initialization leaves this zero; only the enabled EFI profile increments it.
#pragma warning disable CS0649
    internal ulong ConstructorCount;
#pragma warning restore CS0649
    internal ulong CanaryAfter;
}

internal struct RhpObjectObservation
{
    internal ulong Id, Route, MethodTable, Prefix0, Prefix1, Prefix2, Flags, BaseSize;
    internal ulong Allocation, Reference, End, UsedBefore, UsedAfter, Count;
    internal ulong Sync, ObservedMt, ZeroBytes, ZeroOr;
    internal ulong ScalarOffset, Scalar, UntouchedBytes, UntouchedOr;
    // Array-only observations stay in caller-owned POD storage, never another heap cursor.
    internal ulong Length, ElementTable, ElementPrefix0, ElementPrefix1, ElementPrefix2, ScalarWidth;
    // String's related Object is not an array element. All 24 bytes are retained separately.
    internal ulong RelatedObjectTable, RelatedObjectPrefix0, RelatedObjectPrefix1, RelatedObjectPrefix2;
}

internal unsafe struct RhpObservationStorage
{
    internal ulong CanaryBefore;
    internal RhpObjectObservation Object0;
    // Allocate writes these sequential POD slots through (&records->Object0)[id - 1].
#pragma warning disable CS0649
    internal RhpObjectObservation Object1, Object2, Object3, Object4;
    internal RhpObjectObservation Object5, Object6, Object7, Object8, Object9;
#pragma warning restore CS0649
    internal fixed byte BeforeAttempt[4096];
    internal ulong CanaryAfter;
}

internal readonly struct RhpAllocationAttempt
{
    internal RhpAllocationAttempt(RhpAdmissionStatus status, RhpNewFastResult leaf = default,
        RhpObjectObservation observation = default)
    { Status = status; Leaf = leaf; Observation = observation; }
    internal RhpAdmissionStatus Status { get; }
    internal RhpNewFastResult Leaf { get; }
    internal RhpObjectObservation Observation { get; }
    internal bool Succeeded => Status == RhpAdmissionStatus.Success && Leaf.AllocationSucceeded;
}

internal static unsafe partial class RhpNewFastAllocationCore
{
    internal const ulong Canary = 0x71C4B059A386D2EF;
    internal static ulong ControlBytes => (ulong)sizeof(RhpAllocationControl);
    internal static ulong ObservationBytes => (ulong)sizeof(RhpObservationStorage);

    // Supplied image is an original UEFI-loaded, caller-attested identity mapping.
    // Numeric bounds and PE flags alone do not establish active page permissions.
    internal static RhpAdmissionStatus Prepare(RhpNewFastHeapAccess original,
        RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records,
        byte* image, ulong imageBytes)
    {
        RhpAdmissionStatus storage = Storage(original, access, bump, control, records);
        if (storage != RhpAdmissionStatus.Success) return storage;
        if (!Zero((byte*)access, 104) || !Zero((byte*)bump, 80) ||
            !Zero((byte*)control, ControlBytes) || !Zero((byte*)records, ObservationBytes))
            return RhpAdmissionStatus.AlreadyPrepared;
        if (!Image(image, imageBytes, out _, out _, out _)) return RhpAdmissionStatus.InvalidImage;
        ManagedHeapPageRangeResult retained = Retained(original);
        if (!retained.IsReady || retained.ReadBytes != 4096 || !retained.ContentsAreZeroNow ||
            retained.MappedStart != (ulong)original.Backing)
            return RhpAdmissionStatus.RetainedRejected;
        // All supplied extents and original authority were validated before first write.
        *access = original;
        *control = new RhpAllocationControl
        {
            CanaryBefore = Canary, CanaryAfter = Canary,
            Self = (ulong)control, Access = (ulong)access, Bump = (ulong)bump,
            Records = (ulong)records, Image = (ulong)image, ImageBytes = imageBytes, Prepared = 1
        };
        records->CanaryBefore = Canary; records->CanaryAfter = Canary;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpNewFastResult Bind(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success) return default;
        return GuestTestRhpNewFast.InitializeOnce(*access, bump, 80);
    }

    internal static RhpAllocationAttempt Allocate(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong token, ulong expectedToken, ulong id, ulong route)
        => AllocateCore(access, bump, control, records, token, expectedToken, id, route, false, 0, 0);

    internal static RhpAllocationAttempt AllocateMixed(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong token, ulong expectedToken, ulong id, ulong route, long length, ulong expectedElement)
        => AllocateCore(access, bump, control, records, token, expectedToken, id, route, true, length, expectedElement);

    internal static RhpAllocationAttempt AllocateStrings(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong token, ulong expectedToken, ulong id, ulong route, long length, ulong related)
        => AllocateCore(access, bump, control, records, token, expectedToken, id, route, false, length, related, true);

    private static RhpAllocationAttempt AllocateCore(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong token, ulong expectedToken, ulong id, ulong route, bool mixed, long length, ulong expectedElement,
        bool strings = false, bool references = false)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return new(ready);
        if (control->InProgress != 0) return new(RhpAdmissionStatus.Reentry);
        if (token == 0 || token != expectedToken) return new(RhpAdmissionStatus.UnknownToken);
        bool text = strings && route == 5;
        bool array = strings ? route == 6 : mixed && (route == 3 || route == 4);
        if (array && length != (strings ? 1 : id == 3 ? control->Completed == 5 ? 1 : 3 : id == 5 ? 1 : 0))
            return new(RhpAdmissionStatus.InvalidFixture);
        bool filler = strings ? control->Completed == 7 && id == 3
            : mixed ? control->Completed == 5 && id <= 3
            : control->Completed == 10 && id == 1 && route == 1;
        if (references)
        {
            if (mixed || strings || length != 0 || expectedElement != 0 || route != 7 ||
                id == 0 || id > 6 || id != control->Completed + 1)
                return new(RhpAdmissionStatus.InvalidFixture);
            filler = false;
        }
        else if (strings)
        {
            bool unbound = id == 3 && control->Completed == 0 && !bump->IsInitialized && length == 0;
            bool invalidLength = id == 3 && control->Completed == 0 && bump->IsInitialized && length == 0x100000001L;
            if (id == 0 || id > 7 || route != (id == 1 ? 1UL : id == 2 ? 6UL : 5UL) ||
                (!filler && !unbound && !invalidLength && id != control->Completed + 1) ||
                (text && !unbound && !invalidLength && length != (filler
                    ? bump->AllocationCount < 168 ? 0 : bump->AllocationCount == 168 ? 3 : 1
                    : id == 3 ? 0 : id == 4 ? 1 : id == 7 ? 2 : 3)) ||
                (filler && (bump->AllocationCount < 7 || bump->AllocationCount > 169)))
                return new(RhpAdmissionStatus.InvalidFixture);
        }
        else if (id == 0 || id > (mixed ? 5UL : 10UL) ||
            route != (mixed ? (id == 1 || id == 4 ? 1UL : id == 3 ? 4UL : 3UL) : id <= 4 ? 1UL : 2UL) ||
            (!filler && id != control->Completed + 1 && !(mixed && id == 2 && control->Completed == 0 && !bump->IsInitialized))) return new(RhpAdmissionStatus.InvalidFixture);
        if (!PrefixDisjoint(*access, access, bump, control, records, token) ||
            !TryReadPrefix((byte*)control->Image, control->ImageBytes, token, expectedToken,
                out NativeAotMethodTablePrefix prefix, out ulong word0, out ulong word1, out ulong word2))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        ulong element0 = 0, element1 = 0, element2 = 0;
        if (array && (prefix.Flags != (route == 4 ? 0xE0020008U : 0xE0020001U) ||
            prefix.BaseSize != 24 || prefix.RelatedTypeToken != expectedElement ||
            !PrefixDisjoint(*access, access, bump, control, records, expectedElement) ||
            !TryReadPrefix((byte*)control->Image, control->ImageBytes, prefix.RelatedTypeToken, expectedElement,
                out NativeAotMethodTablePrefix element, out element0, out element1, out element2) ||
            ((element.Flags >> 26) & 31) != (route == 4 ? 10U : 5U) || element.BaseSize != 24))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        if (text && (prefix.Flags != 0xD0000002U || prefix.BaseSize != 22 ||
            prefix.RelatedTypeToken != expectedElement || expectedElement == token ||
            !PrefixDisjoint(*access, access, bump, control, records, expectedElement) ||
            !TryReadPrefix((byte*)control->Image, control->ImageBytes, prefix.RelatedTypeToken, expectedElement,
                out NativeAotMethodTablePrefix relatedObject, out element0, out element1, out element2) ||
            relatedObject.BaseSize != 24 || relatedObject.RelatedTypeToken != 0))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        if (!Prior(records, control->Completed, (ulong)access->Backing, references)) return new(RhpAdmissionStatus.PriorObjectChanged);
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        byte* bumpBefore = stackalloc byte[80];
        for (ulong i = 0; i < 80; i++) bumpBefore[i] = ((byte*)bump)[i];
        control->InProgress = 1;
        RhpNewFastResult result = text
            ? GuestTestStringAllocationCharacterStorage.TryAllocate(*access, bump, 80,
                NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token, length)
            : array ? GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(*access, bump, 80,
                NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token, length)
            : GuestTestRhpNewFast.TryAllocate(*access, bump, 80,
                NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token);
        if (!result.AllocationSucceeded)
        {
            control->InProgress = 0;
            if (!RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt))
                return new(RhpAdmissionStatus.ObservationRejected, result);
            return new(RhpAdmissionStatus.AllocationRejected, result);
        }
        // No reference conversion or constructor can precede this actual byte readback.
        RhpObjectObservation observation = Observe(result, id, route, word0, word1, word2);
        if (array)
        {
            observation.Length = Read32((byte*)observation.Allocation + 16);
            observation.ElementTable = expectedElement; observation.ElementPrefix0 = element0;
            observation.ElementPrefix1 = element1; observation.ElementPrefix2 = element2;
        }
        if (text)
        {
            observation.Length = Read32((byte*)observation.Allocation + 16);
            observation.RelatedObjectTable = expectedElement; observation.RelatedObjectPrefix0 = element0;
            observation.RelatedObjectPrefix1 = element1; observation.RelatedObjectPrefix2 = element2;
        }
        if (Read64((byte*)token) != word0 || Read64((byte*)token + 8) != word1 ||
            Read64((byte*)token + 16) != word2 ||
            ((array || text) && (Read64((byte*)expectedElement) != element0 || Read64((byte*)expectedElement + 8) != element1 ||
                Read64((byte*)expectedElement + 16) != element2)))
            return new(RhpAdmissionStatus.PrefixChanged, result, observation);
        ulong expectedBytes = text ? (22 + (ulong)length * 2 + 7) & ~7UL
            : array ? (24 + (ulong)(uint)length * (route == 4 ? 8UL : 1UL) + 7) & ~7UL : prefix.BaseSize;
        if (observation.Sync != 0 || observation.ObservedMt != token || observation.ZeroOr != 0 ||
            observation.Reference != observation.Allocation + 8 ||
            observation.End != observation.Allocation + expectedBytes ||
            observation.UsedAfter != observation.UsedBefore + expectedBytes ||
            ((array || text) && observation.Length != (ulong)length) ||
            !Prior(records, control->Completed, (ulong)access->Backing, references))
            return new(RhpAdmissionStatus.ObservationRejected, result, observation);
        if (!filler) (&records->Object0)[id - 1] = observation;
        control->InProgress = 0;
        return new(RhpAdmissionStatus.Success, result, observation);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool RejectedAllocationUnchanged(RhpNewFastBumpState* bump, byte* bumpBefore,
        byte* backing, byte* backingBefore)
    {
        for (ulong i = 0; i < 80; i++) if (((byte*)bump)[i] != bumpBefore[i]) return false;
        for (ulong i = 0; i < 4096; i++) if (backing[i] != backingBefore[i]) return false;
        return true;
    }

    internal static RhpAdmissionStatus Complete(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong id, ulong reference, ulong scalarOffset, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return ready;
        if (control->InProgress != 0) return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 10) return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* record = &(&records->Object0)[id - 1];
        if (record->UsedBefore > 4096 || record->BaseSize < 24 || record->BaseSize > 4096 - record->UsedBefore ||
            record->Allocation != (ulong)access->Backing + record->UsedBefore ||
            record->Reference != record->Allocation + 8 || record->End != record->Allocation + record->BaseSize ||
            record->Id != id || record->Reference != reference || record->Reference == 0 ||
            (id <= 4 && (scalarOffset != (id == 1 ? 8UL : 16UL) || typedScalar != expectedScalar)) ||
            (id > 4 && (scalarOffset != 0 || typedScalar != 0 || expectedScalar != 0)))
            return RhpAdmissionStatus.ObservationRejected;
        if (id <= 4 && (scalarOffset > record->BaseSize - 16 ||
            Read64((byte*)reference + scalarOffset) != typedScalar)) return RhpAdmissionStatus.ObservationRejected;
        record->ScalarOffset = scalarOffset; record->Scalar = typedScalar;
        record->ScalarWidth = id <= 4 ? 8UL : 0;
        record->UntouchedBytes = record->BaseSize - 16 - (id <= 4 ? 8UL : 0UL);
        record->UntouchedOr = Untouched(*record);
        if (!Prior(records, id, (ulong)access->Backing)) return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CompleteMixed(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong id, ulong reference, int typedLength, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return ready;
        if (control->InProgress != 0) return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 5) return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        bool array = id == 2 || id == 3 || id == 5;
        ulong length = id == 3 ? 3UL : id == 5 ? 1UL : 0;
        ulong width = id == 2 ? 0UL : id == 5 ? 1UL : 8UL;
        ulong offset = id == 1 ? 8UL : id == 2 ? 0UL : id == 3 ? 24UL : 16UL;
        if (r->Id != id || r->Reference != reference || r->Reference == 0 ||
            r->Route != (array ? id == 3 ? 4UL : 3UL : 1UL) ||
            (array && (typedLength < 0 || (ulong)typedLength != length || r->Length != length)) ||
            (!array && typedLength != 0) || typedScalar != expectedScalar ||
            (width == 0 && typedScalar != 0) || r->BaseSize < 24 ||
            r->UsedBefore > 4096 || r->BaseSize > 4096 - r->UsedBefore ||
            r->Allocation != (ulong)access->Backing + r->UsedBefore ||
            r->Reference != r->Allocation + 8 || r->End != r->Allocation + r->BaseSize ||
            (width != 0 && (offset > r->BaseSize - 8 || width > r->BaseSize - 8 - offset ||
                ReadScalar((byte*)reference + offset, width) != typedScalar)))
            return RhpAdmissionStatus.ObservationRejected;
        r->ScalarOffset = offset; r->ScalarWidth = width; r->Scalar = typedScalar;
        r->UntouchedBytes = r->BaseSize - 16 - (array ? 4UL : 0UL) - width;
        r->UntouchedOr = Untouched(*r);
        if (!Prior(records, id, (ulong)access->Backing)) return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CompleteStrings(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong id, ulong reference, int typedLength, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return ready;
        if (control->InProgress != 0) return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 7) return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        ulong length = id == 1 || id == 3 ? 0UL : id == 2 || id == 4 ? 1UL : id == 7 ? 2UL : 3UL;
        ulong width = id == 1 ? 8UL : id == 2 ? 1UL : length * 2;
        ulong offset = id == 1 ? 8UL : id == 2 ? 16UL : 12UL;
        if (r->Id != id || r->Reference != reference || reference == 0 ||
            r->Route != (id == 1 ? 1UL : id == 2 ? 6UL : 5UL) ||
            typedLength < 0 || (ulong)typedLength != length || r->Length != length ||
            typedScalar != expectedScalar || (width == 0 && typedScalar != 0) || r->BaseSize < 24 ||
            r->UsedBefore > 4096 || r->BaseSize > 4096 - r->UsedBefore ||
            r->Allocation != (ulong)access->Backing + r->UsedBefore || reference != r->Allocation + 8 ||
            r->End != r->Allocation + r->BaseSize || offset > r->BaseSize - 8 ||
            width > r->BaseSize - 8 - offset || ReadScalar((byte*)reference + offset, width) != typedScalar)
            return RhpAdmissionStatus.ObservationRejected;
        r->ScalarOffset = offset; r->ScalarWidth = width; r->Scalar = typedScalar;
        r->UntouchedBytes = r->BaseSize - 16 - (id == 1 ? 0UL : 4UL) - width;
        r->UntouchedOr = Untouched(*r);
        if (!Prior(records, id, (ulong)access->Backing)) return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CheckCompleted(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return ready;
        return control->InProgress == 0 && Prior(records, control->Completed, (ulong)access->Backing)
            ? RhpAdmissionStatus.Success : RhpAdmissionStatus.PriorObjectChanged;
    }

    internal static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) =>
        ManagedHeapPageRangeLiveAllocator.ValidateRetained(a.Allocator, a.AllocatorCapacity,
            a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount,
            a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);

    internal static RhpAdmissionStatus Ready(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records)
    {
        // These are supplied readable POD slots; validate their numeric extents before reads.
        if (!Range((ulong)access, 104, 8) || !Range((ulong)bump, 80, 8) ||
            !Range((ulong)control, ControlBytes, 8) || !Range((ulong)records, ObservationBytes, 8))
            return RhpAdmissionStatus.InvalidStorage;
        if (control->Prepared != 1) return RhpAdmissionStatus.NotPrepared;
        RhpAdmissionStatus storage = Storage(*access, access, bump, control, records);
        if (storage != RhpAdmissionStatus.Success) return storage;
        if (control->Self != (ulong)control || control->Access != (ulong)access ||
            control->Bump != (ulong)bump || control->Records != (ulong)records || control->Completed > 10 ||
            control->CanaryBefore != Canary || control->CanaryAfter != Canary ||
            records->CanaryBefore != Canary || records->CanaryAfter != Canary)
            return RhpAdmissionStatus.InvalidStorage;
        ManagedHeapPageRangeResult retained = Retained(*access);
        if (!retained.IsReady || retained.ReadBytes != 4096 || retained.MappedStart != (ulong)access->Backing)
            return RhpAdmissionStatus.RetainedRejected;
        return RhpAdmissionStatus.Success;
    }

    private static RhpAdmissionStatus Storage(RhpNewFastHeapAccess a, RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        if (sizeof(void*) != 8 || sizeof(RhpNewFastHeapAccess) != 104 || sizeof(RhpNewFastBumpState) != 80 ||
            sizeof(ManagedHeapPageRangeState) != 216 ||
            sizeof(RhpObservationStorage) != 4112 + 10 * sizeof(RhpObjectObservation) ||
            a.AllocatorCapacity != 1 || a.NodeCount != 1 || a.NodeCapacity != 1 ||
            a.ReservationCount != 5 || a.ReservationCapacity != 5 || a.ParentIndex >= 5 ||
            a.BackingCapacity != 8192 || a.HeapCapacity != 216)
            return RhpAdmissionStatus.InvalidStorage;
        ulong* starts = stackalloc ulong[9] { (ulong)a.Allocator, (ulong)a.Nodes, (ulong)a.Reservations,
            (ulong)a.Heap, (ulong)a.Backing, (ulong)access, (ulong)bump, (ulong)control, (ulong)records };
        ulong* lengths = stackalloc ulong[9] { 56, 48, 200, 216, 8192, 104, 80, ControlBytes, ObservationBytes };
        for (int i = 0; i < 9; i++)
            if (!Range(starts[i], lengths[i], i == 4 ? 4096UL : 8UL)) return RhpAdmissionStatus.InvalidStorage;
        for (int i = 0; i < 9; i++)
            for (int j = i + 1; j < 9; j++)
                if (Overlap(starts[i], lengths[i], starts[j], lengths[j])) return RhpAdmissionStatus.StorageOverlap;
        return RhpAdmissionStatus.Success;
    }

    private static bool PrefixDisjoint(RhpNewFastHeapAccess a, RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token)
    {
        if (!Range(token, 24, 8)) return false;
        return !Overlap(token, 24, (ulong)a.Allocator, 56) && !Overlap(token, 24, (ulong)a.Nodes, 48) &&
            !Overlap(token, 24, (ulong)a.Reservations, 200) && !Overlap(token, 24, (ulong)a.Heap, 216) &&
            !Overlap(token, 24, (ulong)a.Backing, 8192) && !Overlap(token, 24, (ulong)access, 104) &&
            !Overlap(token, 24, (ulong)bump, 80) && !Overlap(token, 24, (ulong)control, ControlBytes) &&
            !Overlap(token, 24, (ulong)records, ObservationBytes);
    }

    internal static bool TryReadPrefix(byte* image, ulong imageBytes, ulong token, ulong expectedToken,
        out NativeAotMethodTablePrefix prefix, out ulong word0, out ulong word1, out ulong word2)
    {
        prefix = default; word0 = 0; word1 = 0; word2 = 0;
        // Exact finite-token identity precedes every prefix read, including malformed cases.
        if (token == 0 || token != expectedToken || !Range(token, 24, 8) ||
            !Image(image, imageBytes, out ulong size, out ulong table, out uint sections) ||
            token < (ulong)image || token - (ulong)image > size || 24 > size - (token - (ulong)image)) return false;
        ulong rva = token - (ulong)image;
        uint matches = 0;
        for (uint i = 0; i < sections; i++)
        {
            byte* section = image + table + i * 40;
            ulong length = Read32(section + 8), start = Read32(section + 12), raw = Read32(section + 16);
            if (length == 0 || rva < start || rva - start > length || 24 > length - (rva - start)) continue;
            uint flags = Read32(section + 36);
            if ((flags & 0x40000000) == 0 || (flags & 0xA2000000) != 0 ||
                rva - start > raw || 24 > raw - (rva - start)) return false;
            matches++;
        }
        if (matches != 1) return false;
        byte* bytes = (byte*)token;
        word0 = Read64(bytes); word1 = Read64(bytes + 8); word2 = Read64(bytes + 16);
        prefix = new NativeAotMethodTablePrefix((uint)word0, (uint)(word0 >> 32), word1,
            (ushort)word2, (ushort)(word2 >> 16), (uint)(word2 >> 32));
        return true;
    }

    private static bool Image(byte* image, ulong capacity, out ulong size, out ulong table, out uint sections)
    {
        size = 0; table = 0; sections = 0;
        if (!Range((ulong)image, capacity, 1) || capacity < 64 || Read16(image) != 0x5A4D) return false;
        ulong nt = Read32(image + 60);
        if (nt < 64 || nt > capacity || 24 > capacity - nt || Read32(image + nt) != 0x00004550 ||
            Read16(image + nt + 4) != 0x8664) return false;
        sections = Read16(image + nt + 6);
        ulong optional = nt + 24, optionalBytes = Read16(image + nt + 20);
        if (sections == 0 || sections > 8 || optionalBytes < 112 || optional > capacity ||
            optionalBytes > capacity - optional || Read16(image + optional) != 0x20B) return false;
        size = Read32(image + optional + 56);
        ulong headers = Read32(image + optional + 60);
        table = optional + optionalBytes;
        ulong tableBytes = sections * 40UL;
        if (headers == 0 || headers > size || size > capacity || table > headers ||
            tableBytes > headers - table) return false;
        for (uint i = 0; i < sections; i++)
        {
            byte* section = image + table + i * 40;
            ulong length = Read32(section + 8), start = Read32(section + 12);
            if (length == 0) continue;
            if (start < headers || start > size || length > size - start) return false;
            for (uint j = 0; j < i; j++)
            {
                byte* previous = image + table + j * 40;
                ulong otherLength = Read32(previous + 8), otherStart = Read32(previous + 12);
                if (otherLength != 0 && Overlap(start, length, otherStart, otherLength)) return false;
            }
        }
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpObjectObservation Observe(RhpNewFastResult value, ulong id, ulong route,
        ulong word0, ulong word1, ulong word2)
    {
        RhpObjectObservation result = new()
        {
            Id = id, Route = route, MethodTable = value.MethodTableToken,
            Prefix0 = word0, Prefix1 = word1, Prefix2 = word2, Flags = (uint)word0,
            BaseSize = value.BytesConsumed, Allocation = value.MappedAllocationStart,
            Reference = value.ObjectReference, End = value.MappedAllocationEndExclusive,
            UsedBefore = value.UsedBefore, UsedAfter = value.UsedAfter, Count = value.AllocationCount
        };
        byte* bytes = (byte*)result.Allocation;
        result.Sync = Read64(bytes); result.ObservedMt = Read64(bytes + 8);
        for (ulong i = 0; i < result.BaseSize; i++)
            if (i < 8 || i >= (route == 3 || route == 4 || route == 5 || route == 6 ? 20UL : 16UL)) { result.ZeroOr |= bytes[i]; result.ZeroBytes++; }
        return result;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool Prior(RhpObservationStorage* records, ulong count, ulong backing, bool references = false)
    {
        if (references && count > 6) return false;
        ulong used = 0;
        for (ulong i = 0; i < count; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            bool array = r.Route == 3 || r.Route == 4;
            bool text = r.Route == 5, stringArray = r.Route == 6;
            if (r.Id != i + 1 || r.BaseSize < 24 || r.BaseSize > 4096 - used ||
                r.Allocation != backing + used || r.Reference != r.Allocation + 8 ||
                (!references && !array && !text && !stringArray && r.Id <= 4 && (r.ScalarWidth != 8 || r.ScalarOffset != (r.Id == 1 ? 8UL : 16UL))) ||
                (references && (r.Route != 7 || r.ScalarWidth != (r.Id <= 4 ? 8UL : 0UL) ||
                    (r.Id <= 4 ? r.ScalarOffset < 8 || (r.ScalarOffset & 7) != 0
                        : r.ScalarOffset != 0 || r.Scalar != 0))) ||
                (array && (Read32((byte*)r.Allocation + 16) != r.Length ||
                    r.Length != (r.Id == 3 ? 3UL : r.Id == 5 ? 1UL : 0UL) ||
                    r.ScalarWidth != (r.Id == 3 ? 8UL : r.Id == 5 ? 1UL : 0UL) ||
                    r.ScalarOffset != (r.Id == 3 ? 24UL : r.Id == 5 ? 16UL : 0UL))) ||
                (stringArray && (r.Id != 2 || r.Length != 1 || r.ScalarWidth != 1 || r.ScalarOffset != 16 ||
                    Read32((byte*)r.Allocation + 16) != 1 || r.BaseSize != 32)) ||
                (text && (r.Id < 3 || r.Id > 7 || r.Length != (r.Id == 3 ? 0UL : r.Id == 4 ? 1UL : r.Id == 7 ? 2UL : 3UL) ||
                    Read32((byte*)r.Allocation + 16) != r.Length || r.ScalarWidth != 2 * r.Length || r.ScalarOffset != 12 ||
                    r.BaseSize != ((22 + 2 * r.Length + 7) & ~7UL))) ||
                r.End != r.Allocation + r.BaseSize || Read64((byte*)r.Allocation) != 0 ||
                Read64((byte*)r.Reference) != r.MethodTable || Untouched(r) != 0 ||
                (r.ScalarWidth != 0 && (r.ScalarOffset > r.BaseSize - 8 || r.ScalarWidth > r.BaseSize - 8 - r.ScalarOffset ||
                    ReadScalar((byte*)r.Reference + r.ScalarOffset, r.ScalarWidth) != r.Scalar))) return false;
            used += r.BaseSize;
        }
        return true;
    }

    private static ulong Untouched(RhpObjectObservation r)
    {
        ulong combined = 0;
        byte* bytes = (byte*)r.Reference;
        for (ulong i = 8; i < r.BaseSize - 8; i++)
            if (!((r.Route == 3 || r.Route == 4 || r.Route == 5 || r.Route == 6) && i < 12) &&
                (r.ScalarWidth == 0 || i < r.ScalarOffset || i >= r.ScalarOffset + r.ScalarWidth)) combined |= bytes[i];
        return combined;
    }

    private static ulong ReadScalar(byte* bytes, ulong width)
    { ulong value = 0; for (ulong i = 0; i < width; i++) value |= (ulong)bytes[i] << (int)(8 * i); return value; }

    private static bool Zero(byte* bytes, ulong length)
    { for (ulong i = 0; i < length; i++) if (bytes[i] != 0) return false; return true; }
    internal static bool Range(ulong start, ulong length, ulong alignment) =>
        start != 0 && length != 0 && (start & (alignment - 1)) == 0 && start <= ~0UL - length;
    private static bool Overlap(ulong start, ulong length, ulong other, ulong otherLength) =>
        start < other + otherLength && other < start + length;
    internal static ulong Read64(byte* bytes)
    { ulong result = 0; for (int i = 0; i < 8; i++) result |= (ulong)bytes[i] << (8 * i); return result; }
    private static uint Read32(byte* bytes) => (uint)(bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24));
    private static ushort Read16(byte* bytes) => (ushort)(bytes[0] | (bytes[1] << 8));
}


#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE
// Only this private production route authenticates compiler tokens. The core's
// supplied-token seam is for caller-owned host buffers, not a runtime registry.
internal static unsafe partial class RhpNewFastAllocation
{
    private static RhpNewFastHeapAccess s_access;
    private static RhpNewFastBumpState s_bump;
    private static RhpAllocationControl s_control;
    private static RhpObservationStorage s_records;

    internal static void Run(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
#if M04_CHECKED_REFERENCE_BARRIER_PROBE
        RunReferences(original, image, imageBytes);
#else
        T(0); End();
#if M04_STRING_ALLOCATION_PROBE
        RunStrings(original, image, imageBytes);
#elif M04_SZ_ARRAY_ALLOCATION_PROBE
        RunMixed(original, image, imageBytes);
#else
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus prepared = RhpNewFastAllocationCore.Prepare(original, a, b, c, r, image, imageBytes);
            if (prepared != RhpAdmissionStatus.Success) RejectAdapter(prepared, 1);
            PreparationLine();
#if M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE
            Attempt(false);
            RhpPlainFixture unbound = NewPlain(0x1111111111111111);
            // A returning rejection is always a distinct failure, never a constructor-absence claim.
            Complete(1, Bits(unbound), ReadPlain(unbound), 0x1111111111111111);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 2);
#else
            RhpNewFastResult bound = RhpNewFastAllocationCore.Bind(a, b, c, r);
            if (!bound.Succeeded || bound.Operation != RhpNewFastOperation.Bind) RejectAdapter(RhpAdmissionStatus.AllocationRejected, 3);
            BindLine(bound);
            RhpPlainFixture plain = NewPlain(0x1111111111111111);
            Complete(1, Bits(plain), ReadPlain(plain), 0x1111111111111111);
            RhpReferenceFixture reference = NewReference(0x2222222222222222);
            Complete(2, Bits(reference), ReadReference(reference), 0x2222222222222222);
            RhpGenericFixture<object> genericReference = NewGenericReference(0x3333333333333333);
            Complete(3, Bits(genericReference), ReadGenericReference(genericReference), 0x3333333333333333);
            RhpGenericFixture<ulong> genericValue = NewGenericValue(0x4444444444444444);
            Complete(4, Bits(genericValue), ReadGenericValue(genericValue), 0x4444444444444444);
            Complete(5, Bits(AllocateInt32()), 0, 0);
            Complete(6, Bits(AllocateEnum()), 0, 0);
            Complete(7, Bits(AllocateValue()), 0, 0);
            Complete(8, Bits(AllocateReferenceValue()), 0, 0);
            Complete(9, Bits(AllocateGenericValue()), 0, 0);
            Complete(10, Bits(AllocateNullable()), 0, 0);
#if M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE
            ulong initialUsed = b->UsedBytes, fillers = 0, size = r->Object0.BaseSize;
            while (size <= 4096 - b->UsedBytes)
            {
                RhpPlainFixture filler = NewPlain(0x5555555555555555);
                ulong bits = Bits(filler);
                if (ReadPlain(filler) != 0x5555555555555555 || bits != (ulong)a->Backing + b->UsedBytes - size + 8 ||
                    !Fillers(initialUsed, b->UsedBytes, size, TypePlain()) ||
                    RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                    RejectAdapter(RhpAdmissionStatus.PriorObjectChanged, 4);
                fillers++;
            }
            T(1); Decimal(fillers); T(2); Decimal(initialUsed);
            T(3); Decimal(b->UsedBytes); T(4); Decimal(size); T(5); End();
            Attempt(true);
            RhpPlainFixture exhausted = NewPlain(0x5555555555555555);
            if (Bits(exhausted) != 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 5);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 6);
#else
            ManagedHeapPageRangeResult retained = RhpNewFastAllocationCore.Retained(*a);
            if (!retained.IsReady || retained.ReadBytes != 4096 || retained.ContentsAreZeroNow ||
                c->Completed != 10 || b->AllocationCount != 10 || c->ConstructorCount != 4 ||
                retained.InitialAllocatorState.FreePageCount != 2 || retained.InitialAllocatorState.AllocatedPageCount != 0 ||
                retained.CurrentAllocatorState.FreePageCount != 1 || retained.CurrentAllocatorState.AllocatedPageCount != 1 ||
                retained.CurrentAllocatorState.FreeByteCount != 4096 || retained.CurrentAllocatorState.AllocatedByteCount != 4096 ||
                RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                RejectAdapter(RhpAdmissionStatus.RetainedRejected, 7);
            RetainLine(retained);
            T(6); End();
            T(7); End();
#endif
#endif
        }
#endif
#endif
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static ulong Allocate(ulong token)
    {
#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
        return AllocateGcValue(token);
#elif M04_CHECKED_REFERENCE_BARRIER_PROBE
        return AllocateReferenceRole(token);
#elif M04_STRING_ALLOCATION_PROBE
        return AllocateStringRoute(token, TypePlain(), 1, 1, 0, 0);
#elif M04_SZ_ARRAY_ALLOCATION_PROBE
        ulong id = token == TypePlain() ? 1UL : token == TypeReference() ? 4UL : 0;
        if (id == 0) RejectAdapter(RhpAdmissionStatus.UnknownToken, 8);
        return AllocateMixed(token, id == 1 ? TypePlain() : TypeReference(), id, 1, 0, 0);
#else
        ulong id = 0;
        for (ulong candidate = 1; candidate <= 10; candidate++)
            if (token == Token(candidate)) { if (id != 0) RejectAdapter(RhpAdmissionStatus.UnknownToken, 8); id = candidate; }
        if (id == 0) RejectAdapter(RhpAdmissionStatus.UnknownToken, 9);
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            ulong used = b->UsedBytes, count = b->AllocationCount, constructors = c->ConstructorCount;
            RhpAllocationAttempt result = RhpNewFastAllocationCore.Allocate(a, b, c, r, token, Token(id), id, id <= 4 ? 1UL : 2UL);
            if (!result.Succeeded)
            {
                if (result.Status == RhpAdmissionStatus.AllocationRejected &&
                    (result.Leaf.Status == RhpNewFastStatus.NotInitialized || result.Leaf.Status == RhpNewFastStatus.Exhausted))
                    RejectLeaf(result.Leaf.Status, used, b->UsedBytes, count, b->AllocationCount, constructors);
                RejectAdapter(result.Status, 10);
            }
            if (result.Leaf.ObjectReference == 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 11);
            if (c->Completed < 10) ObjectLine(result.Observation);
            return result.Leaf.ObjectReference;
        }
#endif
    }

#if M04_STRING_ALLOCATION_PROBE
    private static void RunStrings(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus prepared = RhpNewFastAllocationCore.Prepare(original, a, b, c, r, image, imageBytes);
            if (prepared != RhpAdmissionStatus.Success) RejectAdapter(prepared, 1);
            PreparationLine();
#if M04_STRING_ALLOCATION_BEFORE_BIND_PROBE
            StringAttempt(0, 0);
            string unbound = NewString('A', 0);
            if (Bits(unbound) != 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 41);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 42);
#else
            RhpNewFastResult bound = RhpNewFastAllocationCore.Bind(a, b, c, r);
            if (!bound.Succeeded || bound.Operation != RhpNewFastOperation.Bind) RejectAdapter(RhpAdmissionStatus.AllocationRejected, 3);
            BindLine(bound);
#if M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE
            StringAttempt(2, 0x100000001UL);
            string invalid = RawString(unchecked((nint)0x100000001L));
            if (Bits(invalid) != 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 43);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 44);
#else
            RhpPlainFixture plain = NewPlain(0x1111111111111111);
            CompleteString(1, Bits(plain), 0, ReadPlain(plain), 0x1111111111111111);
            byte[] one = NewBytes(1);
            StoreByte(one, 0, 0x5A);
            CompleteString(2, Bits(one), ByteLength(one), ReadByte(one, 0), 0x5A);
            string empty = NewString('A', 0);
            CompleteString(3, Bits(empty), StringLength(empty), StringUnits(empty), 0);
            string ascii = NewString('A', 1);
            CompleteString(4, Bits(ascii), StringLength(ascii), StringUnits(ascii), 0x0041);
            string nonAscii = NewString((char)0x03A9, 3);
            CompleteString(5, Bits(nonAscii), StringLength(nonAscii), StringUnits(nonAscii), 0x03A903A903A9);
            string nul = NewString((char)0, 3);
            CompleteString(6, Bits(nul), StringLength(nul), StringUnits(nul), 0);
            string surrogate = NewString((char)0xD800, 2);
            CompleteString(7, Bits(surrogate), StringLength(surrogate), StringUnits(surrogate), 0xD800D800);
            if (b->UsedBytes != 200 || b->AllocationCount != 7 || c->Completed != 7 || c->ConstructorCount != 6)
                RejectAdapter(RhpAdmissionStatus.ObservationRejected, 45);
#if M04_STRING_ALLOCATION_EXHAUSTION_PROBE
            for (ulong i = 0; i < 161; i++)
            {
                string filler = NewString('X', 0);
                if (StringLength(filler) != 0 || Bits(filler) != (ulong)a->Backing + b->UsedBytes - 24 + 8 ||
                    b->UsedBytes != 200 + (i + 1) * 24 || b->AllocationCount != 8 + i ||
                    c->ConstructorCount != 7 + i || !StringFillers(i + 1, false) ||
                    RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                    RejectAdapter(RhpAdmissionStatus.PriorObjectChanged, 46);
            }
            string last = NewString('Z', 3);
            if (StringLength(last) != 3 || StringUnits(last) != 0x005A005A005A ||
                Bits(last) != (ulong)a->Backing + 4064 + 8 || !StringFillers(161, true) ||
                b->UsedBytes != 4096 || b->AllocationCount != 169 || c->ConstructorCount != 168 ||
                RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                RejectAdapter(RhpAdmissionStatus.PriorObjectChanged, 47);
            S(0); Decimal(161); S(1); Decimal(1); T(2); Decimal(200); T(3); Decimal(b->UsedBytes);
            T(20); Decimal(b->AllocationCount); S(2); Decimal(c->ConstructorCount); T(5); End();
            StringAttempt(1, 1);
            string exhausted = NewString('E', 1);
            if (Bits(exhausted) != 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 48);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 49);
#else
            ManagedHeapPageRangeResult retained = RhpNewFastAllocationCore.Retained(*a);
            if (!retained.IsReady || retained.ReadBytes != 4096 || retained.ContentsAreZeroNow ||
                retained.InitialAllocatorState.FreePageCount != 2 || retained.InitialAllocatorState.AllocatedPageCount != 0 ||
                retained.CurrentAllocatorState.FreePageCount != 1 || retained.CurrentAllocatorState.AllocatedPageCount != 1 ||
                retained.CurrentAllocatorState.FreeByteCount != 4096 || retained.CurrentAllocatorState.AllocatedByteCount != 4096 ||
                RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                RejectAdapter(RhpAdmissionStatus.RetainedRejected, 7);
            RetainLine(retained); T(6); End(); T(7); End();
#endif
#endif
#endif
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static ulong AllocateString(ulong token, long length)
    {
        ulong id = s_control.Completed == 0 || s_control.Completed == 7 ? 3UL : s_control.Completed + 1;
        return AllocateStringRoute(token, TypeString(), id, 5, length, TypeObject());
    }

    private static ulong AllocateStringRoute(ulong token, ulong expected, ulong id, ulong route, long length, ulong related)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            ulong used = b->UsedBytes, count = b->AllocationCount, constructors = c->ConstructorCount;
            RhpAllocationAttempt result = RhpNewFastAllocationCore.AllocateStrings(a, b, c, r, token, expected, id, route, length, related);
            if (!result.Succeeded)
            {
                if (c->ConstructorCount != constructors) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 50);
                if (result.Status == RhpAdmissionStatus.AllocationRejected &&
                    (result.Leaf.Status == RhpNewFastStatus.NotInitialized || result.Leaf.Status == RhpNewFastStatus.Exhausted ||
                     result.Leaf.Status == RhpNewFastStatus.InvalidLength))
                    RejectLeaf(result.Leaf.Status, used, b->UsedBytes, count, b->AllocationCount, constructors);
                RejectAdapter(result.Status, 10);
            }
            if (result.Leaf.ObjectReference == 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 11);
            if (c->Completed < 7) ObjectLine(result.Observation);
            return result.Leaf.ObjectReference;
        }
    }

    private static void CompleteString(ulong id, ulong reference, int length, ulong scalar, ulong expected)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus status = RhpNewFastAllocationCore.CompleteStrings(a, b, c, r, id, reference, length, scalar, expected);
            if (status != RhpAdmissionStatus.Success) RejectAdapter(status, 12);
            RhpObjectObservation record = (&r->Object0)[id - 1];
            T(8); Decimal(id); S(3); Decimal((ulong)length); T(9); Decimal(record.ScalarOffset);
            S(4); Decimal(record.ScalarWidth); T(10); Hex(record.Scalar);
            S(5); Hex(id >= 3 ? reference + 12 + 2 * (ulong)length : 0);
            S(6); Hex(id >= 3 ? (ulong)(*((byte*)reference + 12 + 2 * length) | (*((byte*)reference + 13 + 2 * length) << 8)) : 0);
            T(11); Decimal(record.UntouchedBytes); T(12); Hex(record.UntouchedOr);
            S(2); Decimal(c->ConstructorCount); T(13); Decimal(c->Completed); T(14); End();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool StringFillers(ulong emptyCount, bool last)
    {
        if (emptyCount > 161 || (last && emptyCount != 161)) return false;
        for (ulong i = 0; i < emptyCount; i++)
        {
            byte* p = s_access.Backing + 200 + i * 24;
            if (RhpNewFastAllocationCore.Read64(p) != 0 || RhpNewFastAllocationCore.Read64(p + 8) != TypeString() ||
                RhpNewFastAllocationCore.Read64(p + 16) != 0) return false;
        }
        if (last)
        {
            byte* p = s_access.Backing + 4064;
            if (RhpNewFastAllocationCore.Read64(p) != 0 || RhpNewFastAllocationCore.Read64(p + 8) != TypeString() ||
                RhpNewFastAllocationCore.Read64(p + 16) != 0x005A005A00000003 ||
                RhpNewFastAllocationCore.Read64(p + 24) != 0x005A) return false;
        }
        return true;
    }

    private static void StringAttempt(ulong mode, ulong length)
    {
        T(15); if (mode == 0) T(17); else if (mode == 1) T(16); else S(7);
        S(8); Decimal(3); T(51); Hex(TypeString()); S(3); Decimal(length); S(9); Hex(TypeObject());
        T(19); Decimal(s_bump.UsedBytes); T(20); Decimal(s_bump.AllocationCount); End();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeString() => (ulong)Internal.Runtime.MethodTable.Of<string>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeObject() => (ulong)Internal.Runtime.MethodTable.Of<object>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string NewString(char value, int length) => new string(value, length);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string RawString(nint length) => System.Runtime.RuntimeImports.RhNewString(Internal.Runtime.MethodTable.Of<string>(), length);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int StringLength(string value) => value.Length;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static char StringCharacter(string value, int index) => value[index];
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong StringUnits(string value)
    {
        int count = StringLength(value);
        if (count < 0 || count > 3) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 51);
        ulong packed = 0;
        for (int i = 0; i < count; i++) packed |= (ulong)StringCharacter(value, i) << (16 * i);
        return packed;
    }
#endif

#if M04_SZ_ARRAY_ALLOCATION_PROBE
    private static void RunMixed(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus prepared = RhpNewFastAllocationCore.Prepare(original, a, b, c, r, image, imageBytes);
            if (prepared != RhpAdmissionStatus.Success) RejectAdapter(prepared, 1);
            PreparationLine();
#if M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE
            ArrayAttempt(false);
            byte[] unbound = NewBytes(0);
            CompleteArray(2, Bits(unbound), ByteLength(unbound), 0, 0);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 2);
#else
            RhpNewFastResult bound = RhpNewFastAllocationCore.Bind(a, b, c, r);
            if (!bound.Succeeded || bound.Operation != RhpNewFastOperation.Bind) RejectAdapter(RhpAdmissionStatus.AllocationRejected, 3);
            BindLine(bound);
            RhpPlainFixture plain = NewPlain(0x1111111111111111);
            CompleteArray(1, Bits(plain), 0, ReadPlain(plain), 0x1111111111111111);
            byte[] empty = NewBytes(0);
            CompleteArray(2, Bits(empty), ByteLength(empty), 0, 0);
            long[] longs = NewLongs(3);
            StoreLong(longs, 1, 0x3333333333333333);
            CompleteArray(3, Bits(longs), LongLength(longs), (ulong)ReadLong(longs, 1), 0x3333333333333333);
            RhpReferenceFixture reference = NewReference(0x2222222222222222);
            CompleteArray(4, Bits(reference), 0, ReadReference(reference), 0x2222222222222222);
            byte[] one = NewBytes(1);
            StoreByte(one, 0, 0x5A);
            CompleteArray(5, Bits(one), ByteLength(one), ReadByte(one, 0), 0x5A);
            if (b->UsedBytes != 160 || b->AllocationCount != 5 || c->Completed != 5 || c->ConstructorCount != 2)
                RejectAdapter(RhpAdmissionStatus.ObservationRejected, 13);
#if M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE
            ulong pairs = 0;
            while (48 <= 4096 - b->UsedBytes)
            {
                RhpPlainFixture filler = NewPlain(0x5555555555555555);
                if (ReadPlain(filler) != 0x5555555555555555 || Bits(filler) != (ulong)a->Backing + b->UsedBytes - 24 + 8)
                    RejectAdapter(RhpAdmissionStatus.ObservationRejected, 14);
                byte[] emptyFiller = NewBytes(0);
                if (ByteLength(emptyFiller) != 0 || Bits(emptyFiller) != (ulong)a->Backing + b->UsedBytes - 24 + 8 ||
                    !MixedFillers(b->UsedBytes) || RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                    RejectAdapter(RhpAdmissionStatus.PriorObjectChanged, 15);
                pairs++;
            }
            if (pairs != 82 || b->UsedBytes != 4096 || b->AllocationCount != 169 || c->ConstructorCount != 84)
                RejectAdapter(RhpAdmissionStatus.ObservationRejected, 16);
            A(0); Decimal(pairs); T(2); Decimal(160); T(3); Decimal(b->UsedBytes);
            T(20); Decimal(b->AllocationCount); A(1); Decimal(c->ConstructorCount); T(5); End();
            ArrayAttempt(true);
            long[] exhausted = NewLongs(1);
            if (Bits(exhausted) != 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 17);
            RejectAdapter(RhpAdmissionStatus.ObservationRejected, 18);
#else
            ManagedHeapPageRangeResult retained = RhpNewFastAllocationCore.Retained(*a);
            if (!retained.IsReady || retained.ReadBytes != 4096 || retained.ContentsAreZeroNow ||
                retained.InitialAllocatorState.FreePageCount != 2 || retained.InitialAllocatorState.AllocatedPageCount != 0 ||
                retained.CurrentAllocatorState.FreePageCount != 1 || retained.CurrentAllocatorState.AllocatedPageCount != 1 ||
                retained.CurrentAllocatorState.FreeByteCount != 4096 || retained.CurrentAllocatorState.AllocatedByteCount != 4096 ||
                RhpNewFastAllocationCore.CheckCompleted(a, b, c, r) != RhpAdmissionStatus.Success)
                RejectAdapter(RhpAdmissionStatus.RetainedRejected, 7);
            RetainLine(retained); T(6); End(); T(7); End();
#endif
#endif
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static ulong AllocateArray(ulong token, long length, bool pointerSized)
    {
#if M04_STRING_ALLOCATION_PROBE
        if (pointerSized) RejectAdapter(RhpAdmissionStatus.InvalidFixture, 40);
        return AllocateStringRoute(token, TypeBytes(), 2, 6, length, TypeByte());
#else
        ulong id = pointerSized ? 3UL : s_control.Completed == 4 ? 5UL : 2UL;
        return AllocateMixed(token, pointerSized ? TypeLongs() : TypeBytes(), id,
            pointerSized ? 4UL : 3UL, length, pointerSized ? TypeLong() : TypeByte());
#endif
    }

    private static ulong AllocateMixed(ulong token, ulong expected, ulong id, ulong route, long length, ulong element)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            ulong used = b->UsedBytes, count = b->AllocationCount, constructors = c->ConstructorCount;
            // Before-bind is the genuine zero-length array route, with no prior completed slot.
            RhpAllocationAttempt result = RhpNewFastAllocationCore.AllocateMixed(a, b, c, r, token, expected, id, route, length, element);
            if (!result.Succeeded)
            {
                if (result.Status == RhpAdmissionStatus.AllocationRejected &&
                    (result.Leaf.Status == RhpNewFastStatus.NotInitialized || result.Leaf.Status == RhpNewFastStatus.Exhausted))
                    RejectLeaf(result.Leaf.Status, used, b->UsedBytes, count, b->AllocationCount, constructors);
                RejectAdapter(result.Status, 10);
            }
            if (result.Leaf.ObjectReference == 0) RejectAdapter(RhpAdmissionStatus.ObservationRejected, 11);
            if (c->Completed < 5) ObjectLine(result.Observation);
            return result.Leaf.ObjectReference;
        }
    }

    private static void CompleteArray(ulong id, ulong reference, int length, ulong scalar, ulong expected)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus status = RhpNewFastAllocationCore.CompleteMixed(a, b, c, r, id, reference, length, scalar, expected);
            if (status != RhpAdmissionStatus.Success) RejectAdapter(status, 12);
            RhpObjectObservation record = (&r->Object0)[id - 1];
            T(8); Decimal(id); A(2); Decimal((ulong)length); T(9); Decimal(record.ScalarOffset);
            A(3); Decimal(record.ScalarWidth); T(10); Hex(record.Scalar); T(11); Decimal(record.UntouchedBytes);
            T(12); Hex(record.UntouchedOr); T(13); Decimal(c->Completed); T(14); End();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool MixedFillers(ulong used)
    {
        if (used < 160 || used > 4096 || (used - 160) % 48 != 0) return false;
        for (ulong cursor = 160; cursor < used; cursor += 48)
        {
            byte* p = s_access.Backing + cursor;
            if (RhpNewFastAllocationCore.Read64(p) != 0 || RhpNewFastAllocationCore.Read64(p + 8) != TypePlain() ||
                RhpNewFastAllocationCore.Read64(p + 16) != 0x5555555555555555 ||
                RhpNewFastAllocationCore.Read64(p + 24) != 0 || RhpNewFastAllocationCore.Read64(p + 32) != TypeBytes() ||
                RhpNewFastAllocationCore.Read64(p + 40) != 0) return false;
        }
        return true;
    }

    private static void ArrayAttempt(bool exhaustion)
    {
        T(15); if (exhaustion) T(16); else T(17);
        A(4); Decimal(exhaustion ? 3UL : 2UL); T(51); Hex(exhaustion ? TypeLongs() : TypeBytes());
        A(2); Decimal(exhaustion ? 1UL : 0UL); A(5); Hex(exhaustion ? TypeLong() : TypeByte());
        T(19); Decimal(s_bump.UsedBytes); T(20); Decimal(s_bump.AllocationCount); End();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeBytes() => (ulong)Internal.Runtime.MethodTable.Of<byte[]>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeLongs() => (ulong)Internal.Runtime.MethodTable.Of<long[]>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeByte() => (ulong)Internal.Runtime.MethodTable.Of<byte>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeLong() => (ulong)Internal.Runtime.MethodTable.Of<long>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static byte[] NewBytes(int length) => new byte[length];
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long[] NewLongs(int length) => new long[length];
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int ByteLength(byte[] value) => value.Length;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int LongLength(long[] value) => value.Length;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void StoreByte(byte[] value, int index, byte scalar) => value[index] = scalar;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static byte ReadByte(byte[] value, int index) => value[index];
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void StoreLong(long[] value, int index, long scalar) => value[index] = scalar;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long ReadLong(long[] value, int index) => value[index];
#endif

    private static void Complete(ulong id, ulong reference, ulong scalar, ulong expected)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAdmissionStatus status = RhpNewFastAllocationCore.Complete(a, b, c, r, id, reference,
                id <= 4 ? (id == 1 ? 8UL : 16UL) : 0, scalar, expected);
            if (status != RhpAdmissionStatus.Success) RejectAdapter(status, 12);
            RhpObjectObservation record = (&r->Object0)[id - 1];
            T(8); Decimal(id); T(9); Decimal(record.ScalarOffset);
            T(10); Hex(record.Scalar); T(11); Decimal(record.UntouchedBytes);
            T(12); Hex(record.UntouchedOr); T(13); Decimal(c->Completed);
            T(14); End();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool Fillers(ulong first, ulong used, ulong size, ulong token)
    {
        if (size < 24 || used > 4096 || first > used || (used - first) % size != 0) return false;
        for (ulong cursor = first; cursor < used; cursor += size)
        {
            byte* bytes = s_access.Backing + cursor;
            if (RhpNewFastAllocationCore.Read64(bytes) != 0 || RhpNewFastAllocationCore.Read64(bytes + 8) != token ||
                RhpNewFastAllocationCore.Read64(bytes + 16) != 0x5555555555555555) return false;
            for (ulong i = 24; i < size; i++) if (bytes[i] != 0) return false;
        }
        return true;
    }

    internal static void ConstructorEntered() { s_control.ConstructorCount++; }
    private static void Attempt(bool exhaustion)
    {
        T(15); if (exhaustion) T(16); else T(17);
        T(18); Hex(TypePlain()); T(19); Decimal(s_bump.UsedBytes);
        T(20); Decimal(s_bump.AllocationCount); End();
    }

    private static void RejectLeaf(RhpNewFastStatus status, ulong usedBefore, ulong usedAfter,
        ulong countBefore, ulong countAfter, ulong constructors)
    {
        T(21); Reason(status); T(22); Decimal((ulong)status);
        T(2); Decimal(usedBefore); T(3); Decimal(usedAfter);
        T(23); Decimal(countBefore); T(24); Decimal(countAfter);
        T(25); Decimal(constructors); End();
        T(26); Reason(status); End();
        while (true) System.RuntimeFailFast.Halt();
    }

    private static void Reason(RhpNewFastStatus status)
    {
#if M04_STRING_ALLOCATION_PROBE
        if (status == RhpNewFastStatus.InvalidLength) { S(7); return; }
#endif
        if (status == RhpNewFastStatus.NotInitialized) T(27); else T(28);
    }
    internal static void RejectAdapter(RhpAdmissionStatus status, ulong stage)
    {
        T(29); Decimal((ulong)status); T(30); Decimal(stage); End();
        T(31); End();
        while (true) System.RuntimeFailFast.Halt();
    }

    private static void PreparationLine()
    {
        T(71); Hex(s_control.Image); T(33); Decimal(s_control.ImageBytes);
        T(43); Hex(s_control.Access); T(44); Decimal((ulong)sizeof(RhpNewFastHeapAccess));
        T(45); Hex(s_control.Bump); T(46); Decimal((ulong)sizeof(RhpNewFastBumpState));
        T(72); Hex(s_control.Self); T(73); Decimal((ulong)sizeof(RhpAllocationControl));
        T(74); Hex(s_control.Records); T(75); Decimal((ulong)sizeof(RhpObservationStorage)); End();
    }

    private static void BindLine(RhpNewFastResult result)
    {
        T(32); Hex(s_control.Image); T(33); Decimal(s_control.ImageBytes);
        T(34); Hex((ulong)s_access.Allocator); T(35); Hex((ulong)s_access.Nodes);
        T(36); Hex((ulong)s_access.Reservations); T(37); Decimal(s_access.ReservationCapacity * 40);
        T(38); Decimal(s_access.ParentIndex); T(39); Hex((ulong)s_access.Backing);
        T(40); Decimal(s_access.BackingCapacity); T(41); Hex((ulong)s_access.Heap);
        T(42); Decimal(s_access.HeapCapacity); T(43); Hex(s_control.Access); T(44); Decimal((ulong)sizeof(RhpNewFastHeapAccess));
        T(45); Hex(s_control.Bump); T(46); Decimal((ulong)sizeof(RhpNewFastBumpState));
        T(22); Decimal((ulong)result.Status); T(19); Decimal(s_bump.UsedBytes); T(20); Decimal(s_bump.AllocationCount); End();
    }

    private static void ObjectLine(RhpObjectObservation r)
    {
        T(47); Decimal(r.Id); T(48);
#if M04_STRING_ALLOCATION_PROBE
        if (r.Route == 1) S(10); else if (r.Route == 6) S(11); else S(12);
#elif M04_SZ_ARRAY_ALLOCATION_PROBE
        if (r.Route == 1) A(9); else if (r.Route == 3) A(10); else A(11);
#else
        if (r.Route == 1) T(49); else T(50);
#endif
        T(51); Hex(r.MethodTable); T(52); Hex(r.Prefix0); T(53); Hex(r.Prefix1); T(54); Hex(r.Prefix2);
        T(55); Hex(r.Flags); T(4); Decimal(r.BaseSize); T(56); Hex(r.Allocation);
        T(57); Hex(r.Reference); T(58); Hex(r.End); T(2); Decimal(r.UsedBefore);
        T(3); Decimal(r.UsedAfter); T(20); Decimal(r.Count); T(59); Hex(r.Sync);
        T(60); Hex(r.ObservedMt); T(61); Decimal(r.ZeroBytes); T(62); Hex(r.ZeroOr);
#if M04_STRING_ALLOCATION_PROBE
        S(3); Decimal(r.Length); S(13); Hex(r.ElementTable); S(14); Hex(r.ElementPrefix0);
        S(15); Hex(r.ElementPrefix1); S(16); Hex(r.ElementPrefix2);
        S(9); Hex(r.RelatedObjectTable); S(17); Hex(r.RelatedObjectPrefix0);
        S(18); Hex(r.RelatedObjectPrefix1); S(19); Hex(r.RelatedObjectPrefix2);
#elif M04_SZ_ARRAY_ALLOCATION_PROBE
        A(2); Decimal(r.Length); A(5); Hex(r.ElementTable); A(6); Hex(r.ElementPrefix0);
        A(7); Hex(r.ElementPrefix1); A(8); Hex(r.ElementPrefix2);
#endif
        End();
    }

    private static void RetainLine(ManagedHeapPageRangeResult result)
    {
        T(63); Hex((ulong)s_access.Allocator); T(35); Hex((ulong)s_access.Nodes);
        T(36); Hex((ulong)s_access.Reservations); T(38); Decimal(s_access.ParentIndex);
        T(39); Hex((ulong)s_access.Backing); T(41); Hex((ulong)s_access.Heap); T(45); Hex(s_control.Bump);
        T(64); Hex(result.PhysicalStart); T(65); Hex(result.PhysicalEndExclusive);
        T(66); Decimal(result.InitialAllocatorState.FreePageCount);
        T(67); Decimal(result.CurrentAllocatorState.FreePageCount); T(68); Decimal(result.CurrentAllocatorState.AllocatedPageCount);
        T(69); Decimal(result.ReadBytes); T(70); Decimal(s_bump.UsedBytes);
        T(20); Decimal(s_bump.AllocationCount); T(22); Decimal((ulong)result.Status); End();
    }

    private static void Write(byte value) => OS.Boot.EfiEntry.WriteRhpByte(value);
    private static void End() { Write(13); Write(10); }
    private static void Hex(ulong value)
    {
        Write(48); Write(120);
        for (int shift = 60; shift >= 0; shift -= 4) { ulong digit = (value >> shift) & 15; Write((byte)(digit < 10 ? 48 + digit : 55 + digit)); }
    }
    private static void Decimal(ulong value)
    {
        byte* digits = stackalloc byte[20]; int length = 0;
        do { digits[length++] = (byte)(48 + value % 10); value /= 10; } while (value != 0);
        while (length != 0) Write(digits[--length]);
    }

    private static ulong Token(ulong id) => id switch
    {
        1 => TypePlain(), 2 => TypeReference(), 3 => TypeGenericReference(), 4 => TypeGenericValue(),
        5 => TypeInt32(), 6 => TypeEnum(), 7 => TypeValue(), 8 => TypeReferenceValue(), 9 => TypeGenericStruct(), 10 => TypeNullable(), _ => 0
    };
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypePlain() => (ulong)Internal.Runtime.MethodTable.Of<RhpPlainFixture>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeReference() => (ulong)Internal.Runtime.MethodTable.Of<RhpReferenceFixture>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeGenericReference() => (ulong)Internal.Runtime.MethodTable.Of<RhpGenericFixture<object>>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeGenericValue() => (ulong)Internal.Runtime.MethodTable.Of<RhpGenericFixture<ulong>>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeInt32() => (ulong)Internal.Runtime.MethodTable.Of<int>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeEnum() => (ulong)Internal.Runtime.MethodTable.Of<RhpEnumFixture>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeValue() => (ulong)Internal.Runtime.MethodTable.Of<RhpFixedValueFixture>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeReferenceValue() => (ulong)Internal.Runtime.MethodTable.Of<RhpReferenceValueFixture>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeGenericStruct() => (ulong)Internal.Runtime.MethodTable.Of<RhpGenericValueFixture<object>>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeNullable() => (ulong)Internal.Runtime.MethodTable.Of<System.Nullable<int>>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpPlainFixture NewPlain(ulong value) => new RhpPlainFixture(value);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadPlain(RhpPlainFixture value) => value.Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpReferenceFixture NewReference(ulong value) => new RhpReferenceFixture(value);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadReference(RhpReferenceFixture value) => value.Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpGenericFixture<object> NewGenericReference(ulong value) => new RhpGenericFixture<object>(value);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadGenericReference(RhpGenericFixture<object> value) => value.Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpGenericFixture<ulong> NewGenericValue(ulong value) => new RhpGenericFixture<ulong>(value);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadGenericValue(RhpGenericFixture<ulong> value) => value.Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateInt32() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<int>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateEnum() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<RhpEnumFixture>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateValue() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<RhpFixedValueFixture>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateReferenceValue() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<RhpReferenceValueFixture>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateGenericValue() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<RhpGenericValueFixture<object>>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object AllocateNullable() => System.Runtime.RhpNewFastRuntime.RhpNewFast(Internal.Runtime.MethodTable.Of<System.Nullable<int>>());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong Bits(object value) => (ulong)System.Runtime.CompilerServices.Unsafe.As<object, nuint>(ref value);
    private static void T(int token)
    {
#if M04_CHECKED_REFERENCE_BARRIER_PROBE
        if (token == 71) { R(20); return; }
        if (token == 32) { R(21); return; }
        if (token == 63) { R(22); return; }
#endif
        for (int index = 0; ; index++)
        {
            byte value;
#if M04_STRING_ALLOCATION_PROBE
            value = StringOverride(token, index);
            if (value == 255) value = Literal(token, index);
#elif M04_SZ_ARRAY_ALLOCATION_PROBE
            value = ArrayOverride(token, index);
            if (value == 255) value = Literal(token, index);
#else
            value = Literal(token, index);
#endif
            if (value == 0) return; Write(value);
        }
    }
#if M04_SZ_ARRAY_ALLOCATION_PROBE
    private static void A(int token)
    { for (int i = 0; ; i++) { byte value = ArrayLiteral(token, i); if (value == 0) return; Write(value); } }
    private static byte ArrayOverride(int token, int index) => token switch
    {
        0 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 83, 6 => 84, 7 => 65, 8 => 82, 9 => 84, 10 => 32, 11 => 109, 12 => 48, 13 => 52, 14 => 46, 15 => 48, 16 => 48, 17 => 56, 18 => 46, 19 => 115, 20 => 122, 21 => 95, 22 => 97, 23 => 114, 24 => 114, 25 => 97, 26 => 121, _ => 0 },
        6 => index switch { 0 => 67, 1 => 76, 2 => 82, 3 => 79, 4 => 83, 5 => 95, 6 => 77, 7 => 48, 8 => 52, 9 => 95, 10 => 48, 11 => 48, 12 => 56, 13 => 95, 14 => 82, 15 => 69, 16 => 65, 17 => 68, 18 => 89, 19 => 32, 20 => 111, 21 => 98, 22 => 106, 23 => 101, 24 => 99, 25 => 116, 26 => 115, 27 => 61, 28 => 53, 29 => 32, 30 => 102, 31 => 105, 32 => 120, 33 => 101, 34 => 100, 35 => 78, 36 => 101, 37 => 119, 38 => 61, 39 => 50, 40 => 32, 41 => 98, 42 => 121, 43 => 116, 44 => 101, 45 => 78, 46 => 101, 47 => 119, 48 => 61, 49 => 50, 50 => 32, 51 => 108, 52 => 111, 53 => 110, 54 => 103, 55 => 78, 56 => 101, 57 => 119, 58 => 61, 59 => 49, 60 => 32, 61 => 117, 62 => 115, 63 => 101, 64 => 100, 65 => 61, 66 => 49, 67 => 54, 68 => 48, 69 => 32, 70 => 99, 71 => 111, 72 => 117, 73 => 110, 74 => 116, 75 => 61, 76 => 53, 77 => 32, 78 => 104, 79 => 111, 80 => 115, 81 => 116, 82 => 67, 83 => 111, 84 => 110, 85 => 102, 86 => 111, 87 => 114, 88 => 109, 89 => 97, 90 => 110, 91 => 99, 92 => 101, 93 => 61, 94 => 102, 95 => 97, 96 => 108, 97 => 115, 98 => 101, 99 => 32, 100 => 103, 101 => 117, 102 => 101, 103 => 115, 104 => 116, 105 => 73, 106 => 110, 107 => 116, 108 => 101, 109 => 103, 110 => 114, 111 => 97, 112 => 116, 113 => 101, 114 => 100, 115 => 61, 116 => 116, 117 => 114, 118 => 117, 119 => 101, 120 => 32, 121 => 103, 122 => 97, 123 => 116, 124 => 101, 125 => 52, 126 => 61, 127 => 70, 128 => 97, 129 => 105, 130 => 108, _ => 0 },
        7 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 80, 6 => 65, 7 => 83, 8 => 83, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 56, 17 => 46, 18 => 115, 19 => 122, 20 => 95, 21 => 97, 22 => 114, 23 => 114, 24 => 97, 25 => 121, _ => 0 },
        8 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 82, 11 => 69, 12 => 65, 13 => 68, 14 => 66, 15 => 65, 16 => 67, 17 => 75, 18 => 32, 19 => 105, 20 => 100, 21 => 61, _ => 0 },
        15 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 65, 11 => 84, 12 => 84, 13 => 69, 14 => 77, 15 => 80, 16 => 84, 17 => 32, 18 => 114, 19 => 101, 20 => 97, 21 => 115, 22 => 111, 23 => 110, 24 => 61, _ => 0 },
        21 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 82, 11 => 69, 12 => 74, 13 => 69, 14 => 67, 15 => 84, 16 => 32, 17 => 114, 18 => 101, 19 => 97, 20 => 115, 21 => 111, 22 => 110, 23 => 61, _ => 0 },
        26 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 56, 17 => 46, 18 => 115, 19 => 122, 20 => 95, 21 => 97, 22 => 114, 23 => 114, 24 => 97, 25 => 121, 26 => 32, _ => 0 },
        29 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 73, 11 => 78, 12 => 84, 13 => 69, 14 => 82, 15 => 78, 16 => 65, 17 => 76, 18 => 95, 19 => 82, 20 => 69, 21 => 74, 22 => 69, 23 => 67, 24 => 84, 25 => 32, 26 => 97, 27 => 100, 28 => 97, 29 => 112, 30 => 116, 31 => 101, 32 => 114, 33 => 83, 34 => 116, 35 => 97, 36 => 116, 37 => 117, 38 => 115, 39 => 61, _ => 0 },
        31 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 56, 17 => 46, 18 => 115, 19 => 122, 20 => 95, 21 => 97, 22 => 114, 23 => 114, 24 => 97, 25 => 121, 26 => 32, 27 => 97, 28 => 100, 29 => 97, 30 => 112, 31 => 116, 32 => 101, 33 => 114, 34 => 95, 35 => 114, 36 => 101, 37 => 106, 38 => 101, 39 => 99, 40 => 116, 41 => 101, 42 => 100, _ => 0 },
        32 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 66, 11 => 73, 12 => 78, 13 => 68, 14 => 32, 15 => 105, 16 => 109, 17 => 97, 18 => 103, 19 => 101, 20 => 61, _ => 0 },
        47 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 79, 11 => 66, 12 => 74, 13 => 69, 14 => 67, 15 => 84, 16 => 32, 17 => 105, 18 => 100, 19 => 61, _ => 0 },
        63 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 82, 11 => 69, 12 => 84, 13 => 65, 14 => 73, 15 => 78, 16 => 32, 17 => 97, 18 => 108, 19 => 108, 20 => 111, 21 => 99, 22 => 97, 23 => 116, 24 => 111, 25 => 114, 26 => 61, _ => 0 },
        71 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 80, 11 => 82, 12 => 69, 13 => 80, 14 => 32, 15 => 105, 16 => 109, 17 => 97, 18 => 103, 19 => 101, 20 => 61, _ => 0 },
        _ => 255
    };
    private static byte ArrayLiteral(int token, int index) => token switch
    {
        0 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 65, 5 => 82, 6 => 82, 7 => 65, 8 => 89, 9 => 95, 10 => 70, 11 => 73, 12 => 76, 13 => 76, 14 => 32, 15 => 112, 16 => 97, 17 => 105, 18 => 114, 19 => 115, 20 => 61, _ => 0 },
        1 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 110, 4 => 115, 5 => 116, 6 => 114, 7 => 117, 8 => 99, 9 => 116, 10 => 111, 11 => 114, 12 => 115, 13 => 61, _ => 0 },
        2 => index switch { 0 => 32, 1 => 108, 2 => 101, 3 => 110, 4 => 103, 5 => 116, 6 => 104, 7 => 61, _ => 0 },
        3 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 87, 8 => 105, 9 => 100, 10 => 116, 11 => 104, 12 => 61, _ => 0 },
        4 => index switch { 0 => 32, 1 => 105, 2 => 100, 3 => 61, _ => 0 },
        5 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 61, _ => 0 },
        6 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 48, 9 => 61, _ => 0 },
        7 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 49, 9 => 61, _ => 0 },
        8 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 50, 9 => 61, _ => 0 },
        9 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 102, 10 => 105, 11 => 120, 12 => 101, 13 => 100, _ => 0 },
        10 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 98, 10 => 121, 11 => 116, 12 => 101, 13 => 95, 14 => 97, 15 => 114, 16 => 114, 17 => 97, 18 => 121, _ => 0 },
        11 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 108, 10 => 111, 11 => 110, 12 => 103, 13 => 95, 14 => 97, 15 => 114, 16 => 114, 17 => 97, 18 => 121, _ => 0 },
        _ => 0
    };
#endif
#if M04_STRING_ALLOCATION_PROBE
    private static void S(int token)
    {
        for (int i = 0; ; i++) { byte value = StringLiteral(token, i); if (value == 0) return; Write(value); }
    }
    private static byte StringOverride(int token, int index) => token switch
    {
        0 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 83, 6 => 84, 7 => 65, 8 => 82, 9 => 84, 10 => 32, 11 => 109, 12 => 48, 13 => 52, 14 => 46, 15 => 48, 16 => 48, 17 => 57, _ => 0 },
        6 => index switch { 0 => 67, 1 => 76, 2 => 82, 3 => 79, 4 => 83, 5 => 95, 6 => 77, 7 => 48, 8 => 52, 9 => 95, 10 => 48, 11 => 48, 12 => 57, 13 => 95, 14 => 82, 15 => 69, 16 => 65, 17 => 68, 18 => 89, 19 => 32, 20 => 97, 21 => 108, 22 => 108, 23 => 111, 24 => 99, 25 => 97, 26 => 116, 27 => 105, 28 => 111, 29 => 110, 30 => 115, 31 => 61, 32 => 55, 33 => 32, 34 => 117, 35 => 115, 36 => 101, 37 => 100, 38 => 66, 39 => 121, 40 => 116, 41 => 101, 42 => 115, 43 => 61, 44 => 50, 45 => 48, 46 => 48, 47 => 32, 48 => 115, 49 => 104, 50 => 97, 51 => 114, 52 => 101, 53 => 100, 54 => 83, 55 => 116, 56 => 97, 57 => 116, 58 => 101, 59 => 61, 60 => 116, 61 => 114, 62 => 117, 63 => 101, 64 => 32, 65 => 115, 66 => 116, 67 => 114, 68 => 105, 69 => 110, 70 => 103, 71 => 115, 72 => 61, 73 => 53, 74 => 32, 75 => 117, 76 => 116, 77 => 102, 78 => 49, 79 => 54, 80 => 61, 81 => 116, 82 => 114, 83 => 117, 84 => 101, 85 => 32, 86 => 116, 87 => 101, 88 => 114, 89 => 109, 90 => 105, 91 => 110, 92 => 97, 93 => 116, 94 => 111, 95 => 114, 96 => 90, 97 => 101, 98 => 114, 99 => 111, 100 => 61, 101 => 116, 102 => 114, 103 => 117, 104 => 101, 105 => 32, 106 => 112, 107 => 97, 108 => 100, 109 => 100, 110 => 105, 111 => 110, 112 => 103, 113 => 90, 114 => 101, 115 => 114, 116 => 111, 117 => 61, 118 => 116, 119 => 114, 120 => 117, 121 => 101, 122 => 32, 123 => 112, 124 => 114, 125 => 105, 126 => 111, 127 => 114, 128 => 79, 129 => 98, 130 => 106, 131 => 101, 132 => 99, 133 => 116, 134 => 115, 135 => 80, 136 => 114, 137 => 101, 138 => 115, 139 => 101, 140 => 114, 141 => 118, 142 => 101, 143 => 100, 144 => 61, 145 => 116, 146 => 114, 147 => 117, 148 => 101, _ => 0 },
        7 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 80, 6 => 65, 7 => 83, 8 => 83, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 57, _ => 0 },
        8 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 82, 12 => 69, 13 => 65, 14 => 68, 15 => 66, 16 => 65, 17 => 67, 18 => 75, 19 => 32, 20 => 105, 21 => 100, 22 => 61, _ => 0 },
        15 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 65, 12 => 84, 13 => 84, 14 => 69, 15 => 77, 16 => 80, 17 => 84, 18 => 32, 19 => 114, 20 => 101, 21 => 97, 22 => 115, 23 => 111, 24 => 110, 25 => 61, _ => 0 },
        21 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 82, 12 => 69, 13 => 74, 14 => 69, 15 => 67, 16 => 84, 17 => 32, 18 => 114, 19 => 101, 20 => 97, 21 => 115, 22 => 111, 23 => 110, 24 => 61, _ => 0 },
        26 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 57, 17 => 32, _ => 0 },
        29 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 73, 12 => 78, 13 => 84, 14 => 69, 15 => 82, 16 => 78, 17 => 65, 18 => 76, 19 => 95, 20 => 82, 21 => 69, 22 => 74, 23 => 69, 24 => 67, 25 => 84, 26 => 32, 27 => 97, 28 => 100, 29 => 97, 30 => 112, 31 => 116, 32 => 101, 33 => 114, 34 => 83, 35 => 116, 36 => 97, 37 => 116, 38 => 117, 39 => 115, 40 => 61, _ => 0 },
        31 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 57, 17 => 32, 18 => 97, 19 => 100, 20 => 97, 21 => 112, 22 => 116, 23 => 101, 24 => 114, 25 => 95, 26 => 114, 27 => 101, 28 => 106, 29 => 101, 30 => 99, 31 => 116, 32 => 101, 33 => 100, _ => 0 },
        32 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 66, 12 => 73, 13 => 78, 14 => 68, 15 => 32, 16 => 105, 17 => 109, 18 => 97, 19 => 103, 20 => 101, 21 => 61, _ => 0 },
        47 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 79, 12 => 66, 13 => 74, 14 => 69, 15 => 67, 16 => 84, 17 => 32, 18 => 105, 19 => 100, 20 => 61, _ => 0 },
        63 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 82, 12 => 69, 13 => 84, 14 => 65, 15 => 73, 16 => 78, 17 => 32, 18 => 97, 19 => 108, 20 => 108, 21 => 111, 22 => 99, 23 => 97, 24 => 116, 25 => 111, 26 => 114, 27 => 61, _ => 0 },
        71 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 80, 12 => 82, 13 => 69, 14 => 80, 15 => 32, 16 => 105, 17 => 109, 18 => 97, 19 => 103, 20 => 101, 21 => 61, _ => 0 },
        _ => 255
    };
    private static byte StringLiteral(int token, int index) => token switch
    {
        0 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 83, 5 => 84, 6 => 82, 7 => 73, 8 => 78, 9 => 71, 10 => 95, 11 => 70, 12 => 73, 13 => 76, 14 => 76, 15 => 32, 16 => 101, 17 => 109, 18 => 112, 19 => 116, 20 => 121, 21 => 67, 22 => 111, 23 => 117, 24 => 110, 25 => 116, 26 => 61, _ => 0 },
        1 => index switch { 0 => 32, 1 => 108, 2 => 97, 3 => 115, 4 => 116, 5 => 76, 6 => 101, 7 => 110, 8 => 103, 9 => 116, 10 => 104, 11 => 51, 12 => 67, 13 => 111, 14 => 117, 15 => 110, 16 => 116, 17 => 61, _ => 0 },
        2 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 110, 4 => 115, 5 => 116, 6 => 114, 7 => 117, 8 => 99, 9 => 116, 10 => 111, 11 => 114, 12 => 67, 13 => 111, 14 => 117, 15 => 110, 16 => 116, 17 => 61, _ => 0 },
        3 => index switch { 0 => 32, 1 => 108, 2 => 101, 3 => 110, 4 => 103, 5 => 116, 6 => 104, 7 => 61, _ => 0 },
        4 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 87, 8 => 105, 9 => 100, 10 => 116, 11 => 104, 12 => 61, _ => 0 },
        5 => index switch { 0 => 32, 1 => 116, 2 => 101, 3 => 114, 4 => 109, 5 => 105, 6 => 110, 7 => 97, 8 => 116, 9 => 111, 10 => 114, 11 => 61, _ => 0 },
        6 => index switch { 0 => 32, 1 => 116, 2 => 101, 3 => 114, 4 => 109, 5 => 105, 6 => 110, 7 => 97, 8 => 116, 9 => 111, 10 => 114, 11 => 86, 12 => 97, 13 => 108, 14 => 117, 15 => 101, 16 => 61, _ => 0 },
        7 => index switch { 0 => 105, 1 => 110, 2 => 118, 3 => 97, 4 => 108, 5 => 105, 6 => 100, 7 => 95, 8 => 108, 9 => 101, 10 => 110, 11 => 103, 12 => 116, 13 => 104, _ => 0 },
        8 => index switch { 0 => 32, 1 => 105, 2 => 100, 3 => 61, _ => 0 },
        9 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 108, 4 => 97, 5 => 116, 6 => 101, 7 => 100, 8 => 79, 9 => 98, 10 => 106, 11 => 101, 12 => 99, 13 => 116, 14 => 61, _ => 0 },
        10 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 102, 10 => 105, 11 => 120, 12 => 101, 13 => 100, _ => 0 },
        11 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 98, 10 => 121, 11 => 116, 12 => 101, 13 => 95, 14 => 97, 15 => 114, 16 => 114, 17 => 97, 18 => 121, _ => 0 },
        12 => index switch { 0 => 99, 1 => 111, 2 => 109, 3 => 112, 4 => 105, 5 => 108, 6 => 101, 7 => 100, 8 => 95, 9 => 115, 10 => 116, 11 => 114, 12 => 105, 13 => 110, 14 => 103, _ => 0 },
        13 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 61, _ => 0 },
        14 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 48, 9 => 61, _ => 0 },
        15 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 49, 9 => 61, _ => 0 },
        16 => index switch { 0 => 32, 1 => 101, 2 => 108, 3 => 101, 4 => 109, 5 => 101, 6 => 110, 7 => 116, 8 => 50, 9 => 61, _ => 0 },
        17 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 108, 4 => 97, 5 => 116, 6 => 101, 7 => 100, 8 => 79, 9 => 98, 10 => 106, 11 => 101, 12 => 99, 13 => 116, 14 => 48, 15 => 61, _ => 0 },
        18 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 108, 4 => 97, 5 => 116, 6 => 101, 7 => 100, 8 => 79, 9 => 98, 10 => 106, 11 => 101, 12 => 99, 13 => 116, 14 => 49, 15 => 61, _ => 0 },
        19 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 108, 4 => 97, 5 => 116, 6 => 101, 7 => 100, 8 => 79, 9 => 98, 10 => 106, 11 => 101, 12 => 99, 13 => 116, 14 => 50, 15 => 61, _ => 0 },
        _ => 0
    };
#endif

    private static byte Literal(int token, int index) => token switch
    {
        0 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 83, 6 => 84, 7 => 65, 8 => 82, 9 => 84, 10 => 32, 11 => 109, 12 => 48, 13 => 52, 14 => 46, 15 => 48, 16 => 48, 17 => 55, 18 => 46, 19 => 114, 20 => 104, 21 => 112, 22 => 95, 23 => 110, 24 => 101, 25 => 119, 26 => 95, 27 => 102, 28 => 97, 29 => 115, 30 => 116, _ => 0 },
        1 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 70, 9 => 73, 10 => 76, 11 => 76, 12 => 32, 13 => 99, 14 => 111, 15 => 117, 16 => 110, 17 => 116, 18 => 61, _ => 0 },
        2 => index switch { 0 => 32, 1 => 117, 2 => 115, 3 => 101, 4 => 100, 5 => 66, 6 => 101, 7 => 102, 8 => 111, 9 => 114, 10 => 101, 11 => 61, _ => 0 },
        3 => index switch { 0 => 32, 1 => 117, 2 => 115, 3 => 101, 4 => 100, 5 => 65, 6 => 102, 7 => 116, 8 => 101, 9 => 114, 10 => 61, _ => 0 },
        4 => index switch { 0 => 32, 1 => 98, 2 => 97, 3 => 115, 4 => 101, 5 => 61, _ => 0 },
        5 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 105, 4 => 111, 5 => 114, 6 => 77, 7 => 105, 8 => 115, 9 => 109, 10 => 97, 11 => 116, 12 => 99, 13 => 104, 14 => 66, 15 => 121, 16 => 116, 17 => 101, 18 => 115, 19 => 61, 20 => 48, _ => 0 },
        6 => index switch { 0 => 67, 1 => 76, 2 => 82, 3 => 79, 4 => 83, 5 => 95, 6 => 77, 7 => 48, 8 => 52, 9 => 95, 10 => 48, 11 => 48, 12 => 55, 13 => 95, 14 => 82, 15 => 69, 16 => 65, 17 => 68, 18 => 89, 19 => 32, 20 => 111, 21 => 98, 22 => 106, 23 => 101, 24 => 99, 25 => 116, 26 => 115, 27 => 61, 28 => 49, 29 => 48, 30 => 32, 31 => 99, 32 => 111, 33 => 109, 34 => 112, 35 => 105, 36 => 108, 37 => 101, 38 => 100, 39 => 78, 40 => 101, 41 => 119, 42 => 61, 43 => 52, 44 => 32, 45 => 100, 46 => 105, 47 => 114, 48 => 101, 49 => 99, 50 => 116, 51 => 61, 52 => 54, 53 => 32, 54 => 104, 55 => 111, 56 => 115, 57 => 116, 58 => 67, 59 => 111, 60 => 110, 61 => 102, 62 => 111, 63 => 114, 64 => 109, 65 => 97, 66 => 110, 67 => 99, 68 => 101, 69 => 61, 70 => 102, 71 => 97, 72 => 108, 73 => 115, 74 => 101, 75 => 32, 76 => 103, 77 => 117, 78 => 101, 79 => 115, 80 => 116, 81 => 73, 82 => 110, 83 => 116, 84 => 101, 85 => 103, 86 => 114, 87 => 97, 88 => 116, 89 => 101, 90 => 100, 91 => 61, 92 => 116, 93 => 114, 94 => 117, 95 => 101, 96 => 32, 97 => 103, 98 => 97, 99 => 116, 100 => 101, 101 => 52, 102 => 61, 103 => 70, 104 => 97, 105 => 105, 106 => 108, _ => 0 },
        7 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 80, 6 => 65, 7 => 83, 8 => 83, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 55, 17 => 46, 18 => 114, 19 => 104, 20 => 112, 21 => 95, 22 => 110, 23 => 101, 24 => 119, 25 => 95, 26 => 102, 27 => 97, 28 => 115, 29 => 116, _ => 0 },
        8 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 82, 9 => 69, 10 => 65, 11 => 68, 12 => 66, 13 => 65, 14 => 67, 15 => 75, 16 => 32, 17 => 105, 18 => 100, 19 => 61, _ => 0 },
        9 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 79, 8 => 102, 9 => 102, 10 => 115, 11 => 101, 12 => 116, 13 => 61, _ => 0 },
        10 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 61, _ => 0 },
        11 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 102, 4 => 101, 5 => 114, 6 => 101, 7 => 110, 8 => 99, 9 => 101, 10 => 83, 11 => 108, 12 => 111, 13 => 116, 14 => 66, 15 => 121, 16 => 116, 17 => 101, 18 => 115, 19 => 61, _ => 0 },
        12 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 102, 4 => 101, 5 => 114, 6 => 101, 7 => 110, 8 => 99, 9 => 101, 10 => 83, 11 => 108, 12 => 111, 13 => 116, 14 => 79, 15 => 114, 16 => 61, _ => 0 },
        13 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 105, 4 => 111, 5 => 114, 6 => 67, 7 => 111, 8 => 117, 9 => 110, 10 => 116, 11 => 61, _ => 0 },
        14 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 105, 4 => 111, 5 => 114, 6 => 77, 7 => 105, 8 => 115, 9 => 109, 10 => 97, 11 => 116, 12 => 99, 13 => 104, 14 => 66, 15 => 121, 16 => 116, 17 => 101, 18 => 115, 19 => 61, 20 => 48, 21 => 32, 22 => 99, 23 => 97, 24 => 110, 25 => 97, 26 => 114, 27 => 121, 28 => 77, 29 => 105, 30 => 115, 31 => 109, 32 => 97, 33 => 116, 34 => 99, 35 => 104, 36 => 66, 37 => 121, 38 => 116, 39 => 101, 40 => 115, 41 => 61, 42 => 48, _ => 0 },
        15 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 65, 9 => 84, 10 => 84, 11 => 69, 12 => 77, 13 => 80, 14 => 84, 15 => 32, 16 => 114, 17 => 101, 18 => 97, 19 => 115, 20 => 111, 21 => 110, 22 => 61, _ => 0 },
        16 => index switch { 0 => 101, 1 => 120, 2 => 104, 3 => 97, 4 => 117, 5 => 115, 6 => 116, 7 => 105, 8 => 111, 9 => 110, _ => 0 },
        17 => index switch { 0 => 98, 1 => 101, 2 => 102, 3 => 111, 4 => 114, 5 => 101, 6 => 95, 7 => 98, 8 => 105, 9 => 110, 10 => 100, _ => 0 },
        18 => index switch { 0 => 32, 1 => 105, 2 => 100, 3 => 61, 4 => 49, 5 => 32, 6 => 109, 7 => 116, 8 => 61, _ => 0 },
        19 => index switch { 0 => 32, 1 => 117, 2 => 115, 3 => 101, 4 => 100, 5 => 61, _ => 0 },
        20 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 117, 4 => 110, 5 => 116, 6 => 61, _ => 0 },
        21 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 82, 9 => 69, 10 => 74, 11 => 69, 12 => 67, 13 => 84, 14 => 32, 15 => 114, 16 => 101, 17 => 97, 18 => 115, 19 => 111, 20 => 110, 21 => 61, _ => 0 },
        22 => index switch { 0 => 32, 1 => 115, 2 => 116, 3 => 97, 4 => 116, 5 => 117, 6 => 115, 7 => 61, _ => 0 },
        23 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 117, 4 => 110, 5 => 116, 6 => 66, 7 => 101, 8 => 102, 9 => 111, 10 => 114, 11 => 101, 12 => 61, _ => 0 },
        24 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 117, 4 => 110, 5 => 116, 6 => 65, 7 => 102, 8 => 116, 9 => 101, 10 => 114, 11 => 61, _ => 0 },
        25 => index switch { 0 => 32, 1 => 109, 2 => 117, 3 => 116, 4 => 97, 5 => 116, 6 => 105, 7 => 111, 8 => 110, 9 => 66, 10 => 121, 11 => 116, 12 => 101, 13 => 115, 14 => 61, 15 => 48, 16 => 32, 17 => 99, 18 => 111, 19 => 110, 20 => 115, 21 => 116, 22 => 114, 23 => 117, 24 => 99, 25 => 116, 26 => 111, 27 => 114, 28 => 67, 29 => 111, 30 => 117, 31 => 110, 32 => 116, 33 => 61, _ => 0 },
        26 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 55, 17 => 46, 18 => 114, 19 => 104, 20 => 112, 21 => 95, 22 => 110, 23 => 101, 24 => 119, 25 => 95, 26 => 102, 27 => 97, 28 => 115, 29 => 116, 30 => 32, _ => 0 },
        27 => index switch { 0 => 110, 1 => 111, 2 => 116, 3 => 95, 4 => 105, 5 => 110, 6 => 105, 7 => 116, 8 => 105, 9 => 97, 10 => 108, 11 => 105, 12 => 122, 13 => 101, 14 => 100, _ => 0 },
        28 => index switch { 0 => 101, 1 => 120, 2 => 104, 3 => 97, 4 => 117, 5 => 115, 6 => 116, 7 => 101, 8 => 100, _ => 0 },
        29 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 73, 9 => 78, 10 => 84, 11 => 69, 12 => 82, 13 => 78, 14 => 65, 15 => 76, 16 => 95, 17 => 82, 18 => 69, 19 => 74, 20 => 69, 21 => 67, 22 => 84, 23 => 32, 24 => 97, 25 => 100, 26 => 97, 27 => 112, 28 => 116, 29 => 101, 30 => 114, 31 => 83, 32 => 116, 33 => 97, 34 => 116, 35 => 117, 36 => 115, 37 => 61, _ => 0 },
        30 => index switch { 0 => 32, 1 => 115, 2 => 116, 3 => 97, 4 => 103, 5 => 101, 6 => 61, _ => 0 },
        31 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 48, 16 => 55, 17 => 46, 18 => 114, 19 => 104, 20 => 112, 21 => 95, 22 => 110, 23 => 101, 24 => 119, 25 => 95, 26 => 102, 27 => 97, 28 => 115, 29 => 116, 30 => 32, 31 => 97, 32 => 100, 33 => 97, 34 => 112, 35 => 116, 36 => 101, 37 => 114, 38 => 95, 39 => 114, 40 => 101, 41 => 106, 42 => 101, 43 => 99, 44 => 116, 45 => 101, 46 => 100, _ => 0 },
        32 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 66, 9 => 73, 10 => 78, 11 => 68, 12 => 32, 13 => 105, 14 => 109, 15 => 97, 16 => 103, 17 => 101, 18 => 61, _ => 0 },
        33 => index switch { 0 => 32, 1 => 105, 2 => 109, 3 => 97, 4 => 103, 5 => 101, 6 => 66, 7 => 121, 8 => 116, 9 => 101, 10 => 115, 11 => 61, _ => 0 },
        34 => index switch { 0 => 32, 1 => 97, 2 => 108, 3 => 108, 4 => 111, 5 => 99, 6 => 97, 7 => 116, 8 => 111, 9 => 114, 10 => 61, _ => 0 },
        35 => index switch { 0 => 32, 1 => 110, 2 => 111, 3 => 100, 4 => 101, 5 => 115, 6 => 61, _ => 0 },
        36 => index switch { 0 => 32, 1 => 108, 2 => 101, 3 => 100, 4 => 103, 5 => 101, 6 => 114, 7 => 61, _ => 0 },
        37 => index switch { 0 => 32, 1 => 108, 2 => 101, 3 => 100, 4 => 103, 5 => 101, 6 => 114, 7 => 66, 8 => 121, 9 => 116, 10 => 101, 11 => 115, 12 => 61, _ => 0 },
        38 => index switch { 0 => 32, 1 => 112, 2 => 97, 3 => 114, 4 => 101, 5 => 110, 6 => 116, 7 => 73, 8 => 110, 9 => 100, 10 => 101, 11 => 120, 12 => 61, _ => 0 },
        39 => index switch { 0 => 32, 1 => 98, 2 => 97, 3 => 99, 4 => 107, 5 => 105, 6 => 110, 7 => 103, 8 => 61, _ => 0 },
        40 => index switch { 0 => 32, 1 => 98, 2 => 97, 3 => 99, 4 => 107, 5 => 105, 6 => 110, 7 => 103, 8 => 66, 9 => 121, 10 => 116, 11 => 101, 12 => 115, 13 => 61, _ => 0 },
        41 => index switch { 0 => 32, 1 => 104, 2 => 101, 3 => 97, 4 => 112, 5 => 61, _ => 0 },
        42 => index switch { 0 => 32, 1 => 104, 2 => 101, 3 => 97, 4 => 112, 5 => 66, 6 => 121, 7 => 116, 8 => 101, 9 => 115, 10 => 61, _ => 0 },
        43 => index switch { 0 => 32, 1 => 97, 2 => 99, 3 => 99, 4 => 101, 5 => 115, 6 => 115, 7 => 61, _ => 0 },
        44 => index switch { 0 => 32, 1 => 97, 2 => 99, 3 => 99, 4 => 101, 5 => 115, 6 => 115, 7 => 66, 8 => 121, 9 => 116, 10 => 101, 11 => 115, 12 => 61, _ => 0 },
        45 => index switch { 0 => 32, 1 => 98, 2 => 117, 3 => 109, 4 => 112, 5 => 61, _ => 0 },
        46 => index switch { 0 => 32, 1 => 98, 2 => 117, 3 => 109, 4 => 112, 5 => 66, 6 => 121, 7 => 116, 8 => 101, 9 => 115, 10 => 61, _ => 0 },
        47 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 79, 9 => 66, 10 => 74, 11 => 69, 12 => 67, 13 => 84, 14 => 32, 15 => 105, 16 => 100, 17 => 61, _ => 0 },
        48 => index switch { 0 => 32, 1 => 114, 2 => 111, 3 => 117, 4 => 116, 5 => 101, 6 => 61, _ => 0 },
        49 => index switch { 0 => 110, 1 => 101, 2 => 119, _ => 0 },
        50 => index switch { 0 => 100, 1 => 105, 2 => 114, 3 => 101, 4 => 99, 5 => 116, _ => 0 },
        51 => index switch { 0 => 32, 1 => 109, 2 => 116, 3 => 61, _ => 0 },
        52 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 101, 4 => 102, 5 => 105, 6 => 120, 7 => 48, 8 => 61, _ => 0 },
        53 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 101, 4 => 102, 5 => 105, 6 => 120, 7 => 49, 8 => 61, _ => 0 },
        54 => index switch { 0 => 32, 1 => 112, 2 => 114, 3 => 101, 4 => 102, 5 => 105, 6 => 120, 7 => 50, 8 => 61, _ => 0 },
        55 => index switch { 0 => 32, 1 => 102, 2 => 108, 3 => 97, 4 => 103, 5 => 115, 6 => 61, _ => 0 },
        56 => index switch { 0 => 32, 1 => 97, 2 => 108, 3 => 108, 4 => 111, 5 => 99, 6 => 97, 7 => 116, 8 => 105, 9 => 111, 10 => 110, 11 => 61, _ => 0 },
        57 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 102, 4 => 101, 5 => 114, 6 => 101, 7 => 110, 8 => 99, 9 => 101, 10 => 61, _ => 0 },
        58 => index switch { 0 => 32, 1 => 101, 2 => 110, 3 => 100, 4 => 61, _ => 0 },
        59 => index switch { 0 => 32, 1 => 115, 2 => 121, 3 => 110, 4 => 99, 5 => 61, _ => 0 },
        60 => index switch { 0 => 32, 1 => 111, 2 => 98, 3 => 115, 4 => 101, 5 => 114, 6 => 118, 7 => 101, 8 => 100, 9 => 77, 10 => 116, 11 => 61, _ => 0 },
        61 => index switch { 0 => 32, 1 => 122, 2 => 101, 3 => 114, 4 => 111, 5 => 66, 6 => 121, 7 => 116, 8 => 101, 9 => 115, 10 => 61, _ => 0 },
        62 => index switch { 0 => 32, 1 => 122, 2 => 101, 3 => 114, 4 => 111, 5 => 79, 6 => 114, 7 => 61, _ => 0 },
        63 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 82, 9 => 69, 10 => 84, 11 => 65, 12 => 73, 13 => 78, 14 => 32, 15 => 97, 16 => 108, 17 => 108, 18 => 111, 19 => 99, 20 => 97, 21 => 116, 22 => 111, 23 => 114, 24 => 61, _ => 0 },
        64 => index switch { 0 => 32, 1 => 104, 2 => 101, 3 => 97, 4 => 112, 5 => 83, 6 => 116, 7 => 97, 8 => 114, 9 => 116, 10 => 61, _ => 0 },
        65 => index switch { 0 => 32, 1 => 104, 2 => 101, 3 => 97, 4 => 112, 5 => 69, 6 => 110, 7 => 100, 8 => 61, _ => 0 },
        66 => index switch { 0 => 32, 1 => 105, 2 => 110, 3 => 105, 4 => 116, 5 => 105, 6 => 97, 7 => 108, 8 => 80, 9 => 97, 10 => 103, 11 => 101, 12 => 115, 13 => 61, _ => 0 },
        67 => index switch { 0 => 32, 1 => 102, 2 => 114, 3 => 101, 4 => 101, 5 => 80, 6 => 97, 7 => 103, 8 => 101, 9 => 115, 10 => 61, _ => 0 },
        68 => index switch { 0 => 32, 1 => 97, 2 => 108, 3 => 108, 4 => 111, 5 => 99, 6 => 97, 7 => 116, 8 => 101, 9 => 100, 10 => 80, 11 => 97, 12 => 103, 13 => 101, 14 => 115, 15 => 61, _ => 0 },
        69 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 97, 4 => 100, 5 => 66, 6 => 121, 7 => 116, 8 => 101, 9 => 115, 10 => 61, _ => 0 },
        70 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 110, 4 => 116, 5 => 101, 6 => 110, 7 => 116, 8 => 115, 9 => 90, 10 => 101, 11 => 114, 12 => 111, 13 => 61, 14 => 102, 15 => 97, 16 => 108, 17 => 115, 18 => 101, 19 => 32, 20 => 117, 21 => 115, 22 => 101, 23 => 100, 24 => 61, _ => 0 },
        71 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 72, 6 => 80, 7 => 95, 8 => 80, 9 => 82, 10 => 69, 11 => 80, 12 => 32, 13 => 105, 14 => 109, 15 => 97, 16 => 103, 17 => 101, 18 => 61, _ => 0 },
        72 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 110, 4 => 116, 5 => 114, 6 => 111, 7 => 108, 8 => 61, _ => 0 },
        73 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 110, 4 => 116, 5 => 114, 6 => 111, 7 => 108, 8 => 66, 9 => 121, 10 => 116, 11 => 101, 12 => 115, 13 => 61, _ => 0 },
        74 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 99, 4 => 111, 5 => 114, 6 => 100, 7 => 115, 8 => 61, _ => 0 },
        75 => index switch { 0 => 32, 1 => 114, 2 => 101, 3 => 99, 4 => 111, 5 => 114, 6 => 100, 7 => 115, 8 => 66, 9 => 121, 10 => 116, 11 => 101, 12 => 115, 13 => 61, _ => 0 },
        _ => 0
    };
}

#pragma warning disable 649
internal sealed class RhpPlainFixture
{
    internal ulong Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal RhpPlainFixture(ulong value) { RhpNewFastAllocation.ConstructorEntered(); Value = value; }
}
internal sealed class RhpReferenceFixture
{
    internal object Reference;
    internal ulong Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal RhpReferenceFixture(ulong value) { RhpNewFastAllocation.ConstructorEntered(); Value = value; }
}
internal sealed class RhpGenericFixture<T>
{
    internal T Item;
    internal ulong Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal RhpGenericFixture(ulong value) { RhpNewFastAllocation.ConstructorEntered(); Value = value; }
}
internal struct RhpFixedValueFixture { internal ulong Value; }
internal struct RhpReferenceValueFixture { internal object Reference; internal ulong Value; }
internal struct RhpGenericValueFixture<T> { internal T Value; }
internal enum RhpEnumFixture : int { Zero, One }
#pragma warning restore 649
#endif
