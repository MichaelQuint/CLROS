using OS.Kernel.Runtime;

namespace OS.Runtime;
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#endif
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE

#pragma warning disable CS0649

#pragma warning restore CS0649
#endif
internal static unsafe partial class RhpNewFastAllocationCore
{
    internal const ulong ReferenceStackBytes = 33 * 4096;
    internal const ulong ReferenceStackBudget = 16384;
    internal const ulong ReferenceStackWord0 = 0x195A86B4C327D0EF;
    internal const ulong ReferenceStackWord1 = 0x62C79E1035AB48D6;
    internal static bool ArmReferenceStack(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, CheckedReferenceStackState* stack, ulong authority, ulong start, ulong end, ulong initialRsp, ulong witness)
    {
        if (!ReferenceStackAdmission(access, bump, control, records, state, stack, authority, start, end, initialRsp, witness) || state->Stage != 2 || state->Calls != 0 || state->Armed != 1 || !Zero((byte*)stack, (ulong)sizeof(CheckedReferenceStackState)))
            return false;
        // Every rejection above precedes every protected-state/sentinel write.
        *stack = new CheckedReferenceStackState
        {
            Self = (ulong)stack,
            Authority = authority,
            Start = start,
            End = end,
            InitialRsp = initialRsp,
            Witness = witness,
            Phase = 1
        };
        *(ulong*)start = ReferenceStackWord0;
        *(ulong*)(start + 8) = ReferenceStackWord1;
        stack->Armed0 = ReadReferenceStackWord(start);
        stack->Armed1 = ReadReferenceStackWord(start + 8);
        if (stack->Armed0 != ReferenceStackWord0 || stack->Armed1 != ReferenceStackWord1)
        {
            stack->Phase = 3;
            return false;
        }

        return true;
    }

    internal static bool CheckReferenceStack(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, CheckedReferenceStackState* stack, ulong authority, ulong start, ulong end, ulong initialRsp, ulong witness)
    {
        if (!ReferenceStackAdmission(access, bump, control, records, state, stack, authority, start, end, initialRsp, witness) || stack->Self != (ulong)stack || stack->Authority != authority || stack->Start != start || stack->End != end || stack->InitialRsp != initialRsp || stack->Witness != witness || stack->Phase != 1 || stack->Armed0 != ReferenceStackWord0 || stack->Armed1 != ReferenceStackWord1 || stack->Checks >= 5)
            return false;
        ulong expectedCalls = stack->Checks switch
        {
            0 => 18,
            1 => 19,
            2 => 20,
            3 => 22,
            _ => 23
        };
        ulong expectedStage = stack->Checks switch
        {
            0 => 3,
            1 => 4,
            2 => 5,
            3 => 8,
            _ => 9
        };
        if (state->Armed != 1 || state->Calls != expectedCalls || state->Stage != expectedStage)
            return false;
        ulong word0 = ReadReferenceStackWord(start), word1 = ReadReferenceStackWord(start + 8);
        if (word0 != ReferenceStackWord0 || word1 != ReferenceStackWord1)
        {
            stack->Phase = 3;
            return false;
        }

        stack->Final0 = word0;
        stack->Final1 = word1;
        stack->Checks++;
        if (stack->Checks == 5)
            stack->Phase = 2;
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong ReadReferenceStackWord(ulong address) => *(ulong*)address;
    private static bool ReferenceStackAdmission(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, CheckedReferenceStackState* stack, ulong authority, ulong start, ulong end, ulong initialRsp, ulong witness)
    {
        ulong stackState = (ulong)stack, stateBytes = (ulong)sizeof(CheckedReferenceStackState);
        if (!Range(stackState, stateBytes, 8) || !Range(authority, 768, 8) || !Range(start, ReferenceStackBytes, 4096) || end != start + ReferenceStackBytes || initialRsp != end - 40 || !Range(witness, 8, 8) || witness < start + 16 || witness + 8 > initialRsp || witness - (start + 16) < ReferenceStackBudget || Overlap(stackState, stateBytes, start, ReferenceStackBytes) || Overlap(authority, 768, start, ReferenceStackBytes) || Overlap(authority, 768, stackState, stateBytes) || !ReferenceContext(access, bump, control, records, state) || Overlap(start, ReferenceStackBytes, control->Image, control->ImageBytes))
            return false;
        return ReferenceStackSpan(access, bump, control, records, state, stackState, stateBytes) && ReferenceStackSpan(access, bump, control, records, state, start, ReferenceStackBytes);
    }

    private static bool ReferenceStackSpan(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong at, ulong size)
    {
        // Original context was fully validated before these pointer values were read.
        for (ulong i = 0; i < 6; i++)
            if (Overlap(at, size, (&records->Object0)[i].MethodTable, 24))
                return false;
        return !Overlap(at, size, (ulong)access, 104) && !Overlap(at, size, (ulong)bump, 80) && !Overlap(at, size, (ulong)control, ControlBytes) && !Overlap(at, size, (ulong)records, ObservationBytes) && !Overlap(at, size, (ulong)state, (ulong)sizeof(CheckedReferenceState)) && !Overlap(at, size, (ulong)access->Allocator, 56) && !Overlap(at, size, (ulong)access->Nodes, 48) && !Overlap(at, size, (ulong)access->Reservations, 200) && !Overlap(at, size, (ulong)access->Heap, 216) && !Overlap(at, size, (ulong)access->Backing, 8192);
    }

    internal const ulong ReferenceCookie = 0x179BCDE02468ACE0;
    internal const ulong ReferencePairTag = 0x1928374655AABBCC;
    internal static ulong ReferenceBlockTag(ulong index) => 0x3141592653589700 + index;
    internal static ulong ReferenceRoleTag(ulong id) => 0x1111222233334400 + id;
    // Six real allocations still enter the original AllocateCore and its sole host
    // allocator commit. Route 7 is finite and does not alter any older profile.
    internal static RhpAllocationAttempt AllocateReferences(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong token, ulong expectedToken, ulong id) => AllocateCore(access, bump, control, records, token, expectedToken, id, 7, false, 0, 0, false, true);
    internal static RhpAdmissionStatus CompleteReferenceAllocation(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, ulong id, ulong reference, ulong scalarAddress, ulong typedScalar)
    {
        RhpAdmissionStatus ready = Ready(access, bump, control, records);
        if (ready != RhpAdmissionStatus.Success)
            return ready;
        if (control->InProgress != 0)
            return RhpAdmissionStatus.Reentry;
        if (id == 0 || id > 6 || id != control->Completed + 1)
            return RhpAdmissionStatus.InvalidFixture;
        RhpObjectObservation* r = &(&records->Object0)[id - 1];
        ulong width = id <= 4 ? 8UL : 0;
        if (r->Id != id || r->Route != 7 || r->Reference != reference || reference == 0 || r->UsedBefore > 4096 || r->BaseSize < 24 || r->BaseSize > 4096 - r->UsedBefore || r->Allocation != (ulong)access->Backing + r->UsedBefore || reference != r->Allocation + 8 || r->End != r->Allocation + r->BaseSize || r->UsedAfter != r->UsedBefore + r->BaseSize || r->Count != id || bump->AllocationCount != id || bump->UsedBytes != r->UsedAfter || (width == 0 && (scalarAddress != 0 || typedScalar != 0)) || (width != 0 && (typedScalar != ReferenceRoleTag(id) || scalarAddress < reference + 8 || scalarAddress > r->End || 8 > r->End - scalarAddress || (scalarAddress & 7) != 0 || Read64((byte*)scalarAddress) != typedScalar)))
            return RhpAdmissionStatus.ObservationRejected;
        // Record only an observed scalar field; untouched payload/padding must remain zero.
        RhpObjectObservation complete = *r;
        complete.ScalarOffset = width == 0 ? 0 : scalarAddress - reference;
        complete.ScalarWidth = width;
        complete.Scalar = typedScalar;
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
    internal static bool PrepareReferences(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, CheckedReferenceGeometry geometry)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success || control->InProgress != 0 || !ReferenceStorage(access, bump, control, records, state) || !Zero((byte*)state, (ulong)sizeof(CheckedReferenceState)) || control->Completed != 6 || control->ConstructorCount != 6 || bump->AllocationCount != 6 || !Prior(records, 6, (ulong)access->Backing, true) || !ReferenceGeometry(records, geometry))
            return false;
        *state = new CheckedReferenceState
        {
            CanaryBefore = Canary,
            CanaryAfter = Canary,
            Self = (ulong)state,
            Access = (ulong)access,
            Bump = (ulong)bump,
            Control = (ulong)control,
            Records = (ulong)records,
            Stage = 1,
            Geometry = geometry,
            FrozenAccess = *access,
            FrozenBump = *bump,
            FrozenControl = *control
        };
        for (ulong i = 0; i < 4096; i++)
            records->BeforeAttempt[i] = access->Backing[i];
        return true;
    }

    // Only the genuine scalar initialization interval may change these nine Tag
    // words before reference helpers are armed. It is not barrier evidence.
    internal static bool CompleteReferenceScalars(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Stage != 1 || state->Armed != 0 || state->Calls != 0 || state->InitializedSlots != 0)
            return false;
        for (ulong i = 0; i < 4096; i++)
            if (access->Backing[i] != ScalarSetupByte(records, state, (ulong)access->Backing, i))
                return false;
        for (ulong i = 0; i < 4096; i++)
            records->BeforeAttempt[i] = access->Backing[i];
        state->Stage = 2;
        state->Armed = 1;
        return true;
    }

    // No writes occur before all preconditions pass. Once the leaf commits, a
    // failed post-readback is terminal (Busy remains set), never a rollback claim.
    internal static uint ApplyReference(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong operation, ulong destination, ulong operand, ulong byteCount)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Armed != 1 || !ReferencePage(access, records) || !ExpectedReferenceCall(records, state, operation, destination, operand, byteCount))
            return 0;
        ulong page = (ulong)access->Backing;
        if (!InReferencePage(page, destination, byteCount))
            return 0;
        bool move = operation == 3 || operation == 4;
        if (move && !InReferencePage(page, operand, byteCount))
            return 0;
        if (operation == 3 && (destination > ~0UL - 8 || operand > ~0UL - 8))
            return 0;
        if (move)
        {
            if (operation == 3 && !ReferenceValue(records, Read64((byte*)operand)))
                return 0;
            if (operation == 4 && !ReferenceBlock(records, state, operand))
                return 0;
        }
        else if (!ReferenceValue(records, operand))
            return 0;
        state->Busy = 1;
        CheckedReferenceBulkResult result = move ? CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryMove(*access, CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage, operand - page, destination - page, byteCount) : CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryStore(*access, CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage, destination - page, operand);
        if (!result.Succeeded || result.BytesWritten != byteCount || Ready(access, bump, control, records) != RhpAdmissionStatus.Success || !ReferenceFrozenState(bump, control, state))
            return 0;
        ulong destinationOffset = destination - page, sourceOffset = move ? operand - page : 0;
        for (ulong i = 0; i < 4096; i++)
        {
            byte expected = records->BeforeAttempt[i];
            if (i >= destinationOffset && i - destinationOffset < byteCount)
                expected = move ? records->BeforeAttempt[sourceOffset + i - destinationOffset] : (byte)(operand >> (int)(8 * (i - destinationOffset)));
            if (access->Backing[i] != expected)
                return 0;
        }

        for (ulong i = 0; i < 4096; i++)
            records->BeforeAttempt[i] = access->Backing[i];
        state->Calls++;
        if (state->Stage == 2)
        {
            state->InitializedSlots++;
            if (state->InitializedSlots == 18)
                state->Stage = 3;
        }
        else
            state->Stage++;
        state->Busy = 0;
        return 1;
    }

    // CopyPair returns after two independently committed helpers and its scalar
    // movsq. Only this exact pending Tag store is admitted at outer completion.
    internal static bool CompleteReferencePair(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong returnedCookie)
    {
        if (!ReferenceContext(access, bump, control, records, state) || state->Armed != 1 || state->Stage != 7 || state->Calls != 22 || returnedCookie != (ReferenceCookie ^ ReferencePairTag))
            return false;
        ulong tag = records->Object3.Reference + state->Geometry.CellPayload + state->Geometry.PairTag;
        ulong offset = tag - (ulong)access->Backing;
        for (ulong i = 0; i < 4096; i++)
        {
            byte expected = i >= offset && i - offset < 8 ? (byte)(ReferencePairTag >> (int)(8 * (i - offset))) : records->BeforeAttempt[i];
            if (access->Backing[i] != expected)
                return false;
        }

        for (ulong i = 0; i < 4096; i++)
            records->BeforeAttempt[i] = access->Backing[i];
        state->Stage = 8;
        return true;
    }

    internal static bool CompleteReferences(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state, ulong returnedCookie) => ReferenceContext(access, bump, control, records, state) && state->Armed == 1 && state->Stage == 9 && state->InitializedSlots == 18 && state->Calls == 23 && returnedCookie == (ReferenceCookie ^ ReferenceBlockTag(7)) && ReferencePage(access, records);
    private static bool ExpectedReferenceCall(RhpObservationStorage* records, CheckedReferenceState* state, ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        CheckedReferenceGeometry g = state->Geometry;
        if (state->Stage == 2)
        {
            ulong index = state->InitializedSlots;
            if (index >= 18 || operation != 2 || bytes != 8 || state->Calls != index)
                return false;
            ulong address = index < 2 ? records->Object2.Reference + g.CellPayload + (index == 0 ? g.PairFirst : g.PairLast) : records->Object4.Reference + g.BlockPayload + ((index - 2) / 2) * 24 + ((index & 1) == 0 ? g.PairFirst : g.PairLast);
            ulong value = index % 3 == 2 ? 0 : index % 3 == 0 ? records->Object0.Reference : records->Object1.Reference;
            return destination == address && operand == value;
        }

        if (state->InitializedSlots != 18 || state->Calls != state->Stage + 15 && state->Stage != 8)
            return false;
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
            if (!ReferenceValue(records, Read64((byte*)address + i * 24 + state->Geometry.PairFirst)) || !ReferenceValue(records, Read64((byte*)address + i * 24 + state->Geometry.PairLast)) || Read64((byte*)address + i * 24 + state->Geometry.PairTag) != ReferenceBlockTag(i))
                return false;
        return true;
    }

    private static bool ReferenceValue(RhpObservationStorage* records, ulong value) => value == 0 || value == records->Object0.Reference || value == records->Object1.Reference;
    private static bool InReferencePage(ulong page, ulong address, ulong bytes) => (address & 7) == 0 && address >= page && address - page <= 4096 && bytes <= 4096 - (address - page);
    private static bool ReferencePage(RhpNewFastHeapAccess* access, RhpObservationStorage* records)
    {
        for (ulong i = 0; i < 4096; i++)
            if (access->Backing[i] != records->BeforeAttempt[i])
                return false;
        return true;
    }

    private static byte ScalarSetupByte(RhpObservationStorage* records, CheckedReferenceState* state, ulong page, ulong offset)
    {
        ulong first = records->Object2.Reference + state->Geometry.CellPayload + state->Geometry.PairTag - page;
        if (offset >= first && offset - first < 8)
            return (byte)(ReferencePairTag >> (int)(8 * (offset - first)));
        ulong block = records->Object4.Reference + state->Geometry.BlockPayload + state->Geometry.PairTag - page;
        for (ulong i = 0; i < 8; i++)
        {
            ulong at = block + i * 24;
            if (offset >= at && offset - at < 8)
                return (byte)(ReferenceBlockTag(i) >> (int)(8 * (offset - at)));
        }

        return records->BeforeAttempt[offset];
    }

    private static bool ReferenceGeometry(RhpObservationStorage* records, CheckedReferenceGeometry g)
    {
        // Pair's two reference words and trailing Tag are compiler observations;
        // named field association and holder offsets come from genuine byrefs.
        if (g.CellReference != 8 || g.CellTag != 16 || g.PairTag != 16 || !((g.PairFirst == 0 && g.PairLast == 8) || (g.PairFirst == 8 && g.PairLast == 0)) || g.CellPayload < 24 || (g.CellPayload & 7) != 0 || g.BlockPayload < 8 || (g.BlockPayload & 7) != 0)
            return false;
        for (ulong i = 0; i < 6; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            if (r.Route != 7 || r.Id != i + 1 || r.BaseSize < 24 || r.BaseSize > 4096 || r.ScalarWidth != (i < 4 ? 8UL : 0) || r.Scalar != (i < 4 ? ReferenceRoleTag(i + 1) : 0) || (i < 2 && r.ScalarOffset != 8) || (i >= 2 && i < 4 && r.ScalarOffset != g.CellTag) || (i >= 2 && !WithinReferenceObject(r, i < 4 ? g.CellPayload : g.BlockPayload, i < 4 ? 24UL : 192UL)))
                return false;
        }

        return records->Object0.MethodTable == records->Object1.MethodTable && records->Object2.MethodTable == records->Object3.MethodTable && records->Object4.MethodTable == records->Object5.MethodTable && records->Object0.MethodTable != records->Object2.MethodTable && records->Object0.MethodTable != records->Object4.MethodTable && records->Object2.MethodTable != records->Object4.MethodTable;
    }

    private static bool WithinReferenceObject(RhpObjectObservation r, ulong offset, ulong count) => r.BaseSize >= 16 && offset >= 8 && offset <= r.BaseSize - 8 && count <= r.BaseSize - 8 - offset;
    private static bool ReferenceFrozenState(RhpNewFastBumpState* bump, RhpAllocationControl* control, CheckedReferenceState* state)
    {
        for (ulong i = 0; i < 80; i++)
            if (((byte*)bump)[i] != ((byte*)&state->FrozenBump)[i])
                return false;
        for (ulong i = 0; i < ControlBytes; i++)
            if (((byte*)control)[i] != ((byte*)&state->FrozenControl)[i])
                return false;
        return true;
    }

    private static bool ReferenceContext(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        if (Ready(access, bump, control, records) != RhpAdmissionStatus.Success || !ReferenceStorage(access, bump, control, records, state) || state->CanaryBefore != Canary || state->CanaryAfter != Canary || state->Self != (ulong)state || state->Access != (ulong)access || state->Bump != (ulong)bump || state->Control != (ulong)control || state->Records != (ulong)records || state->Busy != 0 || control->InProgress != 0 || control->Completed != 6 || control->ConstructorCount != 6 || bump->AllocationCount != 6 || !ReferenceFrozenState(bump, control, state) || !ReferenceGeometry(records, state->Geometry))
            return false;
        // Compare all thirteen original inputs, not merely the heap or access-slot pointer.
        for (ulong i = 0; i < 104; i++)
            if (((byte*)access)[i] != ((byte*)&state->FrozenAccess)[i])
                return false;
        ulong used = 0;
        for (ulong i = 0; i < 6; i++)
        {
            RhpObjectObservation r = (&records->Object0)[i];
            if (used > 4096 || r.BaseSize > 4096 - used || r.UsedBefore != used || r.UsedAfter != used + r.BaseSize || r.Count != i + 1 || r.Allocation != (ulong)access->Backing + used || r.Reference != r.Allocation + 8 || r.End != r.Allocation + r.BaseSize || !PrefixDisjoint(*access, access, bump, control, records, r.MethodTable) || Overlap(r.MethodTable, 24, (ulong)state, (ulong)sizeof(CheckedReferenceState)) || !TryReadPrefix((byte*)control->Image, control->ImageBytes, r.MethodTable, r.MethodTable, out NativeAotMethodTablePrefix prefix, out ulong p0, out ulong p1, out ulong p2) || p0 != r.Prefix0 || p1 != r.Prefix1 || p2 != r.Prefix2 || prefix.BaseSize != r.BaseSize || Read64((byte*)r.Allocation) != 0 || Read64((byte*)r.Reference) != r.MethodTable || (i < 4 && Read64((byte*)r.Reference + r.ScalarOffset) != ReferenceRoleTag(i + 1)))
                return false;
            used += r.BaseSize;
        }

        return used == bump->UsedBytes;
    }

    private static bool ReferenceStorage(RhpNewFastHeapAccess* access, RhpNewFastBumpState* bump, RhpAllocationControl* control, RhpObservationStorage* records, CheckedReferenceState* state)
    {
        ulong at = (ulong)state, size = (ulong)sizeof(CheckedReferenceState);
        if (!Range(at, size, 8))
            return false;
        // Admit the state before Zero reads it or preparation writes it. Static
        // image storage is allowed, but none of the six retained MT prefixes is.
        for (ulong i = 0; i < 6; i++)
        {
            ulong token = (&records->Object0)[i].MethodTable;
            if (!Range(token, 24, 8) || Overlap(at, size, token, 24))
                return false;
        }

        RhpNewFastHeapAccess a = *access;
        return !Overlap(at, size, (ulong)access, 104) && !Overlap(at, size, (ulong)bump, 80) && !Overlap(at, size, (ulong)control, ControlBytes) && !Overlap(at, size, (ulong)records, ObservationBytes) && !Overlap(at, size, (ulong)a.Allocator, 56) && !Overlap(at, size, (ulong)a.Nodes, 48) && !Overlap(at, size, (ulong)a.Reservations, 200) && !Overlap(at, size, (ulong)a.Heap, 216) && !Overlap(at, size, (ulong)a.Backing, 8192);
    }
}
