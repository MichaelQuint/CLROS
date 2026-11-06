using OS.Kernel.Runtime;

namespace OS.Runtime;
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
internal static unsafe partial class RhpNewFastAllocation
{
    [System.Runtime.RuntimeExport("CLROS_CheckedReferenceBarrier_Apply")]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static uint GcReferenceCallback(ulong operation, ulong destination, ulong operand, ulong bytes) =>
        ApplyGcReference(operation, destination, operand, bytes);
}
#endif
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE
// Only this private enabled route produces genuine references. The ordinary
// scalar callback and managed bulk entry share a POD-only admission core.
internal static unsafe partial class RhpNewFastAllocation
{
    private static CheckedReferenceState s_referenceState;
    private static CheckedReferenceStackState s_referenceStack;

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
                while (true)
                    System.RuntimeFailFast.Halt();
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
                != RhpAdmissionStatus.Success)
                RejectReferences(2);
            ReferenceObjectLine((&r->Object0)[id - 1]);
        }
    }

    private static void RunReferences(RhpNewFastHeapAccess original, byte* image, ulong imageBytes)
    {
        // This address witnesses a real local, not a sampled RSP. The native gate
        // must bind its frame offset and the complete 16 KiB clearance ceiling.
        ulong stackWitness = 0;
        R(0);
        End();
        fixed (RhpNewFastHeapAccess* a = &s_access)
        fixed (RhpNewFastBumpState* b = &s_bump)
        fixed (RhpAllocationControl* c = &s_control)
        fixed (RhpObservationStorage* r = &s_records)
        fixed (CheckedReferenceState* state = &s_referenceState)
        fixed (CheckedReferenceStackState* stack = &s_referenceStack)
        {
            if (RhpNewFastAllocationCore.Prepare(original, a, b, c, r, image, imageBytes) != RhpAdmissionStatus.Success)
                RejectReferences(3);
            PreparationLine();
            RhpNewFastResult bound = RhpNewFastAllocationCore.Bind(a, b, c, r);
            if (!bound.Succeeded)
                RejectReferences(4);
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
                    Address(ref part.Tag) - Address(ref part) != geometry.PairTag)
                    RejectReferences(6);
            }
            if (!RhpNewFastAllocationCore.PrepareReferences(a, b, c, r, state, geometry))
                RejectReferences(7);
            ReferenceGeometryLine(state);
            pair.Tag = RhpNewFastAllocationCore.ReferencePairTag;
            for (ulong i = 0; i < 8; i++)
                AddressReferenceBlockPair(ref block, i).Tag = RhpNewFastAllocationCore.ReferenceBlockTag(i);
            if (!RhpNewFastAllocationCore.CompleteReferenceScalars(a, b, c, r, state))
                RejectReferences(8);
            if (!OS.Boot.EfiEntry.TryGetOriginalReferenceStack(out ulong authority,
                out ulong stackStart, out ulong stackEnd, out ulong initialRsp) ||
                !RhpNewFastAllocationCore.ArmReferenceStack(a, b, c, r, state, stack,
                    authority, stackStart, stackEnd, initialRsp, (ulong)&stackWitness))
                RejectReferences(16);

            // Each compiler-selected checked store must return with its original
            // cookie before another operation can consume that result.
            for (ulong i = 0; i < 18; i++)
            {
                object value = i % 3 == 2 ? null : i % 3 == 0 ? valueA : valueB;
                ref ReferencePair part = ref (i < 2 ? ref pair : ref AddressReferenceBlockPair(ref block, (i - 2) / 2));
                ref object slot = ref ((i & 1) == 0 ? ref part.First : ref part.Last);
                if (StoreReferenceUnknown(ref slot, value, RhpNewFastAllocationCore.ReferenceCookie) != RhpNewFastAllocationCore.ReferenceCookie ||
                    state->Calls != i + 1 || Bits(ReadReferenceSlot(ref slot)) != Bits(value))
                    RejectReferences(9);
            }
            CheckReferenceStack(a, b, c, r, state, stack, (ulong)&stackWitness);
            ReferenceOperationLine(2, state->Calls, RhpNewFastAllocationCore.ReferenceCookie);
            ulong fieldCookie = StoreReferenceField(destination, valueA, RhpNewFastAllocationCore.ReferenceCookie);
            if (fieldCookie != (RhpNewFastAllocationCore.ReferenceCookie ^ RhpNewFastAllocationCore.ReferenceRoleTag(4)) ||
                Bits(ReadReferenceField(destination)) != Bits(valueA) || state->Calls != 19)
                RejectReferences(10);
            CheckReferenceStack(a, b, c, r, state, stack, (ulong)&stackWitness);
            ReferenceOperationLine(1, state->Calls, fieldCookie);
            ulong checkedCookie = StoreReferenceUnknown(ref source.Reference, valueB, RhpNewFastAllocationCore.ReferenceCookie);
            if (checkedCookie != RhpNewFastAllocationCore.ReferenceCookie ||
                Bits(ReadReferenceField(source)) != Bits(valueB) || state->Calls != 20)
                RejectReferences(11);
            CheckReferenceStack(a, b, c, r, state, stack, (ulong)&stackWitness);
            ReferenceOperationLine(2, state->Calls, checkedCookie);
            ulong pairCookie = CopyReferencePair(ref AddressReferencePair(destination), ref pair, RhpNewFastAllocationCore.ReferenceCookie);
            if (!RhpNewFastAllocationCore.CompleteReferencePair(a, b, c, r, state, pairCookie) ||
                Bits(ReadReferenceFirst(ref AddressReferencePair(destination))) != Bits(valueA) ||
                Bits(ReadReferenceLast(ref AddressReferencePair(destination))) != Bits(valueB))
                RejectReferences(12);
            CheckReferenceStack(a, b, c, r, state, stack, (ulong)&stackWitness);
            ReferenceOperationLine(3, state->Calls, pairCookie);
            ulong blockCookie = CopyReferenceBlock(ref AddressReferenceBlock(destinationBlock), ref block, RhpNewFastAllocationCore.ReferenceCookie);
            if (!RhpNewFastAllocationCore.CompleteReferences(a, b, c, r, state, blockCookie))
                RejectReferences(13);
            for (ulong i = 0; i < 8; i++)
            {
                ref ReferencePair part = ref AddressReferenceBlockPair(ref AddressReferenceBlock(destinationBlock), i);
                ulong firstIndex = 2 + i * 2, lastIndex = firstIndex + 1;
                ulong first = firstIndex % 3 == 2 ? 0 : firstIndex % 3 == 0 ? Bits(valueA) : Bits(valueB);
                ulong last = lastIndex % 3 == 2 ? 0 : lastIndex % 3 == 0 ? Bits(valueA) : Bits(valueB);
                if (Bits(ReadReferenceFirst(ref part)) != first || Bits(ReadReferenceLast(ref part)) != last ||
                    ReadReferenceTag(ref part) != RhpNewFastAllocationCore.ReferenceBlockTag(i))
                    RejectReferences(14);
            }
            ReferenceOperationLine(4, state->Calls, blockCookie);
            ManagedHeapPageRangeResult retained = RhpNewFastAllocationCore.Retained(*a);
            if (!retained.IsReady || retained.ReadBytes != 4096 || retained.ContentsAreZeroNow ||
                c->Completed != 6 || c->ConstructorCount != 6 || b->AllocationCount != 6)
                RejectReferences(15);
            RetainLine(retained);
            CheckReferenceStack(a, b, c, r, state, stack, (ulong)&stackWitness);
            ReferenceStackLine(stack);
            R(1);
            Decimal(b->UsedBytes);
            R(2);
            End();
            R(3);
            End();
        }
    }

    // Outside every returning callback/special-helper path. Reacquire the same
    // original authority before the core can read either executing-stack word.
    private static void CheckReferenceStack(RhpNewFastHeapAccess* a, RhpNewFastBumpState* b,
        RhpAllocationControl* c, RhpObservationStorage* r, CheckedReferenceState* state,
        CheckedReferenceStackState* stack, ulong witness)
    {
        if (!OS.Boot.EfiEntry.TryGetOriginalReferenceStack(out ulong authority,
            out ulong start, out ulong end, out ulong initialRsp) ||
            !RhpNewFastAllocationCore.CheckReferenceStack(a, b, c, r, state, stack,
                authority, start, end, initialRsp, witness))
            RejectReferences(17);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong Address<T>(ref T value)
    {
        fixed (byte* p = &System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref value))
            return (ulong)p;
    }
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
            case 0:
                return ref value.P0;
            case 1:
                return ref value.P1;
            case 2:
                return ref value.P2;
            case 3:
                return ref value.P3;
            case 4:
                return ref value.P4;
            case 5:
                return ref value.P5;
            case 6:
                return ref value.P6;
            case 7:
                return ref value.P7;
            default:
                while (true)
                    System.RuntimeFailFast.Halt();
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong StoreReferenceField(ReferenceCell destination, object value, ulong cookie)
    {
        destination.Reference = value;
        return cookie ^ destination.Tag;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong StoreReferenceUnknown(ref object destination, object value, ulong cookie)
    {
        destination = value;
        return cookie;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong CopyReferencePair(ref ReferencePair destination, ref ReferencePair source, ulong cookie)
    {
        destination = source;
        return cookie ^ destination.Tag;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong CopyReferenceBlock(ref ReferenceBlock destination, ref ReferenceBlock source, ulong cookie)
    {
        destination = source;
        return cookie ^ destination.P7.Tag;
    }
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
    {
        R(4);
        Decimal(checkpoint);
        End();
        while (true)
            System.RuntimeFailFast.Halt();
    }

    private static void R(int token)
    {
        for (int index = 0; ; index++)
        {
            byte value = ReferenceAscii(token, index);
            if (value == 0)
                return;
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
        23 => index switch { 0 => 77, 1 => 48, 2 => 52, 3 => 95, 4 => 82, 5 => 69, 6 => 70, 7 => 69, 8 => 82, 9 => 69, 10 => 78, 11 => 67, 12 => 69, 13 => 95, 14 => 83, 15 => 84, 16 => 65, 17 => 67, 18 => 75, 19 => 32, 20 => 115, 21 => 116, 22 => 97, 23 => 114, 24 => 116, 25 => 61, _ => 0 },
        24 => index switch { 0 => 32, 1 => 101, 2 => 110, 3 => 100, 4 => 61, _ => 0 },
        25 => index switch { 0 => 32, 1 => 105, 2 => 110, 3 => 105, 4 => 116, 5 => 105, 6 => 97, 7 => 108, 8 => 82, 9 => 115, 10 => 112, 11 => 61, _ => 0 },
        26 => index switch { 0 => 32, 1 => 119, 2 => 105, 3 => 116, 4 => 110, 5 => 101, 6 => 115, 7 => 115, 8 => 61, _ => 0 },
        27 => index switch { 0 => 32, 1 => 115, 2 => 101, 3 => 110, 4 => 116, 5 => 105, 6 => 110, 7 => 101, 8 => 108, 9 => 48, 10 => 61, _ => 0 },
        28 => index switch { 0 => 32, 1 => 115, 2 => 101, 3 => 110, 4 => 116, 5 => 105, 6 => 110, 7 => 101, 8 => 108, 9 => 49, 10 => 61, _ => 0 },
        29 => index switch { 0 => 32, 1 => 101, 2 => 120, 3 => 112, 4 => 101, 5 => 99, 6 => 116, 7 => 101, 8 => 100, 9 => 48, 10 => 61, _ => 0 },
        30 => index switch { 0 => 32, 1 => 101, 2 => 120, 3 => 112, 4 => 101, 5 => 99, 6 => 116, 7 => 101, 8 => 100, 9 => 49, 10 => 61, _ => 0 },
        31 => index switch { 0 => 32, 1 => 97, 2 => 114, 3 => 109, 4 => 101, 5 => 100, 6 => 48, 7 => 61, _ => 0 },
        32 => index switch { 0 => 32, 1 => 97, 2 => 114, 3 => 109, 4 => 101, 5 => 100, 6 => 49, 7 => 61, _ => 0 },
        33 => index switch { 0 => 32, 1 => 102, 2 => 105, 3 => 110, 4 => 97, 5 => 108, 6 => 48, 7 => 61, _ => 0 },
        34 => index switch { 0 => 32, 1 => 102, 2 => 105, 3 => 110, 4 => 97, 5 => 108, 6 => 49, 7 => 61, _ => 0 },
        35 => index switch { 0 => 32, 1 => 99, 2 => 104, 3 => 101, 4 => 99, 5 => 107, 6 => 115, 7 => 61, _ => 0 },
        36 => index switch { 0 => 32, 1 => 119, 2 => 111, 3 => 114, 4 => 100, 5 => 115, 6 => 61, 7 => 50, 8 => 32, 9 => 112, 10 => 114, 11 => 101, 12 => 115, 13 => 101, 14 => 114, 15 => 118, 16 => 101, 17 => 100, 18 => 61, 19 => 116, 20 => 114, 21 => 117, 22 => 101, _ => 0 },
        _ => 0
    };

    // Numeric observations are serialized only outside the returning callback graph.
    private static void ReferenceObjectLine(RhpObjectObservation r)
    {
        R(5);
        Decimal(r.Id);
        T(51);
        Hex(r.MethodTable);
        T(52);
        Hex(r.Prefix0);
        T(53);
        Hex(r.Prefix1);
        T(54);
        Hex(r.Prefix2);
        T(55);
        Hex(r.Flags);
        T(4);
        Decimal(r.BaseSize);
        T(56);
        Hex(r.Allocation);
        T(57);
        Hex(r.Reference);
        T(58);
        Hex(r.End);
        T(2);
        Decimal(r.UsedBefore);
        T(3);
        Decimal(r.UsedAfter);
        T(20);
        Decimal(r.Count);
        T(59);
        Hex(r.Sync);
        T(60);
        Hex(r.ObservedMt);
        T(61);
        Decimal(r.ZeroBytes);
        T(62);
        Hex(r.ZeroOr);
        R(6);
        Decimal(r.ScalarOffset);
        R(7);
        Hex(r.Scalar);
        End();
    }
    private static void ReferenceGeometryLine(CheckedReferenceState* state)
    {
        R(8);
        Hex((ulong)state);
        R(9);
        Decimal((ulong)sizeof(CheckedReferenceState));
        R(10);
        Decimal(state->Geometry.CellReference);
        R(11);
        Decimal(state->Geometry.CellTag);
        R(12);
        Decimal(state->Geometry.CellPayload);
        R(13);
        Decimal(state->Geometry.PairFirst);
        R(14);
        Decimal(state->Geometry.PairLast);
        R(15);
        Decimal(state->Geometry.PairTag);
        R(16);
        Decimal(state->Geometry.BlockPayload);
        End();
    }
    private static void ReferenceStackLine(CheckedReferenceStackState* stack)
    {
        R(23);
        Hex(stack->Start);
        R(24);
        Hex(stack->End);
        R(25);
        Hex(stack->InitialRsp);
        R(26);
        Hex(stack->Witness);
        R(27);
        Hex(stack->Start);
        R(28);
        Hex(stack->Start + 8);
        R(29);
        Hex(RhpNewFastAllocationCore.ReferenceStackWord0);
        R(30);
        Hex(RhpNewFastAllocationCore.ReferenceStackWord1);
        R(31);
        Hex(stack->Armed0);
        R(32);
        Hex(stack->Armed1);
        R(33);
        Hex(stack->Final0);
        R(34);
        Hex(stack->Final1);
        R(35);
        Decimal(stack->Checks);
        R(36);
        End();
    }
    private static void ReferenceOperationLine(ulong operation, ulong calls, ulong cookie)
    {
        R(17);
        Decimal(operation);
        R(18);
        Decimal(calls);
        R(19);
        Hex(cookie);
        End();
    }
}

#pragma warning disable CS0649

#pragma warning restore CS0649
#endif
