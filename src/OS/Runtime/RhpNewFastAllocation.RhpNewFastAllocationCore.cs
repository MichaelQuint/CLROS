using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
internal static unsafe partial class RhpNewFastAllocationCore
{
    internal const ulong Canary = 0x71C4B059A386D2EF;
    internal static ulong ControlBytes => (ulong)sizeof(RhpAllocationControl);
    internal static ulong ObservationBytes => (ulong)sizeof(RhpObservationStorage);

    // Supplied image is an original UEFI-loaded, caller-attested identity mapping.
    // Numeric bounds and PE flags alone do not establish active page permissions.
    internal static RhpAdmissionStatus Prepare(RhpNewFastHeapAccess original, RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, byte* image, ulong imageBytes)
    {
        RhpAdmissionStatus storage = Storage(original, access, bump, control, records);
        if (storage != RhpAdmissionStatus.Success)
            return storage;
        if (!Zero((byte*)access, 104) || !Zero((byte*)bump, 80) || !Zero((byte*)control, ControlBytes) || !Zero((byte*)records, ObservationBytes))
            return RhpAdmissionStatus.AlreadyPrepared;
        if (!Image(image, imageBytes, out _, out _, out _))
            return RhpAdmissionStatus.InvalidImage;
        ManagedHeapPageRangeResult retained = Retained(original);
        if (!retained.IsReady || retained.ReadBytes != 4096 || !retained.ContentsAreZeroNow || retained.MappedStart != (ulong)original.Backing)
            return RhpAdmissionStatus.RetainedRejected;
        // All supplied extents and original authority were validated before first write.
        *access = original;
        *control = new RhpAllocationControl
        {
            CanaryBefore = Canary,
            CanaryAfter = Canary,
            Self = (ulong)control,
            Access = (ulong)access,
            Bump = (ulong)bump,
            Records = (ulong)records,
            Image = (ulong)image,
            ImageBytes = imageBytes,
            Prepared = 1
        };
        records->CanaryBefore = Canary;
        records->CanaryAfter = Canary;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpNewFastResult Bind(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success)
            return default;
        return GuestTestRhpNewFast.InitializeOnce(*access, bump, 80);
    }

    internal static RhpAllocationAttempt Allocate(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token, ulong expectedToken, ulong id, ulong route) => AllocateCore(access, bump, control, records, token, expectedToken, id, route, false, 0, 0);
    internal static RhpAllocationAttempt AllocateMixed(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token, ulong expectedToken, ulong id, ulong route, long length, ulong expectedElement) => AllocateCore(access, bump, control, records, token, expectedToken, id, route, true, length, expectedElement);
    internal static RhpAllocationAttempt AllocateStrings(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token, ulong expectedToken, ulong id, ulong route, long length, ulong related) => AllocateCore(access, bump, control, records, token, expectedToken, id, route, false, length, related, true);
    private static RhpAllocationAttempt AllocateCore(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token, ulong expectedToken, ulong id, ulong route, bool mixed, long length, ulong expectedElement, bool strings = false, bool references = false)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return new(ready);
        if (control->InProgress != 0)
            return new(RhpAdmissionStatus.Reentry);
        if (token == 0 || token != expectedToken)
            return new(RhpAdmissionStatus.UnknownToken);
        bool text = strings && route == 5;
        bool array = strings ? route == 6 : mixed && (route == 3 || route == 4);
        if (array && length != (strings ? 1 : id == 3 ? control->Completed == 5 ? 1 : 3 : id == 5 ? 1 : 0))
            return new(RhpAdmissionStatus.InvalidFixture);
        bool filler = strings ? control->Completed == 7 && id == 3 : mixed ? control->Completed == 5 && id <= 3 : control->Completed == 10 && id == 1 && route == 1;
        if (references)
        {
            if (mixed || strings || length != 0 || expectedElement != 0 || route != 7 || id == 0 || id > 6 || id != control->Completed + 1)
                return new(RhpAdmissionStatus.InvalidFixture);
            filler = false;
        }
        else if (strings)
        {
            bool unbound = id == 3 && control->Completed == 0 && !bump->IsInitialized && length == 0;
            bool invalidLength = id == 3 && control->Completed == 0 && bump->IsInitialized && length == 0x100000001L;
            if (id == 0 || id > 7 || route != (id == 1 ? 1UL : id == 2 ? 6UL : 5UL) || (!filler && !unbound && !invalidLength && id != control->Completed + 1) || (text && !unbound && !invalidLength && length != (filler ? bump->AllocationCount < 168 ? 0 : bump->AllocationCount == 168 ? 3 : 1 : id == 3 ? 0 : id == 4 ? 1 : id == 7 ? 2 : 3)) || (filler && (bump->AllocationCount < 7 || bump->AllocationCount > 169)))
                return new(RhpAdmissionStatus.InvalidFixture);
        }
        else if (id == 0 || id > (mixed ? 5UL : 10UL) || route != (mixed ? (id == 1 || id == 4 ? 1UL : id == 3 ? 4UL : 3UL) : id <= 4 ? 1UL : 2UL) || (!filler && id != control->Completed + 1 && !(mixed && id == 2 && control->Completed == 0 && !bump->IsInitialized)))
            return new(RhpAdmissionStatus.InvalidFixture);
        if (!PrefixDisjoint(*access, access, bump, control, records, token) || !TryReadPrefix((byte*)control->Image, control->ImageBytes, token, expectedToken, out NativeAotMethodTablePrefix prefix, out ulong word0, out ulong word1, out ulong word2))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        ulong element0 = 0, element1 = 0, element2 = 0;
        if (array && (prefix.Flags != (route == 4 ? 0xE0020008U : 0xE0020001U) || prefix.BaseSize != 24 || prefix.RelatedTypeToken != expectedElement || !PrefixDisjoint(*access, access, bump, control, records, expectedElement) || !TryReadPrefix((byte*)control->Image, control->ImageBytes, prefix.RelatedTypeToken, expectedElement, out NativeAotMethodTablePrefix element, out element0, out element1, out element2) || ((element.Flags >> 26) & 31) != (route == 4 ? 10U : 5U) || element.BaseSize != 24))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        if (text && (prefix.Flags != 0xD0000002U || prefix.BaseSize != 22 || prefix.RelatedTypeToken != expectedElement || expectedElement == token || !PrefixDisjoint(*access, access, bump, control, records, expectedElement) || !TryReadPrefix((byte*)control->Image, control->ImageBytes, prefix.RelatedTypeToken, expectedElement, out NativeAotMethodTablePrefix relatedObject, out element0, out element1, out element2) || relatedObject.BaseSize != 24 || relatedObject.RelatedTypeToken != 0))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        if (!Prior(records, control->Completed, (ulong)access->Backing, references))
            return new(RhpAdmissionStatus.PriorObjectChanged);
        for (ulong i = 0; i < 4096; i++)
            records->BeforeAttempt[i] = access->Backing[i];
        byte* bumpBefore = stackalloc byte[80];
        for (ulong i = 0; i < 80; i++)
            bumpBefore[i] = ((byte*)bump)[i];
        control->InProgress = 1;
        RhpNewFastResult result = text ? GuestTestStringAllocationCharacterStorage.TryAllocate(*access, bump, 80, NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token, length) : array ? GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(*access, bump, 80, NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token, length) : GuestTestRhpNewFast.TryAllocate(*access, bump, 80, NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token);
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
            observation.ElementTable = expectedElement;
            observation.ElementPrefix0 = element0;
            observation.ElementPrefix1 = element1;
            observation.ElementPrefix2 = element2;
        }

        if (text)
        {
            observation.Length = Read32((byte*)observation.Allocation + 16);
            observation.RelatedObjectTable = expectedElement;
            observation.RelatedObjectPrefix0 = element0;
            observation.RelatedObjectPrefix1 = element1;
            observation.RelatedObjectPrefix2 = element2;
        }

        if (Read64((byte*)token) != word0 || Read64((byte*)token + 8) != word1 || Read64((byte*)token + 16) != word2 || ((array || text) && (Read64((byte*)expectedElement) != element0 || Read64((byte*)expectedElement + 8) != element1 || Read64((byte*)expectedElement + 16) != element2)))
            return new(RhpAdmissionStatus.PrefixChanged, result, observation);
        ulong expectedBytes = text ? (22 + (ulong)length * 2 + 7) & ~7UL : array ? (24 + (ulong)(uint)length * (route == 4 ? 8UL : 1UL) + 7) & ~7UL : prefix.BaseSize;
        if (observation.Sync != 0 || observation.ObservedMt != token || observation.ZeroOr != 0 || observation.Reference != observation.Allocation + 8 || observation.End != observation.Allocation + expectedBytes || observation.UsedAfter != observation.UsedBefore + expectedBytes || ((array || text) && observation.Length != (ulong)length) || !Prior(records, control->Completed, (ulong)access->Backing, references))
            return new(RhpAdmissionStatus.ObservationRejected, result, observation);
        if (!filler)
            (&records->Object0)[id - 1] = observation;
        control->InProgress = 0;
        return new(RhpAdmissionStatus.Success, result, observation);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool RejectedAllocationUnchanged(RhpNewFastBumpState* bump, byte* bumpBefore, byte* backing, byte* backingBefore)
    {
        for (ulong i = 0; i < 80; i++)
            if (((byte*)bump)[i] != bumpBefore[i])
                return false;
        for (ulong i = 0; i < 4096; i++)
            if (backing[i] != backingBefore[i])
                return false;
        return true;
    }

    internal static RhpAdmissionStatus Complete(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong id, ulong reference, ulong scalarOffset, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return ready;
        if (control->InProgress != 0)
            return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 10)
            return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* record = &(&records->Object0)[id - 1];
        if (record->UsedBefore > 4096 || record->BaseSize < 24 || record->BaseSize > 4096 - record->UsedBefore || record->Allocation != (ulong)access->Backing + record->UsedBefore || record->Reference != record->Allocation + 8 || record->End != record->Allocation + record->BaseSize || record->Id != id || record->Reference != reference || record->Reference == 0 || (id <= 4 && (scalarOffset != (id == 1 ? 8UL : 16UL) || typedScalar != expectedScalar)) || (id > 4 && (scalarOffset != 0 || typedScalar != 0 || expectedScalar != 0)))
            return RhpAdmissionStatus.ObservationRejected;
        if (id <= 4 && (scalarOffset > record->BaseSize - 16 || Read64((byte*)reference + scalarOffset) != typedScalar))
            return RhpAdmissionStatus.ObservationRejected;
        record->ScalarOffset = scalarOffset;
        record->Scalar = typedScalar;
        record->ScalarWidth = id <= 4 ? 8UL : 0;
        record->UntouchedBytes = record->BaseSize - 16 - (id <= 4 ? 8UL : 0UL);
        record->UntouchedOr = Untouched(*record);
        if (!Prior(records, id, (ulong)access->Backing))
            return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CompleteMixed(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong id, ulong reference, int typedLength, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return ready;
        if (control->InProgress != 0)
            return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 5)
            return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        bool array = id == 2 || id == 3 || id == 5;
        ulong length = id == 3 ? 3UL : id == 5 ? 1UL : 0;
        ulong width = id == 2 ? 0UL : id == 5 ? 1UL : 8UL;
        ulong offset = id == 1 ? 8UL : id == 2 ? 0UL : id == 3 ? 24UL : 16UL;
        if (r->Id != id || r->Reference != reference || r->Reference == 0 || r->Route != (array ? id == 3 ? 4UL : 3UL : 1UL) || (array && (typedLength < 0 || (ulong)typedLength != length || r->Length != length)) || (!array && typedLength != 0) || typedScalar != expectedScalar || (width == 0 && typedScalar != 0) || r->BaseSize < 24 || r->UsedBefore > 4096 || r->BaseSize > 4096 - r->UsedBefore || r->Allocation != (ulong)access->Backing + r->UsedBefore || r->Reference != r->Allocation + 8 || r->End != r->Allocation + r->BaseSize || (width != 0 && (offset > r->BaseSize - 8 || width > r->BaseSize - 8 - offset || ReadScalar((byte*)reference + offset, width) != typedScalar)))
            return RhpAdmissionStatus.ObservationRejected;
        r->ScalarOffset = offset;
        r->ScalarWidth = width;
        r->Scalar = typedScalar;
        r->UntouchedBytes = r->BaseSize - 16 - (array ? 4UL : 0UL) - width;
        r->UntouchedOr = Untouched(*r);
        if (!Prior(records, id, (ulong)access->Backing))
            return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CompleteStrings(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong id, ulong reference, int typedLength, ulong typedScalar, ulong expectedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return ready;
        if (control->InProgress != 0)
            return RhpAdmissionStatus.Reentry;
        if (id != control->Completed + 1 || id > 7)
            return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        ulong length = id == 1 || id == 3 ? 0UL : id == 2 || id == 4 ? 1UL : id == 7 ? 2UL : 3UL;
        ulong width = id == 1 ? 8UL : id == 2 ? 1UL : length * 2;
        ulong offset = id == 1 ? 8UL : id == 2 ? 16UL : 12UL;
        if (r->Id != id || r->Reference != reference || reference == 0 || r->Route != (id == 1 ? 1UL : id == 2 ? 6UL : 5UL) || typedLength < 0 || (ulong)typedLength != length || r->Length != length || typedScalar != expectedScalar || (width == 0 && typedScalar != 0) || r->BaseSize < 24 || r->UsedBefore > 4096 || r->BaseSize > 4096 - r->UsedBefore || r->Allocation != (ulong)access->Backing + r->UsedBefore || reference != r->Allocation + 8 || r->End != r->Allocation + r->BaseSize || offset > r->BaseSize - 8 || width > r->BaseSize - 8 - offset || ReadScalar((byte*)reference + offset, width) != typedScalar)
            return RhpAdmissionStatus.ObservationRejected;
        r->ScalarOffset = offset;
        r->ScalarWidth = width;
        r->Scalar = typedScalar;
        r->UntouchedBytes = r->BaseSize - 16 - (id == 1 ? 0UL : 4UL) - width;
        r->UntouchedOr = Untouched(*r);
        if (!Prior(records, id, (ulong)access->Backing))
            return RhpAdmissionStatus.PriorObjectChanged;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    internal static RhpAdmissionStatus CheckCompleted(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return ready;
        return control->InProgress == 0 && Prior(records, control->Completed, (ulong)access->Backing) ? RhpAdmissionStatus.Success : RhpAdmissionStatus.PriorObjectChanged;
    }

    internal static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) => ManagedHeapPageRangeLiveAllocator.ValidateRetained(a.Allocator, a.AllocatorCapacity, a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount, a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);
    internal static RhpAdmissionStatus Ready(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        // These are supplied readable POD slots; validate their numeric extents before reads.
        if (!Range((ulong)access, 104, 8) || !Range((ulong)bump, 80, 8) || !Range((ulong)control, ControlBytes, 8) || !Range((ulong)records, ObservationBytes, 8))
            return RhpAdmissionStatus.InvalidStorage;
        if (control->Prepared != 1)
            return RhpAdmissionStatus.NotPrepared;
        RhpAdmissionStatus storage = Storage(*access, access, bump, control, records);
        if (storage != RhpAdmissionStatus.Success)
            return storage;
        if (control->Self != (ulong)control || control->Access != (ulong)access || control->Bump != (ulong)bump || control->Records != (ulong)records || control->Completed > 10 || control->CanaryBefore != Canary || control->CanaryAfter != Canary || records->CanaryBefore != Canary || records->CanaryAfter != Canary)
            return RhpAdmissionStatus.InvalidStorage;
        ManagedHeapPageRangeResult retained = Retained(*access);
        if (!retained.IsReady || retained.ReadBytes != 4096 || retained.MappedStart != (ulong)access->Backing)
            return RhpAdmissionStatus.RetainedRejected;
        return RhpAdmissionStatus.Success;
    }

    private static RhpAdmissionStatus Storage(RhpNewFastHeapAccess a, RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records)
    {
        if (sizeof(void*) != 8 || sizeof(RhpNewFastHeapAccess) != 104 || sizeof(RhpNewFastBumpState) != 80 || sizeof(ManagedHeapPageRangeState) != 216 || sizeof(RhpObservationStorage) != 4112 + 10 * sizeof(RhpObjectObservation) || a.AllocatorCapacity != 1 || a.NodeCount != 1 || a.NodeCapacity != 1 || a.ReservationCount != 5 || a.ReservationCapacity != 5 || a.ParentIndex >= 5 || a.BackingCapacity != 8192 || a.HeapCapacity != 216)
            return RhpAdmissionStatus.InvalidStorage;
        ulong* starts = stackalloc ulong[9]
        {
            (ulong)a.Allocator,
            (ulong)a.Nodes,
            (ulong)a.Reservations,
            (ulong)a.Heap,
            (ulong)a.Backing,
            (ulong)access,
            (ulong)bump,
            (ulong)control,
            (ulong)records
        };
        ulong* lengths = stackalloc ulong[9]
        {
            56,
            48,
            200,
            216,
            8192,
            104,
            80,
            ControlBytes,
            ObservationBytes
        };
        for (int i = 0; i < 9; i++)
            if (!Range(starts[i], lengths[i], i == 4 ? 4096UL : 8UL))
                return RhpAdmissionStatus.InvalidStorage;
        for (int i = 0; i < 9; i++)
            for (int j = i + 1; j < 9; j++)
                if (Overlap(starts[i], lengths[i], starts[j], lengths[j]))
                    return RhpAdmissionStatus.StorageOverlap;
        return RhpAdmissionStatus.Success;
    }

    private static bool PrefixDisjoint(RhpNewFastHeapAccess a, RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token)
    {
        if (!Range(token, 24, 8))
            return false;
        return !Overlap(token, 24, (ulong)a.Allocator, 56) && !Overlap(token, 24, (ulong)a.Nodes, 48) && !Overlap(token, 24, (ulong)a.Reservations, 200) && !Overlap(token, 24, (ulong)a.Heap, 216) && !Overlap(token, 24, (ulong)a.Backing, 8192) && !Overlap(token, 24, (ulong)access, 104) && !Overlap(token, 24, (ulong)bump, 80) && !Overlap(token, 24, (ulong)control, ControlBytes) && !Overlap(token, 24, (ulong)records, ObservationBytes);
    }

    internal static bool TryReadPrefix(byte* image, ulong imageBytes, ulong token, ulong expectedToken, out NativeAotMethodTablePrefix prefix, out ulong word0, out ulong word1, out ulong word2)
    {
        prefix = default;
        word0 = 0;
        word1 = 0;
        word2 = 0;
        // Exact finite-token identity precedes every prefix read, including malformed cases.
        if (token == 0 || token != expectedToken || !Range(token, 24, 8) || !Image(image, imageBytes, out ulong size, out ulong table, out uint sections) || token < (ulong)image || token - (ulong)image > size || 24 > size - (token - (ulong)image))
            return false;
        ulong rva = token - (ulong)image;
        uint matches = 0;
        for (uint i = 0; i < sections; i++)
        {
            byte* section = image + table + i * 40;
            ulong length = Read32(section + 8), start = Read32(section + 12), raw = Read32(section + 16);
            if (length == 0 || rva < start || rva - start > length || 24 > length - (rva - start))
                continue;
            uint flags = Read32(section + 36);
            if ((flags & 0x40000000) == 0 || (flags & 0xA2000000) != 0 || rva - start > raw || 24 > raw - (rva - start))
                return false;
            matches++;
        }

        if (matches != 1)
            return false;
        byte* bytes = (byte*)token;
        word0 = Read64(bytes);
        word1 = Read64(bytes + 8);
        word2 = Read64(bytes + 16);
        prefix = new NativeAotMethodTablePrefix((uint)word0, (uint)(word0 >> 32), word1, (ushort)word2, (ushort)(word2 >> 16), (uint)(word2 >> 32));
        return true;
    }

    private static bool Image(byte* image, ulong capacity, out ulong size, out ulong table, out uint sections)
    {
        size = 0;
        table = 0;
        sections = 0;
        if (!Range((ulong)image, capacity, 1) || capacity < 64 || Read16(image) != 0x5A4D)
            return false;
        ulong nt = Read32(image + 60);
        if (nt < 64 || nt > capacity || 24 > capacity - nt || Read32(image + nt) != 0x00004550 || Read16(image + nt + 4) != 0x8664)
            return false;
        sections = Read16(image + nt + 6);
        ulong optional = nt + 24, optionalBytes = Read16(image + nt + 20);
        if (sections == 0 || sections > 8 || optionalBytes < 112 || optional > capacity || optionalBytes > capacity - optional || Read16(image + optional) != 0x20B)
            return false;
        size = Read32(image + optional + 56);
        ulong headers = Read32(image + optional + 60);
        table = optional + optionalBytes;
        ulong tableBytes = sections * 40UL;
        if (headers == 0 || headers > size || size > capacity || table > headers || tableBytes > headers - table)
            return false;
        for (uint i = 0; i < sections; i++)
        {
            byte* section = image + table + i * 40;
            ulong length = Read32(section + 8), start = Read32(section + 12);
            if (length == 0)
                continue;
            if (start < headers || start > size || length > size - start)
                return false;
            for (uint j = 0; j < i; j++)
            {
                byte* previous = image + table + j * 40;
                ulong otherLength = Read32(previous + 8), otherStart = Read32(previous + 12);
                if (otherLength != 0 && Overlap(start, length, otherStart, otherLength))
                    return false;
            }
        }

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RhpObjectObservation Observe(RhpNewFastResult value, ulong id, ulong route, ulong word0, ulong word1, ulong word2)
    {
        RhpObjectObservation result = new()
        {
            Id = id,
            Route = route,
            MethodTable = value.MethodTableToken,
            Prefix0 = word0,
            Prefix1 = word1,
            Prefix2 = word2,
            Flags = (uint)word0,
            BaseSize = value.BytesConsumed,
            Allocation = value.MappedAllocationStart,
            Reference = value.ObjectReference,
            End = value.MappedAllocationEndExclusive,
            UsedBefore = value.UsedBefore,
            UsedAfter = value.UsedAfter,
            Count = value.AllocationCount
        };
        byte* bytes = (byte*)result.Allocation;
        result.Sync = Read64(bytes);
        result.ObservedMt = Read64(bytes + 8);
        for (ulong i = 0; i < result.BaseSize; i++)
            if (i < 8 || i >= (route == 3 || route == 4 || route == 5 || route == 6 ? 20UL : 16UL))
            {
                result.ZeroOr |= bytes[i];
                result.ZeroBytes++;
            }

        return result;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool Prior(RhpObservationStorage* records, ulong count, ulong backing, bool references = false)
    {
        if (references && count > 6)
            return false;
        ulong used = 0;
        for (ulong i = 0; i < count; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            bool array = r.Route == 3 || r.Route == 4;
            bool text = r.Route == 5, stringArray = r.Route == 6;
            if (r.Id != i + 1 || r.BaseSize < 24 || r.BaseSize > 4096 - used || r.Allocation != backing + used || r.Reference != r.Allocation + 8 || (!references && !array && !text && !stringArray && r.Id <= 4 && (r.ScalarWidth != 8 || r.ScalarOffset != (r.Id == 1 ? 8UL : 16UL))) || (references && (r.Route != 7 || r.ScalarWidth != (r.Id <= 4 ? 8UL : 0UL) || (r.Id <= 4 ? r.ScalarOffset < 8 || (r.ScalarOffset & 7) != 0 : r.ScalarOffset != 0 || r.Scalar != 0))) || (array && (Read32((byte*)r.Allocation + 16) != r.Length || r.Length != (r.Id == 3 ? 3UL : r.Id == 5 ? 1UL : 0UL) || r.ScalarWidth != (r.Id == 3 ? 8UL : r.Id == 5 ? 1UL : 0UL) || r.ScalarOffset != (r.Id == 3 ? 24UL : r.Id == 5 ? 16UL : 0UL))) || (stringArray && (r.Id != 2 || r.Length != 1 || r.ScalarWidth != 1 || r.ScalarOffset != 16 || Read32((byte*)r.Allocation + 16) != 1 || r.BaseSize != 32)) || (text && (r.Id < 3 || r.Id > 7 || r.Length != (r.Id == 3 ? 0UL : r.Id == 4 ? 1UL : r.Id == 7 ? 2UL : 3UL) || Read32((byte*)r.Allocation + 16) != r.Length || r.ScalarWidth != 2 * r.Length || r.ScalarOffset != 12 || r.BaseSize != ((22 + 2 * r.Length + 7) & ~7UL))) || r.End != r.Allocation + r.BaseSize || Read64((byte*)r.Allocation) != 0 || Read64((byte*)r.Reference) != r.MethodTable || Untouched(r) != 0 || (r.ScalarWidth != 0 && (r.ScalarOffset > r.BaseSize - 8 || r.ScalarWidth > r.BaseSize - 8 - r.ScalarOffset || ReadScalar((byte*)r.Reference + r.ScalarOffset, r.ScalarWidth) != r.Scalar)))
                return false;
            used += r.BaseSize;
        }

        return true;
    }

    private static ulong Untouched(RhpObjectObservation r)
    {
        ulong combined = 0;
        byte* bytes = (byte*)r.Reference;
        for (ulong i = 8; i < r.BaseSize - 8; i++)
            if (!((r.Route == 3 || r.Route == 4 || r.Route == 5 || r.Route == 6) && i < 12) && (r.ScalarWidth == 0 || i < r.ScalarOffset || i >= r.ScalarOffset + r.ScalarWidth))
                combined |= bytes[i];
        return combined;
    }

    private static ulong ReadScalar(byte* bytes, ulong width)
    {
        ulong value = 0;
        for (ulong i = 0; i < width; i++)
            value |= (ulong)bytes[i] << (int)(8 * i);
        return value;
    }

    private static bool Zero(byte* bytes, ulong length)
    {
        for (ulong i = 0; i < length; i++)
            if (bytes[i] != 0)
                return false;
        return true;
    }

    internal static bool Range(ulong start, ulong length, ulong alignment) => start != 0 && length != 0 && (start & (alignment - 1)) == 0 && start <= ~0UL - length;
    private static bool Overlap(ulong start, ulong length, ulong other, ulong otherLength) => start < other + otherLength && other < start + length;
    internal static ulong Read64(byte* bytes)
    {
        ulong result = 0;
        for (int i = 0; i < 8; i++)
            result |= (ulong)bytes[i] << (8 * i);
        return result;
    }

    private static uint Read32(byte* bytes) => (uint)(bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24));
    private static ushort Read16(byte* bytes) => (ushort)(bytes[0] | (bytes[1] << 8));
}
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE

#if M04_CHECKED_REFERENCE_BARRIER_PROBE

#else

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#if M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE

#else

#if M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE

#else

#endif
#endif

#endif
#endif

#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#elif M04_CHECKED_REFERENCE_BARRIER_PROBE

#elif M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_STRING_ALLOCATION_PROBE

#if M04_STRING_ALLOCATION_BEFORE_BIND_PROBE

#else

#if M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE

#else

#if M04_STRING_ALLOCATION_EXHAUSTION_PROBE

#else

#endif
#endif
#endif

#endif

#if M04_SZ_ARRAY_ALLOCATION_PROBE

#if M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE

#else

#if M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE

#else

#endif
#endif

#if M04_STRING_ALLOCATION_PROBE

#else

#endif

#endif

#if M04_STRING_ALLOCATION_PROBE

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#endif

#if M04_CHECKED_REFERENCE_BARRIER_PROBE

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_SZ_ARRAY_ALLOCATION_PROBE

#endif
#if M04_STRING_ALLOCATION_PROBE

#endif

#pragma warning disable 649

#pragma warning restore 649
#endif
