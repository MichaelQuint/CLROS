using OS.Std;

namespace OS.Kernel.Runtime;
/// <summary>
/// Bounded destructive diagnostic over the original collecting owner and metadata.
/// The caller supplies every external live root through one permanent receipt and
/// the optional retained static-root base. Other objects may be reclaimed, including
/// in the initial collection. Mappings/metadata must remain stable and exclusive.
/// Uses a pointer-free 24-byte class with one diagnostic payload word. This is an
/// explicit allocator probe, not proof of compiler allocation or general GC support.
/// Failure preserves partial progress; the caller must stop and diagnose it.
/// </summary>

public static unsafe class SeededAllocationChurnCollectionRootRetentionGuestProbes
{
    public const uint MaximumRounds = 64;
    private const uint Slots = 4;
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    public static SeededChurnResult Run(CollectorModuleAccess module, RhpNewFastHeapAccess heap, RhpNewFastBumpState* owner, ulong capacity, ulong methodTable, uint seed, uint rounds, RhpNewFastResult permanentRoot = default, CollectorStaticRootAccess staticRoot = default)
    {
        SeededChurnResult result = default;
        if (rounds == 0 || rounds > MaximumRounds)
            return Fail(result, SeededChurnStatus.InvalidRounds);
        var map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(Profile, module.Capture, module.Mapping, module.Owner, module.Capacity, module.Handle, module.Metadata, methodTable);
        if (!map.Succeeded || map.SeriesCount != 0 || map.Prefix.BaseSize != 24)
            return Fail(result, SeededChurnStatus.TypeRejected);
        RhpNewFastResult* roots = stackalloc RhpNewFastResult[5];
        RhpNewFastResult* batch = stackalloc RhpNewFastResult[4];
        ulong* cookies = stackalloc ulong[4];
        ulong* retired = stackalloc ulong[5];
        roots[0] = permanentRoot;
        for (uint i = 0; i < Slots; i++)
        {
            roots[i + 1] = default;
            cookies[i] = 0;
        }

        var collected = Collect(module, heap, owner, capacity, roots, staticRoot);
        result.CollectionStatus = collected.Status;
        if (!collected.Succeeded)
            return Fail(result, SeededChurnStatus.CollectionRejected);
        result.Collections = 1;
        result.InitialReclaimed = collected.ReclaimedObjects;
        result.InitialReclaimedBytes = collected.ReclaimedBytes;
        result.BaselineLiveObjects = collected.LiveObjects;
        uint baselineLive = collected.LiveObjects, liveSlots = 0, retiredCount = 0;
        ulong lastIdentity = owner->AllocationCount;
        for (uint round = 0; round < rounds; round++)
        {
            uint slot = DeterministicRandom32.Next(ref seed) % Slots;
            uint count = 1 + DeterministicRandom32.Next(ref seed) % Slots;
            RhpNewFastResult prior = roots[slot + 1];
            ulong cookie = 0;
            for (uint i = 0; i < count; i++)
            {
                var allocation = GuestTestRhpNewFast.TryAllocate(heap, owner, capacity, Profile, map.Prefix, methodTable);
                result.AllocationStatus = allocation.Status;
                if (!allocation.AllocationSucceeded)
                    return Fail(result, SeededChurnStatus.AllocationRejected);
                result.Allocations++;
                if (allocation.AllocationCount <= lastIdentity)
                    return Fail(result, SeededChurnStatus.IdentityMismatch);
                lastIdentity = allocation.AllocationCount;
                for (uint j = 0; j < retiredCount; j++)
                    if (allocation.ObjectReference == retired[j])
                        result.ReusedAddresses++;
                cookie = ((ulong)DeterministicRandom32.Next(ref seed) << 32) | result.Allocations;
                *(ulong*)(allocation.ObjectReference + 8) = cookie;
                batch[i] = allocation;
                result.Fingerprint = ((result.Fingerprint << 7) | (result.Fingerprint >> 57)) ^ cookie;
            }

            roots[slot + 1] = batch[count - 1];
            cookies[slot] = cookie;
            if (!prior.AllocationSucceeded)
                liveSlots++;
            collected = Collect(module, heap, owner, capacity, roots, staticRoot);
            result.CollectionStatus = collected.Status;
            if (!collected.Succeeded)
                return Fail(result, SeededChurnStatus.CollectionRejected);
            result.Collections++;
            uint expectedDead = count - 1 + (prior.AllocationSucceeded ? 1U : 0U);
            if (collected.LiveObjects != baselineLive + liveSlots || collected.ReclaimedObjects != expectedDead || collected.ReclaimedBytes != expectedDead * 24UL)
                return Fail(result, SeededChurnStatus.ReclamationMismatch);
            result.Reclaimed += expectedDead;
            retiredCount = 0;
            for (uint i = 0; i < count - 1; i++)
            {
                if (Valid(heap, owner, capacity, batch[i]))
                    return Fail(result, SeededChurnStatus.ReclamationMismatch);
                retired[retiredCount++] = batch[i].ObjectReference;
            }

            if (prior.AllocationSucceeded)
            {
                if (Valid(heap, owner, capacity, prior))
                    return Fail(result, SeededChurnStatus.ReclamationMismatch);
                retired[retiredCount++] = prior.ObjectReference;
            }

            for (uint i = 0; i < Slots; i++)
            {
                if (!roots[i + 1].AllocationSucceeded)
                    continue;
                if (!Valid(heap, owner, capacity, roots[i + 1]) || *(ulong*)(roots[i + 1].ObjectReference + 8) != cookies[i])
                    return Fail(result, SeededChurnStatus.RetentionMismatch);
            }

            if (permanentRoot.AllocationSucceeded && !Valid(heap, owner, capacity, permanentRoot))
                return Fail(result, SeededChurnStatus.RetentionMismatch);
            result.CompletedRounds++;
        }

        // Release only the probe's roots. The caller's permanent/static roots remain.
        for (uint i = 0; i < Slots; i++)
            roots[i + 1] = default;
        collected = Collect(module, heap, owner, capacity, roots, staticRoot);
        result.CollectionStatus = collected.Status;
        if (!collected.Succeeded)
            return Fail(result, SeededChurnStatus.CollectionRejected);
        result.Collections++;
        if (collected.LiveObjects != baselineLive || collected.ReclaimedObjects != liveSlots || collected.ReclaimedBytes != liveSlots * 24UL)
            return Fail(result, SeededChurnStatus.ReclamationMismatch);
        result.Reclaimed += liveSlots;
        result.Status = SeededChurnStatus.Success;
        return result;
    }

    private static BoundedCollectionResult Collect(CollectorModuleAccess module, RhpNewFastHeapAccess heap, RhpNewFastBumpState* owner, ulong capacity, RhpNewFastResult* roots, CollectorStaticRootAccess staticRoot) => BoundedNonMovingCollectorExplicitRootEnumeration.Collect(Profile, module, heap, owner, capacity, roots, 5, 5, staticRoot);
    private static bool Valid(RhpNewFastHeapAccess heap, RhpNewFastBumpState* owner, ulong capacity, RhpNewFastResult root) => GuestTestRhpNewFast.TryValidateRetainedAllocation(heap, owner, capacity, root) == RhpNewFastStatus.Success;
    private static SeededChurnResult Fail(SeededChurnResult result, SeededChurnStatus status)
    {
        result.Status = status;
        return result;
    }
}
