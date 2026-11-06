namespace OS.Kernel.Diagnostics;

public static class GuardedISTStacksDoubleFaultPageFaultNMI
{
    public const ulong PageSize = 4096;
    public const int StackCount = 3;
    public const byte DoubleFaultIstIndex = 1;
    public const byte PageFaultIstIndex = 2;
    public const byte NmiIstIndex = 3;
    public static GuardedIstStackArenaInitializationResult Initialize(in GuardedIstStackArenaRequest request, out GuardedIstStackAllocatorState state)
    {
        state = default;
        GuardedIstStackArenaStatus validation = ValidateRequest(request, out ulong requiredBytes);
        if (validation != GuardedIstStackArenaStatus.Success)
        {
            return GuardedIstStackArenaInitializationResult.Fail(validation, requiredBytes, request.ArenaSizeBytes);
        }

        state.Initialize(request, requiredBytes);
        return GuardedIstStackArenaInitializationResult.Pass(requiredBytes, request.ArenaSizeBytes);
    }

    public static GuardedIstStackAllocationResult Allocate(ref GuardedIstStackAllocatorState state)
    {
        if (!state.IsInitialized || state.RequiredBytes == 0 || state.ArenaSizeBytes < state.RequiredBytes || state.OwnerToken == 0)
        {
            return GuardedIstStackAllocationResult.Fail(GuardedIstStackAllocationStatus.InvalidAllocatorState, state.RequiredBytes, state.ArenaSizeBytes);
        }

        if (state.HasActiveLease)
        {
            return GuardedIstStackAllocationResult.Fail(GuardedIstStackAllocationStatus.AlreadyAllocated, state.RequiredBytes, state.ArenaSizeBytes);
        }

        if (state.LastGeneration == ulong.MaxValue)
        {
            return GuardedIstStackAllocationResult.Fail(GuardedIstStackAllocationStatus.GenerationExhausted, state.RequiredBytes, state.ArenaSizeBytes);
        }

        ulong generation = state.LastGeneration + 1;
        ulong reservationSize = state.GuardSizeBytes + state.StackSizeBytes;
        GuardedIstStackDescriptor doubleFault = CreateDescriptor(GuardedIstStackKind.DoubleFault, DoubleFaultIstIndex, state, reservationSize, 0);
        GuardedIstStackDescriptor pageFault = CreateDescriptor(GuardedIstStackKind.PageFault, PageFaultIstIndex, state, reservationSize, 1);
        GuardedIstStackDescriptor nonMaskableInterrupt = CreateDescriptor(GuardedIstStackKind.NonMaskableInterrupt, NmiIstIndex, state, reservationSize, 2);
        GuardedIstStackLease lease = new GuardedIstStackLease(state.OwnerToken, generation, state.ArenaBaseAddress, state.RequiredBytes);
        state.MarkAllocated(generation);
        return GuardedIstStackAllocationResult.Pass(doubleFault, pageFault, nonMaskableInterrupt, lease, state.RequiredBytes, state.ArenaSizeBytes);
    }

    public static GuardedIstStackReleaseResult Release(ref GuardedIstStackAllocatorState state, in GuardedIstStackLease lease)
    {
        if (!state.IsInitialized || state.OwnerToken == 0)
        {
            return GuardedIstStackReleaseResult.Fail(GuardedIstStackReleaseStatus.InvalidAllocatorState);
        }

        if (!lease.IsUsable)
        {
            return GuardedIstStackReleaseResult.Fail(GuardedIstStackReleaseStatus.InvalidLease);
        }

        if (lease.OwnerToken != state.OwnerToken)
        {
            return GuardedIstStackReleaseResult.Fail(GuardedIstStackReleaseStatus.OwnerMismatch);
        }

        if (!state.HasActiveLease)
        {
            GuardedIstStackReleaseStatus status = lease.Generation <= state.LastGeneration ? GuardedIstStackReleaseStatus.AlreadyReleased : GuardedIstStackReleaseStatus.StaleOwnership;
            return GuardedIstStackReleaseResult.Fail(status);
        }

        if (lease.Generation != state.ActiveGeneration || lease.ArenaBaseAddress != state.ArenaBaseAddress || lease.RequiredBytes != state.RequiredBytes)
        {
            return GuardedIstStackReleaseResult.Fail(GuardedIstStackReleaseStatus.StaleOwnership);
        }

        ulong releasedGeneration = state.ActiveGeneration;
        state.MarkReleased();
        return GuardedIstStackReleaseResult.Pass(releasedGeneration);
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong upper = address >> 48;
        bool bit47Set = ((address >> 47) & 1UL) != 0;
        return bit47Set ? upper == 0xffffUL : upper == 0;
    }

    private static GuardedIstStackArenaStatus ValidateRequest(in GuardedIstStackArenaRequest request, out ulong requiredBytes)
    {
        requiredBytes = 0;
        if (request.ArenaBaseAddress == 0)
        {
            return GuardedIstStackArenaStatus.NullArenaBaseAddress;
        }

        if (!IsCanonicalX64Address(request.ArenaBaseAddress))
        {
            return GuardedIstStackArenaStatus.NonCanonicalArenaBaseAddress;
        }

        if (!IsPageAligned(request.ArenaBaseAddress))
        {
            return GuardedIstStackArenaStatus.UnalignedArenaBaseAddress;
        }

        if (!IsPageMultiple(request.ArenaSizeBytes))
        {
            return GuardedIstStackArenaStatus.InvalidArenaSize;
        }

        if (!IsPageMultiple(request.StackSizeBytes))
        {
            return GuardedIstStackArenaStatus.InvalidStackSize;
        }

        if (!IsPageMultiple(request.GuardSizeBytes))
        {
            return GuardedIstStackArenaStatus.InvalidGuardSize;
        }

        if (request.OwnerToken == 0)
        {
            return GuardedIstStackArenaStatus.NullOwnerToken;
        }

        if (!request.StackWritable)
        {
            return GuardedIstStackArenaStatus.StackMustBeWritable;
        }

        if (request.StackExecutable)
        {
            return GuardedIstStackArenaStatus.ProhibitedWritableExecutable;
        }

        if (!TryAdd(request.GuardSizeBytes, request.StackSizeBytes, out ulong reservationSize) || !TryMultiply(reservationSize, StackCount, out requiredBytes))
        {
            return GuardedIstStackArenaStatus.SizeOverflow;
        }

        if (request.ArenaSizeBytes < requiredBytes)
        {
            return GuardedIstStackArenaStatus.ArenaExhausted;
        }

        if (!TryAdd(request.ArenaBaseAddress, request.ArenaSizeBytes, out ulong arenaEnd))
        {
            return GuardedIstStackArenaStatus.ArenaEndAddressWrap;
        }

        ulong arenaLastAddress = arenaEnd - 1;
        if (!IsCanonicalX64Address(arenaLastAddress) || !IsSameCanonicalHalf(request.ArenaBaseAddress, arenaLastAddress))
        {
            return GuardedIstStackArenaStatus.NonCanonicalArenaEndAddress;
        }

        bool hasReservedBase = request.ReservedBaseAddress != 0;
        bool hasReservedSize = request.ReservedSizeBytes != 0;
        if (hasReservedBase != hasReservedSize)
        {
            return GuardedIstStackArenaStatus.InvalidReservedRange;
        }

        if (!hasReservedBase)
        {
            return GuardedIstStackArenaStatus.Success;
        }

        if (!IsCanonicalX64Address(request.ReservedBaseAddress) || !IsPageAligned(request.ReservedBaseAddress) || !IsPageMultiple(request.ReservedSizeBytes))
        {
            return GuardedIstStackArenaStatus.InvalidReservedRange;
        }

        if (!TryAdd(request.ReservedBaseAddress, request.ReservedSizeBytes, out ulong reservedEnd))
        {
            return GuardedIstStackArenaStatus.ReservedRangeAddressWrap;
        }

        ulong reservedLastAddress = reservedEnd - 1;
        if (!IsCanonicalX64Address(reservedLastAddress) || !IsSameCanonicalHalf(request.ReservedBaseAddress, reservedLastAddress))
        {
            return GuardedIstStackArenaStatus.InvalidReservedRange;
        }

        ulong layoutEnd = request.ArenaBaseAddress + requiredBytes;
        if (RangesOverlap(request.ArenaBaseAddress, layoutEnd, request.ReservedBaseAddress, reservedEnd))
        {
            return GuardedIstStackArenaStatus.ReservedRangeOverlap;
        }

        return GuardedIstStackArenaStatus.Success;
    }

    private static GuardedIstStackDescriptor CreateDescriptor(GuardedIstStackKind kind, byte istIndex, in GuardedIstStackAllocatorState state, ulong reservationSize, ulong ordinal)
    {
        ulong guardBaseAddress = state.ArenaBaseAddress + (reservationSize * ordinal);
        ulong stackBaseAddress = guardBaseAddress + state.GuardSizeBytes;
        ulong stackTopAddress = stackBaseAddress + state.StackSizeBytes;
        return new GuardedIstStackDescriptor(kind, istIndex, guardBaseAddress, state.GuardSizeBytes, stackBaseAddress, state.StackSizeBytes, stackTopAddress);
    }

    private static bool IsPageAligned(ulong value)
    {
        return (value & (PageSize - 1)) == 0;
    }

    private static bool IsSameCanonicalHalf(ulong first, ulong second)
    {
        return ((first >> 47) & 1UL) == ((second >> 47) & 1UL);
    }

    private static bool IsPageMultiple(ulong value)
    {
        return value != 0 && IsPageAligned(value);
    }

    private static bool TryAdd(ulong first, ulong second, out ulong result)
    {
        result = first + second;
        return result >= first;
    }

    private static bool TryMultiply(ulong value, ulong multiplier, out ulong result)
    {
        if (value != 0 && multiplier > ulong.MaxValue / value)
        {
            result = 0;
            return false;
        }

        result = value * multiplier;
        return true;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
