using OS.Kernel.Runtime;

namespace OS.Runtime;

// These offsets come from actual C# field-address roots in the enabled image.
// The bounded numeric checks below do not authenticate a compiler layout by themselves.
internal struct CheckedReferenceGeometry
{
    internal ulong CellReference, CellTag, CellPayload;
    internal ulong PairFirst, PairLast, PairTag, BlockPayload;
}

#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE
// Only this private enabled route produces genuine references. The ordinary
// scalar callback and managed bulk entry share a POD-only admission core.
internal static unsafe partial class RhpNewFastAllocation
{
    private static CheckedReferenceState s_referenceState;

    [System.Runtime.RuntimeExport("CLROS_CheckedReferenceBarrier_Apply")]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static uint ReferenceCallback(ulong operation, ulong destination, ulong operand, ulong bytes) =>
        ApplyReferences(operation, destination, operand, bytes);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static uint ApplyReferences(ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        fixed (CheckedReferenceState* state = &s_referenceState)
            return RhpNewFastAllocationCore.ApplyReference(a, b, c, r, state, operation, destination, operand, bytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void MoveReferences(ref byte destination, ref byte source, nuint bytes)
    {
        fixed (byte* d = &destination)
        fixed (byte* s = &source)
            if (ApplyReferences(4, (ulong)d, (ulong)s, (ulong)bytes) != 1)
                while (true) System.RuntimeFailFast.Halt();
    }

    private static ulong AllocateReferenceRole(ulong token)
    {
        ulong id = s_control.Completed + 1;
        ulong expected = id <= 2 ? TypePlain() : id <= 4 ? TypeReferenceCell() : id <= 6 ? TypeReferenceBlock() : 0;
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            RhpAllocationAttempt attempt = RhpNewFastAllocationCore.AllocateReferences(a, b, c, r, token, expected, id);
            if (!attempt.Succeeded || attempt.Leaf.ObjectReference == 0)
                RejectReferences(1);
            return attempt.Leaf.ObjectReference;
        }
    }

    private static void CompleteReferenceRole(ulong id, object value, ulong scalarAddress, ulong scalar)
    {
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        {
            if (RhpNewFastAllocationCore.CompleteReferenceAllocation(a, b, c, r, id, Bits(value), scalarAddress, scalar)
                != RhpAdmissionStatus.Success) RejectReferences(2);
            ReferenceObjectLine((&r->Object0)[id - 1]);
        }
    }

    private static void RunReferences(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
        R(0); End();
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        fixed (CheckedReferenceState* state = &s_referenceState)
        {
            if (RhpNewFastAllocationCore.Prepare(original, a, b, c, r, image, imageBytes) != RhpAdmissionStatus.Success)
                RejectReferences(3);
            PreparationLine();
            RhpNewFastResult bound = RhpNewFastAllocationCore.Bind(a, b, c, r);
            if (!bound.Succeeded) RejectReferences(4);
            BindLine(bound);
            RhpPlainFixture valueA = NewPlain(RhpNewFastAllocationCore.ReferenceRoleTag(1));
            CompleteReferenceRole(1, valueA, Address(ref valueA.Value), ReadPlain(valueA));
            RhpPlainFixture valueB = NewPlain(RhpNewFastAllocationCore.ReferenceRoleTag(2));
            CompleteReferenceRole(2, valueB, Address(ref valueB.Value), ReadPlain(valueB));
            ReferenceCell source = NewReferenceCell(RhpNewFastAllocationCore.ReferenceRoleTag(3));
            CompleteReferenceRole(3, source, Address(ref source.Tag), ReadReferenceCellTag(source));
            ReferenceCell destination = NewReferenceCell(RhpNewFastAllocationCore.ReferenceRoleTag(4));
            CompleteReferenceRole(4, destination, Address(ref destination.Tag), ReadReferenceCellTag(destination));
            ReferenceBlockHolder sourceBlock = NewReferenceBlock();
            CompleteReferenceRole(5, sourceBlock, 0, 0);
            ReferenceBlockHolder destinationBlock = NewReferenceBlock();
            CompleteReferenceRole(6, destinationBlock, 0, 0);

            // Every field relation is derived from genuine managed byrefs in this
            // image; static admission still requires a separate native/PE join.
            ref ReferencePair pair = ref AddressReferencePair(source);
            ref ReferenceBlock block = ref AddressReferenceBlock(sourceBlock);
            CheckedReferenceGeometry geometry = new()
            {
                CellReference = Address(ref source.Reference) - Bits(source),
                CellTag = Address(ref source.Tag) - Bits(source),
                CellPayload = Address(ref pair) - Bits(source),
                PairFirst = Address(ref pair.First) - Address(ref pair),
                PairLast = Address(ref pair.Last) - Address(ref pair),
                PairTag = Address(ref pair.Tag) - Address(ref pair),
                BlockPayload = Address(ref block) - Bits(sourceBlock)
            };
            if (Address(ref destination.Reference) - Bits(destination) != geometry.CellReference ||
                Address(ref destination.Tag) - Bits(destination) != geometry.CellTag ||
                Address(ref AddressReferencePair(destination)) - Bits(destination) != geometry.CellPayload ||
                Address(ref AddressReferenceBlock(destinationBlock)) - Bits(destinationBlock) != geometry.BlockPayload)
                RejectReferences(5);
            for (ulong i = 0; i < 8; i++)
            {
                ref ReferencePair part = ref AddressReferenceBlockPair(ref block, i);
                ref ReferencePair target = ref AddressReferenceBlockPair(ref AddressReferenceBlock(destinationBlock), i);
                if (Address(ref part) - Address(ref block) != i * 24 ||
                    Address(ref target) - Address(ref AddressReferenceBlock(destinationBlock)) != i * 24 ||
                    Address(ref part.First) - Address(ref part) != geometry.PairFirst ||
                    Address(ref part.Last) - Address(ref part) != geometry.PairLast ||
                    Address(ref part.Tag) - Address(ref part) != geometry.PairTag) RejectReferences(6);
            }
            if (!RhpNewFastAllocationCore.PrepareReferences(a, b, c, r, state, geometry)) RejectReferences(7);
            ReferenceGeometryLine(state);
            pair.Tag = RhpNewFastAllocationCore.ReferencePairTag;
            for (ulong i = 0; i < 8; i++) AddressReferenceBlockPair(ref block, i).Tag = RhpNewFastAllocationCore.ReferenceBlockTag(i);
            if (!RhpNewFastAllocationCore.CompleteReferenceScalars(a, b, c, r, state)) RejectReferences(8);

            // Each compiler-selected checked store must return with its original
            // cookie before another operation can consume that result.
            for (ulong i = 0; i < 18; i++)
            {
                object value = i % 3 == 2 ? null : i % 3 == 0 ? valueA : valueB;
                ref ReferencePair part = ref (i < 2 ? ref pair : ref AddressReferenceBlockPair(ref block, (i - 2) / 2));
                ref object slot = ref ((i & 1) == 0 ? ref part.First : ref part.Last);
                if (StoreReferenceUnknown(ref slot, value, RhpNewFastAllocationCore.ReferenceCookie) != RhpNewFastAllocationCore.ReferenceCookie ||
                    state->Calls != i + 1 || Bits(ReadReferenceSlot(ref slot)) != Bits(value)) RejectReferences(9);
            }
            ReferenceOperationLine(2, state->Calls, RhpNewFastAllocationCore.ReferenceCookie);
            ulong fieldCookie = StoreReferenceField(destination, valueA, RhpNewFastAllocationCore.ReferenceCookie);
            if (fieldCookie != (RhpNewFastAllocationCore.ReferenceCookie ^ RhpNewFastAllocationCore.ReferenceRoleTag(4)) ||
                Bits(ReadReferenceField(destination)) != Bits(valueA) || state->Calls != 19) RejectReferences(10);
            ReferenceOperationLine(1, state->Calls, fieldCookie);
            ulong checkedCookie = StoreReferenceUnknown(ref source.Reference, valueB, RhpNewFastAllocationCore.ReferenceCookie);
            if (checkedCookie != RhpNewFastAllocationCore.ReferenceCookie ||
                Bits(ReadReferenceField(source)) != Bits(valueB) || state->Calls != 20) RejectReferences(11);
            ReferenceOperationLine(2, state->Calls, checkedCookie);
            ulong pairCookie = CopyReferencePair(ref AddressReferencePair(destination), ref pair, RhpNewFastAllocationCore.ReferenceCookie);
            if (!RhpNewFastAllocationCore.CompleteReferencePair(a, b, c, r, state, pairCookie) ||
                Bits(ReadReferenceFirst(ref AddressReferencePair(destination))) != Bits(valueA) ||
                Bits(ReadReferenceLast(ref AddressReferencePair(destination))) != Bits(valueB)) RejectReferences(12);
            ReferenceOperationLine(3, state->Calls, pairCookie);
            ulong blockCookie = CopyReferenceBlock(ref AddressReferenceBlock(destinationBlock), ref block, RhpNewFastAllocationCore.ReferenceCookie);
            if (!RhpNewFastAllocationCore.CompleteReferences(a, b, c, r, state, blockCookie)) RejectReferences(13);
            for (ulong i = 0; i < 8; i++)
            {
                ref ReferencePair part = ref AddressReferenceBlockPair(ref AddressReferenceBlock(destinationBlock), i);
                ulong firstIndex = 2 + i * 2, lastIndex = firstIndex + 1;
                ulong first = firstIndex % 3 == 2 ? 0 : firstIndex % 3 == 0 ? Bits(valueA) : Bits(valueB);
                ulong last = lastIndex % 3 == 2 ? 0 : lastIndex % 3 == 0 ? Bits(valueA) : Bits(valueB);
                if (Bits(ReadReferenceFirst(ref part)) != first || Bits(ReadReferenceLast(ref part)) != last ||
                    ReadReferenceTag(ref part) != RhpNewFastAllocationCore.ReferenceBlockTag(i)) RejectReferences(14);
            }
            ReferenceOperationLine(4, state->Calls, blockCookie);
            ManagedHeapPageRangeResult retained = RhpNewFastAllocationCore.Retained(*a);
            if (!retained.IsReady || retained.ReadBytes != 4096 || retained.ContentsAreZeroNow ||
                c->Completed != 6 || c->ConstructorCount != 6 || b->AllocationCount != 6)
                RejectReferences(15);
            RetainLine(retained);
            R(1); Decimal(b->UsedBytes); R(2); End(); R(3); End();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong Address<T>(ref T value)
    { fixed (byte* p = &System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref value)) return (ulong)p; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeReferenceCell() => (ulong)Internal.Runtime.MethodTable.Of<ReferenceCell>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TypeReferenceBlock() => (ulong)Internal.Runtime.MethodTable.Of<ReferenceBlockHolder>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ReferenceCell NewReferenceCell(ulong tag) => new ReferenceCell(tag);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ReferenceBlockHolder NewReferenceBlock() => new ReferenceBlockHolder();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadReferenceCellTag(ReferenceCell value) => value.Tag;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ref ReferencePair AddressReferencePair(ReferenceCell value) => ref value.Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ref ReferenceBlock AddressReferenceBlock(ReferenceBlockHolder value) => ref value.Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ref ReferencePair AddressReferenceBlockPair(ref ReferenceBlock value, ulong index)
    {
        switch (index)
        {
            case 0: return ref value.P0; case 1: return ref value.P1;
            case 2: return ref value.P2; case 3: return ref value.P3;
            case 4: return ref value.P4; case 5: return ref value.P5;
            case 6: return ref value.P6; case 7: return ref value.P7;
            default: while (true) System.RuntimeFailFast.Halt();
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong StoreReferenceField(ReferenceCell destination, object value, ulong cookie)
    { destination.Reference = value; return cookie ^ destination.Tag; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong StoreReferenceUnknown(ref object destination, object value, ulong cookie)
    { destination = value; return cookie; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong CopyReferencePair(ref ReferencePair destination, ref ReferencePair source, ulong cookie)
    { destination = source; return cookie ^ destination.Tag; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong CopyReferenceBlock(ref ReferenceBlock destination, ref ReferenceBlock source, ulong cookie)
    { destination = source; return cookie ^ destination.P7.Tag; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object ReadReferenceSlot(ref object value) => value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object ReadReferenceField(ReferenceCell value) => value.Reference;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object ReadReferenceFirst(ref ReferencePair value) => value.First;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object ReadReferenceLast(ref ReferencePair value) => value.Last;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadReferenceTag(ref ReferencePair value) => value.Tag;

    private static void RejectReferences(ulong checkpoint)
    { R(4); Decimal(checkpoint); End(); while (true) System.RuntimeFailFast.Halt(); }

    private static void R(int token)
    {
        for (int index = 0; ; index++)
        {
            byte value = ReferenceAscii(token, index);
            if (value == 0) return;
            Write(value);
        }
    }
    private static byte ReferenceAscii(int token, int index) => token switch
    {
        0 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 83, 6 => 84, 7 => 65, 8 => 82, 9 => 84, 10 => 32, 11 => 109, 12 => 48, 13 => 52, 14 => 46, 15 => 48, 16 => 49, 17 => 48, 18 => 46, 19 => 114, 20 => 101, 21 => 102, 22 => 101, 23 => 114, 24 => 101, 25 => 110, 26 => 99, 27 => 101, 28 => 115, _ => 0 },
        1 => index switch { 0 => 67, 1 => 76, 2 => 82, 3 => 79, 4 => 83, 5 => 95, 6 => 77, 7 => 48, 8 => 52, 9 => 95, 10 => 48, 11 => 49, 12 => 48, 13 => 95, 14 => 78, 15 => 65, 16 => 84, 17 => 73, 18 => 86, 19 => 69, 20 => 95, 21 => 82, 22 => 69, 23 => 65, 24 => 68, 25 => 89, 26 => 32, 27 => 97, 28 => 108, 29 => 108, 30 => 111, 31 => 99, 32 => 97, 33 => 116, 34 => 105, 35 => 111, 36 => 110, 37 => 115, 38 => 61, 39 => 54, 40 => 32, 41 => 117, 42 => 115, 43 => 101, 44 => 100, 45 => 66, 46 => 121, 47 => 116, 48 => 101, 49 => 115, 50 => 61, _ => 0 },
        2 => index switch { 0 => 32, 1 => 99, 2 => 97, 3 => 108, 4 => 108, 5 => 115, 6 => 61, 7 => 50, 8 => 51, 9 => 32, 10 => 105, 11 => 110, 12 => 105, 13 => 116, 14 => 105, 15 => 97, 16 => 108, 17 => 83, 18 => 116, 19 => 111, 20 => 114, 21 => 101, 22 => 115, 23 => 61, 24 => 49, 25 => 56, 26 => 32, 27 => 102, 28 => 105, 29 => 101, 30 => 108, 31 => 100, 32 => 83, 33 => 116, 34 => 111, 35 => 114, 36 => 101, 37 => 115, 38 => 61, 39 => 49, 40 => 32, 41 => 99, 42 => 104, 43 => 101, 44 => 99, 45 => 107, 46 => 101, 47 => 100, 48 => 83, 49 => 116, 50 => 111, 51 => 114, 52 => 101, 53 => 115, 54 => 61, 55 => 49, 56 => 32, 57 => 112, 58 => 97, 59 => 105, 60 => 114, 61 => 72, 62 => 101, 63 => 108, 64 => 112, 65 => 101, 66 => 114, 67 => 115, 68 => 61, 69 => 50, 70 => 32, 71 => 98, 72 => 117, 73 => 108, 74 => 107, 75 => 67, 76 => 97, 77 => 108, 78 => 108, 79 => 115, 80 => 61, 81 => 49, 82 => 32, 83 => 112, 84 => 97, 85 => 105, 86 => 114, 87 => 67, 88 => 111, 89 => 109, 90 => 112, 91 => 108, 92 => 101, 93 => 116, 94 => 101, 95 => 61, 96 => 116, 97 => 114, 98 => 117, 99 => 101, 100 => 32, 101 => 98, 102 => 108, 103 => 111, 104 => 99, 105 => 107, 106 => 67, 107 => 111, 108 => 109, 109 => 112, 110 => 108, 111 => 101, 112 => 116, 113 => 101, 114 => 61, 115 => 116, 116 => 114, 117 => 117, 118 => 101, 119 => 32, 120 => 101, 121 => 120, 122 => 99, 123 => 108, 124 => 117, 125 => 115, 126 => 105, 127 => 118, 128 => 101, 129 => 78, 130 => 111, 131 => 110, 132 => 67, 133 => 111, 134 => 108, 135 => 108, 136 => 101, 137 => 99, 138 => 116, 139 => 105, 140 => 110, 141 => 103, 142 => 61, 143 => 116, 144 => 114, 145 => 117, 146 => 101, 147 => 32, 148 => 103, 149 => 97, 150 => 116, 151 => 101, 152 => 52, 153 => 61, 154 => 70, 155 => 97, 156 => 105, 157 => 108, _ => 0 },
        3 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 80, 6 => 65, 7 => 83, 8 => 83, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 49, 16 => 48, 17 => 46, 18 => 114, 19 => 101, 20 => 102, 21 => 101, 22 => 114, 23 => 101, 24 => 110, 25 => 99, 26 => 101, 27 => 115, _ => 0 },
        4 => index switch { 0 => 84, 1 => 69, 2 => 83, 3 => 84, 4 => 95, 5 => 70, 6 => 65, 7 => 73, 8 => 76, 9 => 32, 10 => 109, 11 => 48, 12 => 52, 13 => 46, 14 => 48, 15 => 49, 16 => 48, 17 => 46, 18 => 114, 19 => 101, 20 => 102, 21 => 101, 22 => 114, 23 => 101, 24 => 110, 25 => 99, 26 => 101, 27 => 115, 28 => 32, 29 => 97, 30 => 100, 31 => 109, 32 => 105, 33 => 115, 34 => 115, 35 => 105, 36 => 111, 37 => 110, 38 => 95, 39 => 114, 40 => 101, 41 => 106, 42 => 101, 43 => 99, 44 => 116, 45 => 101, 46 => 100, 47 => 32, 48 => 99, 49 => 104, 50 => 101, 51 => 99, 52 => 107, 53 => 112, 54 => 111, 55 => 105, 56 => 110, 57 => 116, 58 => 61, _ => 0 },
        5 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 79, 15 => 66, 16 => 74, 17 => 69, 18 => 67, 19 => 84, 20 => 32, 21 => 105, 22 => 100, 23 => 61, _ => 0 },
        6 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 79, 8 => 102, 9 => 102, 10 => 115, 11 => 101, 12 => 116, 13 => 61, _ => 0 },
        7 => index switch { 0 => 32, 1 => 115, 2 => 99, 3 => 97, 4 => 108, 5 => 97, 6 => 114, 7 => 61, _ => 0 },
        8 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 71, 15 => 69, 16 => 79, 17 => 77, 18 => 69, 19 => 84, 20 => 82, 21 => 89, 22 => 32, 23 => 115, 24 => 116, 25 => 97, 26 => 116, 27 => 101, 28 => 61, _ => 0 },
        9 => index switch { 0 => 32, 1 => 115, 2 => 116, 3 => 97, 4 => 116, 5 => 101, 6 => 66, 7 => 121, 8 => 116, 9 => 101, 10 => 115, 11 => 61, _ => 0 },
        10 => index switch { 0 => 32, 1 => 99, 2 => 101, 3 => 108, 4 => 108, 5 => 82, 6 => 101, 7 => 102, 8 => 101, 9 => 114, 10 => 101, 11 => 110, 12 => 99, 13 => 101, 14 => 61, _ => 0 },
        11 => index switch { 0 => 32, 1 => 99, 2 => 101, 3 => 108, 4 => 108, 5 => 84, 6 => 97, 7 => 103, 8 => 61, _ => 0 },
        12 => index switch { 0 => 32, 1 => 99, 2 => 101, 3 => 108, 4 => 108, 5 => 80, 6 => 97, 7 => 121, 8 => 108, 9 => 111, 10 => 97, 11 => 100, 12 => 61, _ => 0 },
        13 => index switch { 0 => 32, 1 => 112, 2 => 97, 3 => 105, 4 => 114, 5 => 70, 6 => 105, 7 => 114, 8 => 115, 9 => 116, 10 => 61, _ => 0 },
        14 => index switch { 0 => 32, 1 => 112, 2 => 97, 3 => 105, 4 => 114, 5 => 76, 6 => 97, 7 => 115, 8 => 116, 9 => 61, _ => 0 },
        15 => index switch { 0 => 32, 1 => 112, 2 => 97, 3 => 105, 4 => 114, 5 => 84, 6 => 97, 7 => 103, 8 => 61, _ => 0 },
        16 => index switch { 0 => 32, 1 => 98, 2 => 108, 3 => 111, 4 => 99, 5 => 107, 6 => 80, 7 => 97, 8 => 121, 9 => 108, 10 => 111, 11 => 97, 12 => 100, 13 => 61, _ => 0 },
        17 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 79, 15 => 80, 16 => 69, 17 => 82, 18 => 65, 19 => 84, 20 => 73, 21 => 79, 22 => 78, 23 => 32, 24 => 107, 25 => 105, 26 => 110, 27 => 100, 28 => 61, _ => 0 },
        18 => index switch { 0 => 32, 1 => 99, 2 => 97, 3 => 108, 4 => 108, 5 => 115, 6 => 61, _ => 0 },
        19 => index switch { 0 => 32, 1 => 99, 2 => 111, 3 => 111, 4 => 107, 5 => 105, 6 => 101, 7 => 61, _ => 0 },
        20 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 80, 15 => 82, 16 => 69, 17 => 80, 18 => 32, 19 => 105, 20 => 109, 21 => 97, 22 => 103, 23 => 101, 24 => 61, _ => 0 },
        21 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 66, 15 => 73, 16 => 78, 17 => 68, 18 => 32, 19 => 105, 20 => 109, 21 => 97, 22 => 103, 23 => 101, 24 => 61, _ => 0 },
        22 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 82, 15 => 69, 16 => 84, 17 => 65, 18 => 73, 19 => 78, 20 => 32, 21 => 97, 22 => 108, 23 => 108, 24 => 111, 25 => 99, 26 => 97, 27 => 116, 28 => 111, 29 => 114, 30 => 61, _ => 0 },
        _ => 0
    };

    // Numeric observations are serialized only outside the returning callback graph.
    private static void ReferenceObjectLine(RhpObjectObservation r)
    {
        R(5); Decimal(r.Id); T(51); Hex(r.MethodTable); T(52); Hex(r.Prefix0); T(53); Hex(r.Prefix1); T(54); Hex(r.Prefix2);
        T(55); Hex(r.Flags); T(4); Decimal(r.BaseSize); T(56); Hex(r.Allocation); T(57); Hex(r.Reference); T(58); Hex(r.End);
        T(2); Decimal(r.UsedBefore); T(3); Decimal(r.UsedAfter); T(20); Decimal(r.Count);
        T(59); Hex(r.Sync); T(60); Hex(r.ObservedMt); T(61); Decimal(r.ZeroBytes); T(62); Hex(r.ZeroOr);
        R(6); Decimal(r.ScalarOffset); R(7); Hex(r.Scalar); End();
    }
    private static void ReferenceGeometryLine(CheckedReferenceState* state)
    {
        R(8); Hex((ulong)state); R(9); Decimal((ulong)sizeof(CheckedReferenceState));
        R(10); Decimal(state->Geometry.CellReference); R(11); Decimal(state->Geometry.CellTag);
        R(12); Decimal(state->Geometry.CellPayload); R(13); Decimal(state->Geometry.PairFirst);
        R(14); Decimal(state->Geometry.PairLast); R(15); Decimal(state->Geometry.PairTag);
        R(16); Decimal(state->Geometry.BlockPayload); End();
    }
    private static void ReferenceOperationLine(ulong operation, ulong calls, ulong cookie)
    { R(17); Decimal(operation); R(18); Decimal(calls); R(19); Hex(cookie); End(); }
}

#pragma warning disable CS0649
internal sealed class ReferenceCell
{
    internal object Reference;
    internal ulong Tag;
    internal ReferencePair Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal ReferenceCell(ulong tag) { RhpNewFastAllocation.ConstructorEntered(); Tag = tag; }
}
internal struct ReferencePair { internal object First; internal ulong Tag; internal object Last; }
internal struct ReferenceBlock
{
    internal ReferencePair P0, P1, P2, P3, P4, P5, P6, P7;
}
internal sealed class ReferenceBlockHolder
{
    internal ReferenceBlock Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal ReferenceBlockHolder() { RhpNewFastAllocation.ConstructorEntered(); }
}
#pragma warning restore CS0649
#endif

internal unsafe struct CheckedReferenceState
{
    internal ulong CanaryBefore, Self, Access, Bump, Control, Records;
    internal ulong Armed, Busy, Stage, InitializedSlots, Calls;
    internal CheckedReferenceGeometry Geometry;
    internal RhpNewFastHeapAccess FrozenAccess;
    internal RhpNewFastBumpState FrozenBump;
    internal RhpAllocationControl FrozenControl;
    internal ulong CanaryAfter;
}

internal static unsafe partial class RhpNewFastAllocationCore
{
    internal const ulong ReferenceCookie = 0x179BCDE02468ACE0;
    internal const ulong ReferencePairTag = 0x1928374655AABBCC;
    internal static ulong ReferenceBlockTag(ulong index) => 0x3141592653589700 + index;
    internal static ulong ReferenceRoleTag(ulong id) => 0x1111222233334400 + id;

    // Six real allocations still enter the original AllocateCore and its sole host
    // allocator commit. Route 7 is finite and does not alter any older profile.
    internal static RhpAllocationAttempt AllocateReferences(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong token, ulong expectedToken, ulong id) =>
        AllocateCore(access, bump, control, records, token, expectedToken, id, 7, false, 0, 0, false, true);

    internal static RhpAdmissionStatus CompleteReferenceAllocation(RhpNewFastHeapAccess* access,
        RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records,
        ulong id, ulong reference, ulong scalarAddress, ulong typedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success) return ready;
        if (control->InProgress != 0) return RhpAdmissionStatus.Reentry;
        if (id == 0 || id > 6 || id != control->Completed + 1) return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        ulong width = id <= 4 ? 8UL : 0;
        if (r->Id != id || r->Route != 7 || r->Reference != reference || reference == 0 ||
            r->UsedBefore > 4096 || r->BaseSize < 24 || r->BaseSize > 4096 - r->UsedBefore ||
            r->Allocation != (ulong)access->Backing + r->UsedBefore || reference != r->Allocation + 8 ||
            r->End != r->Allocation + r->BaseSize || r->UsedAfter != r->UsedBefore + r->BaseSize ||
            r->Count != id || bump->AllocationCount != id || bump->UsedBytes != r->UsedAfter ||
            (width == 0 && (scalarAddress != 0 || typedScalar != 0)) ||
            (width != 0 && (typedScalar != ReferenceRoleTag(id) || scalarAddress < reference + 8 ||
                scalarAddress > r->End || 8 > r->End - scalarAddress || (scalarAddress & 7) != 0 ||
                Read64((byte*)scalarAddress) != typedScalar))) return RhpAdmissionStatus.ObservationRejected;
        // Record only an observed scalar field; untouched payload/padding must remain zero.
        RhpObjectObservation complete = *r;
        complete.ScalarOffset = width == 0 ? 0 : scalarAddress - reference;
        complete.ScalarWidth = width; complete.Scalar = typedScalar;
        complete.UntouchedBytes = r->BaseSize - 16 - width;
        complete.UntouchedOr = Untouched(complete);
        if (complete.UntouchedOr != 0 || !Prior(records, id - 1, (ulong)access->Backing, true))
            return RhpAdmissionStatus.PriorObjectChanged;
        *r = complete;
        control->Completed = id;
        return RhpAdmissionStatus.Success;
    }

    // Arm preparation observes all six completed allocations. It does not accept
    // arbitrary external windows or turn supplied POD values into live references.
    internal static bool PrepareReferences(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state,
        CheckedReferenceGeometry geometry)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success || control->InProgress != 0 ||
            !ReferenceStorage(access, bump, control, records, state) || !Zero((byte*)state, (ulong)sizeof(CheckedReferenceState)) ||
            control->Completed != 6 || control->ConstructorCount != 6 || bump->AllocationCount != 6 ||
            !Prior(records, 6, (ulong)access->Backing, true) || !ReferenceGeometry(records, geometry)) return false;
        *state = new CheckedReferenceState
        {
            CanaryBefore = Canary, CanaryAfter = Canary, Self = (ulong)state,
            Access = (ulong)access, Bump = (ulong)bump, Control = (ulong)control, Records = (ulong)records,
            Stage = 1, Geometry = geometry, FrozenAccess = *access, FrozenBump = *bump, FrozenControl = *control
        };
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        return true;
    }

    // Only the genuine scalar initialization interval may change these nine Tag
    // words before reference helpers are armed. It is not barrier evidence.
    internal static bool CompleteReferenceScalars(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Stage != 1 || state->Armed != 0 ||
            state->Calls != 0 || state->InitializedSlots != 0) return false;
        for (ulong i = 0; i < 4096; i++)
            if (access->Backing[i] != ScalarSetupByte(records, state, (ulong)access->Backing, i)) return false;
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        state->Stage = 2; state->Armed = 1;
        return true;
    }

    // No writes occur before all preconditions pass. Once the leaf commits, a
    // failed post-readback is terminal (Busy remains set), never a rollback claim.
    internal static uint ApplyReference(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state,
        ulong operation, ulong destination, ulong operand, ulong byteCount)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Armed != 1 ||
            !ReferencePage(access, records) || !ExpectedReferenceCall(records, state,
                operation, destination, operand, byteCount)) return 0;
        ulong page = (ulong)access->Backing;
        if (!InReferencePage(page, destination, byteCount)) return 0;
        bool move = operation == 3 || operation == 4;
        if (move && !InReferencePage(page, operand, byteCount)) return 0;
        if (operation == 3 && (destination > ~0UL - 8 || operand > ~0UL - 8)) return 0;
        if (move)
        {
            if (operation == 3 && !ReferenceValue(records, Read64((byte*)operand))) return 0;
            if (operation == 4 && !ReferenceBlock(records, state, operand)) return 0;
        }
        else if (!ReferenceValue(records, operand)) return 0;
        state->Busy = 1;
        CheckedReferenceBulkResult result = move
            ? CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryMove(*access,
                CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage, operand - page, destination - page, byteCount)
            : CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryStore(*access,
                CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage, destination - page, operand);
        if (!result.Succeeded || result.BytesWritten != byteCount ||
            Ready(access, bump, control, records) != RhpAdmissionStatus.Success ||
            !ReferenceFrozenState(bump, control, state)) return 0;
        ulong destinationOffset = destination - page, sourceOffset = move ? operand - page : 0;
        for (ulong i = 0; i < 4096; i++)
        {
            byte expected = records->BeforeAttempt[i];
            if (i >= destinationOffset && i - destinationOffset < byteCount)
                expected = move ? records->BeforeAttempt[sourceOffset + i - destinationOffset]
                    : (byte)(operand >> (int)(8 * (i - destinationOffset)));
            if (access->Backing[i] != expected) return 0;
        }
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        state->Calls++;
        if (state->Stage == 2)
        {
            state->InitializedSlots++;
            if (state->InitializedSlots == 18) state->Stage = 3;
        }
        else state->Stage++;
        state->Busy = 0;
        return 1;
    }

    // CopyPair returns after two independently committed helpers and its scalar
    // movsq. Only this exact pending Tag store is admitted at outer completion.
    internal static bool CompleteReferencePair(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong returnedCookie)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Armed != 1 ||
            state->Stage != 7 || state->Calls != 22 || returnedCookie != (ReferenceCookie ^ ReferencePairTag)) return false;
        ulong tag = records->Object3.Reference + state->Geometry.CellPayload + state->Geometry.PairTag;
        ulong offset = tag - (ulong)access->Backing;
        for (ulong i = 0; i < 4096; i++)
        {
            byte expected = i >= offset && i - offset < 8
                ? (byte)(ReferencePairTag >> (int)(8 * (i - offset))) : records->BeforeAttempt[i];
            if (access->Backing[i] != expected) return false;
        }
        for (ulong i = 0; i < 4096; i++) records->BeforeAttempt[i] = access->Backing[i];
        state->Stage = 8;
        return true;
    }

    internal static bool CompleteReferences(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong returnedCookie) =>
        ReferenceContext(access, bump, control, records, state) && state->Armed == 1 &&
        state->Stage == 9 && state->InitializedSlots == 18 && state->Calls == 23 &&
        returnedCookie == (ReferenceCookie ^ ReferenceBlockTag(7)) && ReferencePage(access, records);

    private static bool ExpectedReferenceCall(RhpObservationStorage* records, CheckedReferenceState* state,
        ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        CheckedReferenceGeometry g = state->Geometry;
        if (state->Stage == 2)
        {
            ulong index = state->InitializedSlots;
            if (index >= 18 || operation != 2 || bytes != 8 || state->Calls != index) return false;
            ulong address = index < 2 ? records->Object2.Reference + g.CellPayload +
                (index == 0 ? g.PairFirst : g.PairLast) : records->Object4.Reference + g.BlockPayload +
                ((index - 2) / 2) * 24 + ((index & 1) == 0 ? g.PairFirst : g.PairLast);
            ulong value = index % 3 == 2 ? 0 : index % 3 == 0 ? records->Object0.Reference : records->Object1.Reference;
            return destination == address && operand == value;
        }
        if (state->InitializedSlots != 18 || state->Calls != state->Stage + 15 && state->Stage != 8) return false;
        return state->Stage switch
        {
            3 => operation == 1 && bytes == 8 && destination == records->Object3.Reference + g.CellReference && operand == records->Object0.Reference,
            4 => operation == 2 && bytes == 8 && destination == records->Object2.Reference + g.CellReference && operand == records->Object1.Reference,
            5 => operation == 3 && bytes == 8 && destination == records->Object3.Reference + g.CellPayload && operand == records->Object2.Reference + g.CellPayload,
            6 => operation == 3 && bytes == 8 && destination == records->Object3.Reference + g.CellPayload + 8 && operand == records->Object2.Reference + g.CellPayload + 8,
            8 => state->Calls == 22 && operation == 4 && bytes == 192 && destination == records->Object5.Reference + g.BlockPayload && operand == records->Object4.Reference + g.BlockPayload,
            _ => false
        };
    }

    private static bool ReferenceBlock(RhpObservationStorage* records, CheckedReferenceState* state, ulong address)
    {
        for (ulong i = 0; i < 8; i++)
            if (!ReferenceValue(records, Read64((byte*)address + i * 24 + state->Geometry.PairFirst)) ||
                !ReferenceValue(records, Read64((byte*)address + i * 24 + state->Geometry.PairLast)) ||
                Read64((byte*)address + i * 24 + state->Geometry.PairTag) != ReferenceBlockTag(i)) return false;
        return true;
    }

    private static bool ReferenceValue(RhpObservationStorage* records, ulong value) =>
        value == 0 || value == records->Object0.Reference || value == records->Object1.Reference;

    private static bool InReferencePage(ulong page, ulong address, ulong bytes) =>
        (address & 7) == 0 && address >= page && address - page <= 4096 && bytes <= 4096 - (address - page);

    private static bool ReferencePage(RhpNewFastHeapAccess* access, RhpObservationStorage* records)
    {
        for (ulong i = 0; i < 4096; i++) if (access->Backing[i] != records->BeforeAttempt[i]) return false;
        return true;
    }

    private static byte ScalarSetupByte(RhpObservationStorage* records, CheckedReferenceState* state, ulong page, ulong offset)
    {
        ulong first = records->Object2.Reference + state->Geometry.CellPayload + state->Geometry.PairTag - page;
        if (offset >= first && offset - first < 8) return (byte)(ReferencePairTag >> (int)(8 * (offset - first)));
        ulong block = records->Object4.Reference + state->Geometry.BlockPayload + state->Geometry.PairTag - page;
        for (ulong i = 0; i < 8; i++)
        {
            ulong at = block + i * 24;
            if (offset >= at && offset - at < 8) return (byte)(ReferenceBlockTag(i) >> (int)(8 * (offset - at)));
        }
        return records->BeforeAttempt[offset];
    }

    private static bool ReferenceGeometry(RhpObservationStorage* records, CheckedReferenceGeometry g)
    {
        // Pair's two reference words and trailing Tag are compiler observations;
        // named field association and holder offsets come from genuine byrefs.
        if (g.CellReference != 8 || g.CellTag != 16 || g.PairTag != 16 ||
            !((g.PairFirst == 0 && g.PairLast == 8) || (g.PairFirst == 8 && g.PairLast == 0)) ||
            g.CellPayload < 24 || (g.CellPayload & 7) != 0 || g.BlockPayload < 8 || (g.BlockPayload & 7) != 0)
            return false;
        for (ulong i = 0; i < 6; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            if (r.Route != 7 || r.Id != i + 1 || r.BaseSize < 24 || r.BaseSize > 4096 ||
                r.ScalarWidth != (i < 4 ? 8UL : 0) || r.Scalar != (i < 4 ? ReferenceRoleTag(i + 1) : 0) ||
                (i < 2 && r.ScalarOffset != 8) || (i >= 2 && i < 4 && r.ScalarOffset != g.CellTag) ||
                (i >= 2 && !WithinReferenceObject(r, i < 4 ? g.CellPayload : g.BlockPayload, i < 4 ? 24UL : 192UL))) return false;
        }
        return records->Object0.MethodTable == records->Object1.MethodTable &&
            records->Object2.MethodTable == records->Object3.MethodTable &&
            records->Object4.MethodTable == records->Object5.MethodTable &&
            records->Object0.MethodTable != records->Object2.MethodTable &&
            records->Object0.MethodTable != records->Object4.MethodTable &&
            records->Object2.MethodTable != records->Object4.MethodTable;
    }

    private static bool WithinReferenceObject(RhpObjectObservation r, ulong offset, ulong count) =>
        r.BaseSize >= 16 && offset >= 8 && offset <= r.BaseSize - 8 && count <= r.BaseSize - 8 - offset;

    private static bool ReferenceFrozenState(RhpNewFastBumpState* bump, RhpAllocationControl* control, CheckedReferenceState* state)
    {
        for (ulong i = 0; i < 80; i++) if (((byte*)bump)[i] != ((byte*)&state->FrozenBump)[i]) return false;
        for (ulong i = 0; i < ControlBytes; i++) if (((byte*)control)[i] != ((byte*)&state->FrozenControl)[i]) return false;
        return true;
    }

    private static bool ReferenceContext(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success ||
            !ReferenceStorage(access, bump, control, records, state) ||
            state->CanaryBefore != Canary || state->CanaryAfter != Canary || state->Self != (ulong)state ||
            state->Access != (ulong)access || state->Bump != (ulong)bump || state->Control != (ulong)control ||
            state->Records != (ulong)records || state->Busy != 0 || control->InProgress != 0 ||
            control->Completed != 6 || control->ConstructorCount != 6 || bump->AllocationCount != 6 ||
            !ReferenceFrozenState(bump, control, state) || !ReferenceGeometry(records, state->Geometry)) return false;
        // Compare all thirteen original inputs, not merely the heap or access-slot pointer.
        for (ulong i = 0; i < 104; i++) if (((byte*)access)[i] != ((byte*)&state->FrozenAccess)[i]) return false;
        ulong used = 0;
        for (ulong i = 0; i < 6; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            if (used > 4096 || r.BaseSize > 4096 - used || r.UsedBefore != used ||
                r.UsedAfter != used + r.BaseSize || r.Count != i + 1 || r.Allocation != (ulong)access->Backing + used ||
                r.Reference != r.Allocation + 8 || r.End != r.Allocation + r.BaseSize ||
                !PrefixDisjoint(*access, access, bump, control, records, r.MethodTable) ||
                Overlap(r.MethodTable, 24, (ulong)state, (ulong)sizeof(CheckedReferenceState)) ||
                !TryReadPrefix((byte*)control->Image, control->ImageBytes, r.MethodTable, r.MethodTable,
                    out NativeAotMethodTablePrefix prefix, out ulong p0, out ulong p1, out ulong p2) ||
                p0 != r.Prefix0 || p1 != r.Prefix1 || p2 != r.Prefix2 || prefix.BaseSize != r.BaseSize ||
                Read64((byte*)r.Allocation) != 0 || Read64((byte*)r.Reference) != r.MethodTable ||
                (i < 4 && Read64((byte*)r.Reference + r.ScalarOffset) != ReferenceRoleTag(i + 1))) return false;
            used += r.BaseSize;
        }
        return used == bump->UsedBytes;
    }

    private static bool ReferenceStorage(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump,
        RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        ulong at = (ulong)state, size = (ulong)sizeof(CheckedReferenceState);
        if (!Range(at, size, 8)) return false;
        // Admit the state before Zero reads it or preparation writes it. Static
        // image storage is allowed, but none of the six retained MT prefixes is.
        for (ulong i = 0; i < 6; i++)
        {
            ulong token = (&records->Object0)[i].MethodTable;
            if (!Range(token, 24, 8) || Overlap(at, size, token, 24)) return false;
        }
        RhpNewFastHeapAccess a = *access;
        return !Overlap(at, size, (ulong)access, 104) && !Overlap(at, size, (ulong)bump, 80) &&
            !Overlap(at, size, (ulong)control, ControlBytes) && !Overlap(at, size, (ulong)records, ObservationBytes) &&
            !Overlap(at, size, (ulong)a.Allocator, 56) && !Overlap(at, size, (ulong)a.Nodes, 48) &&
            !Overlap(at, size, (ulong)a.Reservations, 200) && !Overlap(at, size, (ulong)a.Heap, 216) &&
            !Overlap(at, size, (ulong)a.Backing, 8192);
    }
}
