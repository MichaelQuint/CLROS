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
    internal ulong Prepared, InProgress, Completed, ConstructorCount, CanaryAfter;
}

internal struct RhpObjectObservation
{
    internal ulong Id, Route, MethodTable, Prefix0, Prefix1, Prefix2, Flags, BaseSize;
    internal ulong Allocation, Reference, End, UsedBefore, UsedAfter, Count;
    internal ulong Sync, ObservedMt, ZeroBytes, ZeroOr;
    internal ulong ScalarOffset, Scalar, UntouchedBytes, UntouchedOr;
}

internal unsafe struct RhpObservationStorage
{
    internal ulong CanaryBefore;
    internal RhpObjectObservation Object0, Object1, Object2, Object3, Object4;
    internal RhpObjectObservation Object5, Object6, Object7, Object8, Object9;
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

internal static unsafe class RhpNewFastAllocationCore
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
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return new(ready);
        if (control->InProgress != 0) return new(RhpAdmissionStatus.Reentry);
        if (token == 0 || token != expectedToken) return new(RhpAdmissionStatus.UnknownToken);
        bool filler = control->Completed == 10 && id == 1 && route == 1;
        if (id == 0 || id > 10 || route != (id <= 4 ? 1UL : 2UL) ||
            (!filler && id != control->Completed + 1)) return new(RhpAdmissionStatus.InvalidFixture);
        if (!PrefixDisjoint(*access, access, bump, control, records, token) ||
            !TryReadPrefix((byte*)control->Image, control->ImageBytes, token, expectedToken,
                out NativeAotMethodTablePrefix prefix, out ulong word0, out ulong word1, out ulong word2))
            return new(RhpAdmissionStatus.InvalidPrefixExtent);
        if (!Prior(records, control->Completed, (ulong)access->Backing)) return new(RhpAdmissionStatus.PriorObjectChanged);
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        byte* bumpBefore = stackalloc byte[80];
        for (ulong i = 0; i < 80; i++) bumpBefore[i] = ((byte*)bump)[i];
        control->InProgress = 1;
        RhpNewFastResult result = GuestTestRhpNewFast.TryAllocate(*access, bump, 80,
            NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian, prefix, token);
        if (!result.AllocationSucceeded)
        {
            control->InProgress = 0;
            if (!RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt))
                return new(RhpAdmissionStatus.ObservationRejected, result);
            return new(RhpAdmissionStatus.AllocationRejected, result);
        }
        // No reference conversion or constructor can precede this actual byte readback.
        RhpObjectObservation observation = Observe(result, id, route, word0, word1, word2);
        if (Read64((byte*)token) != word0 || Read64((byte*)token + 8) != word1 ||
            Read64((byte*)token + 16) != word2)
            return new(RhpAdmissionStatus.PrefixChanged, result, observation);
        if (observation.Sync != 0 || observation.ObservedMt != token || observation.ZeroOr != 0 ||
            observation.Reference != observation.Allocation + 8 ||
            observation.End != observation.Allocation + prefix.BaseSize ||
            observation.UsedAfter != observation.UsedBefore + prefix.BaseSize ||
            !Prior(records, control->Completed, (ulong)access->Backing))
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
        record->UntouchedBytes = record->BaseSize - 16 - (id <= 4 ? 8UL : 0UL);
        record->UntouchedOr = Untouched(*record);
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
            if (i < 8 || i >= 16) { result.ZeroOr |= bytes[i]; result.ZeroBytes++; }
        return result;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool Prior(RhpObservationStorage* records, ulong count, ulong backing)
    {
        ulong used = 0;
        for (ulong i = 0; i < count; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            if (r.Id != i + 1 || r.BaseSize < 24 || r.BaseSize > 4096 - used ||
                r.Allocation != backing + used || r.Reference != r.Allocation + 8 ||
                (r.Id <= 4 && r.ScalarOffset != (r.Id == 1 ? 8UL : 16UL)) ||
                r.End != r.Allocation + r.BaseSize || Read64((byte*)r.Allocation) != 0 ||
                Read64((byte*)r.Reference) != r.MethodTable || Untouched(r) != 0 ||
                (r.Id <= 4 && (r.ScalarOffset > r.BaseSize - 16 ||
                    Read64((byte*)r.Reference + r.ScalarOffset) != r.Scalar))) return false;
            used += r.BaseSize;
        }
        return true;
    }

    private static ulong Untouched(RhpObjectObservation r)
    {
        ulong combined = 0;
        byte* bytes = (byte*)r.Reference;
        for (ulong i = 8; i < r.BaseSize - 8; i++)
            if (r.Id > 4 || i < r.ScalarOffset || i >= r.ScalarOffset + 8) combined |= bytes[i];
        return combined;
    }

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
internal static unsafe class RhpNewFastAllocation
{
    private static RhpNewFastHeapAccess s_access;
    private static RhpNewFastBumpState s_bump;
    private static RhpAllocationControl s_control;
    private static RhpObservationStorage s_records;

    internal static void Run(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
        T(0); End();
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
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static ulong Allocate(ulong token)
    {
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
    }

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
    { if (status == RhpNewFastStatus.NotInitialized) T(27); else T(28); }
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
        T(47); Decimal(r.Id); T(48); if (r.Route == 1) T(49); else T(50);
        T(51); Hex(r.MethodTable); T(52); Hex(r.Prefix0); T(53); Hex(r.Prefix1); T(54); Hex(r.Prefix2);
        T(55); Hex(r.Flags); T(4); Decimal(r.BaseSize); T(56); Hex(r.Allocation);
        T(57); Hex(r.Reference); T(58); Hex(r.End); T(2); Decimal(r.UsedBefore);
        T(3); Decimal(r.UsedAfter); T(20); Decimal(r.Count); T(59); Hex(r.Sync);
        T(60); Hex(r.ObservedMt); T(61); Decimal(r.ZeroBytes); T(62); Hex(r.ZeroOr); End();
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
        for (int index = 0; ; index++)
        {
            byte value = Literal(token, index); if (value == 0) return; Write(value);
        }
    }
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
