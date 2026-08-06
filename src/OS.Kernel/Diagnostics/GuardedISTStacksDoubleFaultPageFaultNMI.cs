namespace OS.Kernel.Diagnostics;

public enum GuardedIstStackKind : byte
{
    DoubleFault = 1,
    PageFault = 2,
    NonMaskableInterrupt = 3
}

public enum GuardedIstStackArenaStatus : byte
{
    Success = 0,
    NullArenaBaseAddress = 1,
    NonCanonicalArenaBaseAddress = 2,
    UnalignedArenaBaseAddress = 3,
    InvalidArenaSize = 4,
    InvalidStackSize = 5,
    InvalidGuardSize = 6,
    NullOwnerToken = 7,
    StackMustBeWritable = 8,
    ProhibitedWritableExecutable = 9,
    SizeOverflow = 10,
    ArenaEndAddressWrap = 11,
    NonCanonicalArenaEndAddress = 12,
    ArenaExhausted = 13,
    InvalidReservedRange = 14,
    ReservedRangeAddressWrap = 15,
    ReservedRangeOverlap = 16
}

public enum GuardedIstStackAllocationStatus : byte
{
    Success = 0,
    InvalidAllocatorState = 1,
    AlreadyAllocated = 2,
    GenerationExhausted = 3
}

public enum GuardedIstStackReleaseStatus : byte
{
    Success = 0,
    InvalidAllocatorState = 1,
    InvalidLease = 2,
    OwnerMismatch = 3,
    AlreadyReleased = 4,
    StaleOwnership = 5
}

public readonly struct GuardedIstStackArenaRequest
{
    public GuardedIstStackArenaRequest(
        ulong arenaBaseAddress,
        ulong arenaSizeBytes,
        ulong stackSizeBytes,
        ulong guardSizeBytes,
        ulong ownerToken,
        bool stackWritable,
        bool stackExecutable,
        ulong reservedBaseAddress = 0,
        ulong reservedSizeBytes = 0)
    {
        ArenaBaseAddress = arenaBaseAddress;
        ArenaSizeBytes = arenaSizeBytes;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
        OwnerToken = ownerToken;
        StackWritable = stackWritable;
        StackExecutable = stackExecutable;
        ReservedBaseAddress = reservedBaseAddress;
        ReservedSizeBytes = reservedSizeBytes;
    }

    public ulong ArenaBaseAddress { get; }

    public ulong ArenaSizeBytes { get; }

    public ulong StackSizeBytes { get; }

    public ulong GuardSizeBytes { get; }

    public ulong OwnerToken { get; }

    public bool StackWritable { get; }

    public bool StackExecutable { get; }

    public ulong ReservedBaseAddress { get; }

    public ulong ReservedSizeBytes { get; }
}

public readonly struct GuardedIstStackArenaInitializationResult
{
    private GuardedIstStackArenaInitializationResult(
        bool isInitialized,
        GuardedIstStackArenaStatus status,
        ulong requiredBytes,
        ulong availableBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
    }

    public bool IsInitialized { get; }

    public GuardedIstStackArenaStatus Status { get; }

    public ulong RequiredBytes { get; }

    public ulong AvailableBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == GuardedIstStackArenaStatus.Success &&
        RequiredBytes != 0 &&
        AvailableBytes >= RequiredBytes;

    internal static GuardedIstStackArenaInitializationResult Pass(
        ulong requiredBytes,
        ulong availableBytes)
    {
        return new GuardedIstStackArenaInitializationResult(
            true,
            GuardedIstStackArenaStatus.Success,
            requiredBytes,
            availableBytes);
    }

    internal static GuardedIstStackArenaInitializationResult Fail(
        GuardedIstStackArenaStatus status,
        ulong requiredBytes,
        ulong availableBytes)
    {
        return new GuardedIstStackArenaInitializationResult(
            true,
            status,
            requiredBytes,
            availableBytes);
    }
}

public readonly struct GuardedIstStackDescriptor
{
    internal GuardedIstStackDescriptor(
        GuardedIstStackKind kind,
        byte istIndex,
        ulong guardBaseAddress,
        ulong guardSizeBytes,
        ulong stackBaseAddress,
        ulong stackSizeBytes,
        ulong stackTopAddress)
    {
        IsInitialized = true;
        Kind = kind;
        IstIndex = istIndex;
        GuardBaseAddress = guardBaseAddress;
        GuardSizeBytes = guardSizeBytes;
        StackBaseAddress = stackBaseAddress;
        StackSizeBytes = stackSizeBytes;
        StackTopAddress = stackTopAddress;
        GuardPagePresent = false;
        StackWritable = true;
        StackExecutable = false;
    }

    public bool IsInitialized { get; }

    public GuardedIstStackKind Kind { get; }

    public byte IstIndex { get; }

    public ulong GuardBaseAddress { get; }

    public ulong GuardSizeBytes { get; }

    public ulong StackBaseAddress { get; }

    public ulong StackSizeBytes { get; }

    public ulong StackTopAddress { get; }

    public bool GuardPagePresent { get; }

    public bool StackWritable { get; }

    public bool StackExecutable { get; }

    public bool IsUsable =>
        IsInitialized &&
        IstIndex >= GuardedISTStacksDoubleFaultPageFaultNMI.DoubleFaultIstIndex &&
        IstIndex <= GuardedISTStacksDoubleFaultPageFaultNMI.NmiIstIndex &&
        GuardBaseAddress != 0 &&
        GuardSizeBytes != 0 &&
        StackBaseAddress == GuardBaseAddress + GuardSizeBytes &&
        StackSizeBytes != 0 &&
        StackTopAddress == StackBaseAddress + StackSizeBytes &&
        !GuardPagePresent &&
        StackWritable &&
        !StackExecutable;
}

public readonly struct GuardedIstStackLease
{
    internal GuardedIstStackLease(
        ulong ownerToken,
        ulong generation,
        ulong arenaBaseAddress,
        ulong requiredBytes)
    {
        IsInitialized = true;
        OwnerToken = ownerToken;
        Generation = generation;
        ArenaBaseAddress = arenaBaseAddress;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }

    public ulong OwnerToken { get; }

    public ulong Generation { get; }

    public ulong ArenaBaseAddress { get; }

    public ulong RequiredBytes { get; }

    public bool IsUsable =>
        IsInitialized &&
        OwnerToken != 0 &&
        Generation != 0 &&
        ArenaBaseAddress != 0 &&
        RequiredBytes != 0;
}

public struct GuardedIstStackAllocatorState
{
    public bool IsInitialized { get; private set; }

    public ulong ArenaBaseAddress { get; private set; }

    public ulong ArenaSizeBytes { get; private set; }

    public ulong StackSizeBytes { get; private set; }

    public ulong GuardSizeBytes { get; private set; }

    public ulong RequiredBytes { get; private set; }

    public ulong OwnerToken { get; private set; }

    public ulong LastGeneration { get; private set; }

    public ulong ActiveGeneration { get; private set; }

    public bool HasActiveLease => IsInitialized && ActiveGeneration != 0;

    internal void Initialize(
        in GuardedIstStackArenaRequest request,
        ulong requiredBytes)
    {
        IsInitialized = true;
        ArenaBaseAddress = request.ArenaBaseAddress;
        ArenaSizeBytes = request.ArenaSizeBytes;
        StackSizeBytes = request.StackSizeBytes;
        GuardSizeBytes = request.GuardSizeBytes;
        RequiredBytes = requiredBytes;
        OwnerToken = request.OwnerToken;
        LastGeneration = 0;
        ActiveGeneration = 0;
    }

    internal void MarkAllocated(ulong generation)
    {
        LastGeneration = generation;
        ActiveGeneration = generation;
    }

    internal void MarkReleased()
    {
        ActiveGeneration = 0;
    }
}

public readonly struct GuardedIstStackAllocationResult
{
    private GuardedIstStackAllocationResult(
        bool isInitialized,
        GuardedIstStackAllocationStatus status,
        GuardedIstStackDescriptor doubleFault,
        GuardedIstStackDescriptor pageFault,
        GuardedIstStackDescriptor nonMaskableInterrupt,
        GuardedIstStackLease lease,
        ulong requiredBytes,
        ulong availableBytes,
        bool stateMutated)
    {
        IsInitialized = isInitialized;
        Status = status;
        DoubleFault = doubleFault;
        PageFault = pageFault;
        NonMaskableInterrupt = nonMaskableInterrupt;
        Lease = lease;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        StateMutated = stateMutated;
    }

    public bool IsInitialized { get; }

    public GuardedIstStackAllocationStatus Status { get; }

    public GuardedIstStackDescriptor DoubleFault { get; }

    public GuardedIstStackDescriptor PageFault { get; }

    public GuardedIstStackDescriptor NonMaskableInterrupt { get; }

    public GuardedIstStackLease Lease { get; }

    public ulong RequiredBytes { get; }

    public ulong AvailableBytes { get; }

    public bool StateMutated { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == GuardedIstStackAllocationStatus.Success &&
        DoubleFault.IsUsable &&
        PageFault.IsUsable &&
        NonMaskableInterrupt.IsUsable &&
        Lease.IsUsable &&
        RequiredBytes != 0 &&
        AvailableBytes >= RequiredBytes &&
        StateMutated;

    internal static GuardedIstStackAllocationResult Pass(
        GuardedIstStackDescriptor doubleFault,
        GuardedIstStackDescriptor pageFault,
        GuardedIstStackDescriptor nonMaskableInterrupt,
        GuardedIstStackLease lease,
        ulong requiredBytes,
        ulong availableBytes)
    {
        return new GuardedIstStackAllocationResult(
            true,
            GuardedIstStackAllocationStatus.Success,
            doubleFault,
            pageFault,
            nonMaskableInterrupt,
            lease,
            requiredBytes,
            availableBytes,
            true);
    }

    internal static GuardedIstStackAllocationResult Fail(
        GuardedIstStackAllocationStatus status,
        ulong requiredBytes,
        ulong availableBytes)
    {
        return new GuardedIstStackAllocationResult(
            true,
            status,
            default(GuardedIstStackDescriptor),
            default(GuardedIstStackDescriptor),
            default(GuardedIstStackDescriptor),
            default(GuardedIstStackLease),
            requiredBytes,
            availableBytes,
            false);
    }
}

public readonly struct GuardedIstStackReleaseResult
{
    private GuardedIstStackReleaseResult(
        bool isInitialized,
        GuardedIstStackReleaseStatus status,
        ulong releasedGeneration,
        bool stateMutated)
    {
        IsInitialized = isInitialized;
        Status = status;
        ReleasedGeneration = releasedGeneration;
        StateMutated = stateMutated;
    }

    public bool IsInitialized { get; }

    public GuardedIstStackReleaseStatus Status { get; }

    public ulong ReleasedGeneration { get; }

    public bool StateMutated { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == GuardedIstStackReleaseStatus.Success &&
        ReleasedGeneration != 0 &&
        StateMutated;

    internal static GuardedIstStackReleaseResult Pass(ulong releasedGeneration)
    {
        return new GuardedIstStackReleaseResult(
            true,
            GuardedIstStackReleaseStatus.Success,
            releasedGeneration,
            true);
    }

    internal static GuardedIstStackReleaseResult Fail(
        GuardedIstStackReleaseStatus status)
    {
        return new GuardedIstStackReleaseResult(
            true,
            status,
            0,
            false);
    }
}

public static class GuardedISTStacksDoubleFaultPageFaultNMI
{
    public const ulong PageSize = 4096;
    public const int StackCount = 3;

    public const byte DoubleFaultIstIndex = 1;
    public const byte PageFaultIstIndex = 2;
    public const byte NmiIstIndex = 3;

    public static GuardedIstStackArenaInitializationResult Initialize(
        in GuardedIstStackArenaRequest request,
        out GuardedIstStackAllocatorState state)
    {
        state = default;

        GuardedIstStackArenaStatus validation = ValidateRequest(
            request,
            out ulong requiredBytes);
        if (validation != GuardedIstStackArenaStatus.Success)
        {
            return GuardedIstStackArenaInitializationResult.Fail(
                validation,
                requiredBytes,
                request.ArenaSizeBytes);
        }

        state.Initialize(request, requiredBytes);
        return GuardedIstStackArenaInitializationResult.Pass(
            requiredBytes,
            request.ArenaSizeBytes);
    }

    public static GuardedIstStackAllocationResult Allocate(
        ref GuardedIstStackAllocatorState state)
    {
        if (!state.IsInitialized ||
            state.RequiredBytes == 0 ||
            state.ArenaSizeBytes < state.RequiredBytes ||
            state.OwnerToken == 0)
        {
            return GuardedIstStackAllocationResult.Fail(
                GuardedIstStackAllocationStatus.InvalidAllocatorState,
                state.RequiredBytes,
                state.ArenaSizeBytes);
        }

        if (state.HasActiveLease)
        {
            return GuardedIstStackAllocationResult.Fail(
                GuardedIstStackAllocationStatus.AlreadyAllocated,
                state.RequiredBytes,
                state.ArenaSizeBytes);
        }

        if (state.LastGeneration == ulong.MaxValue)
        {
            return GuardedIstStackAllocationResult.Fail(
                GuardedIstStackAllocationStatus.GenerationExhausted,
                state.RequiredBytes,
                state.ArenaSizeBytes);
        }

        ulong generation = state.LastGeneration + 1;
        ulong reservationSize = state.GuardSizeBytes + state.StackSizeBytes;

        GuardedIstStackDescriptor doubleFault = CreateDescriptor(
            GuardedIstStackKind.DoubleFault,
            DoubleFaultIstIndex,
            state,
            reservationSize,
            0);
        GuardedIstStackDescriptor pageFault = CreateDescriptor(
            GuardedIstStackKind.PageFault,
            PageFaultIstIndex,
            state,
            reservationSize,
            1);
        GuardedIstStackDescriptor nonMaskableInterrupt = CreateDescriptor(
            GuardedIstStackKind.NonMaskableInterrupt,
            NmiIstIndex,
            state,
            reservationSize,
            2);
        GuardedIstStackLease lease = new GuardedIstStackLease(
            state.OwnerToken,
            generation,
            state.ArenaBaseAddress,
            state.RequiredBytes);

        state.MarkAllocated(generation);
        return GuardedIstStackAllocationResult.Pass(
            doubleFault,
            pageFault,
            nonMaskableInterrupt,
            lease,
            state.RequiredBytes,
            state.ArenaSizeBytes);
    }

    public static GuardedIstStackReleaseResult Release(
        ref GuardedIstStackAllocatorState state,
        in GuardedIstStackLease lease)
    {
        if (!state.IsInitialized || state.OwnerToken == 0)
        {
            return GuardedIstStackReleaseResult.Fail(
                GuardedIstStackReleaseStatus.InvalidAllocatorState);
        }

        if (!lease.IsUsable)
        {
            return GuardedIstStackReleaseResult.Fail(
                GuardedIstStackReleaseStatus.InvalidLease);
        }

        if (lease.OwnerToken != state.OwnerToken)
        {
            return GuardedIstStackReleaseResult.Fail(
                GuardedIstStackReleaseStatus.OwnerMismatch);
        }

        if (!state.HasActiveLease)
        {
            GuardedIstStackReleaseStatus status =
                lease.Generation <= state.LastGeneration
                    ? GuardedIstStackReleaseStatus.AlreadyReleased
                    : GuardedIstStackReleaseStatus.StaleOwnership;
            return GuardedIstStackReleaseResult.Fail(status);
        }

        if (lease.Generation != state.ActiveGeneration ||
            lease.ArenaBaseAddress != state.ArenaBaseAddress ||
            lease.RequiredBytes != state.RequiredBytes)
        {
            return GuardedIstStackReleaseResult.Fail(
                GuardedIstStackReleaseStatus.StaleOwnership);
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

    private static GuardedIstStackArenaStatus ValidateRequest(
        in GuardedIstStackArenaRequest request,
        out ulong requiredBytes)
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

        if (!TryAdd(
                request.GuardSizeBytes,
                request.StackSizeBytes,
                out ulong reservationSize) ||
            !TryMultiply(reservationSize, StackCount, out requiredBytes))
        {
            return GuardedIstStackArenaStatus.SizeOverflow;
        }

        if (request.ArenaSizeBytes < requiredBytes)
        {
            return GuardedIstStackArenaStatus.ArenaExhausted;
        }

        if (!TryAdd(
                request.ArenaBaseAddress,
                request.ArenaSizeBytes,
                out ulong arenaEnd))
        {
            return GuardedIstStackArenaStatus.ArenaEndAddressWrap;
        }

        ulong arenaLastAddress = arenaEnd - 1;
        if (!IsCanonicalX64Address(arenaLastAddress) ||
            !IsSameCanonicalHalf(request.ArenaBaseAddress, arenaLastAddress))
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

        if (!IsCanonicalX64Address(request.ReservedBaseAddress) ||
            !IsPageAligned(request.ReservedBaseAddress) ||
            !IsPageMultiple(request.ReservedSizeBytes))
        {
            return GuardedIstStackArenaStatus.InvalidReservedRange;
        }

        if (!TryAdd(
                request.ReservedBaseAddress,
                request.ReservedSizeBytes,
                out ulong reservedEnd))
        {
            return GuardedIstStackArenaStatus.ReservedRangeAddressWrap;
        }

        ulong reservedLastAddress = reservedEnd - 1;
        if (!IsCanonicalX64Address(reservedLastAddress) ||
            !IsSameCanonicalHalf(request.ReservedBaseAddress, reservedLastAddress))
        {
            return GuardedIstStackArenaStatus.InvalidReservedRange;
        }

        ulong layoutEnd = request.ArenaBaseAddress + requiredBytes;
        if (RangesOverlap(
                request.ArenaBaseAddress,
                layoutEnd,
                request.ReservedBaseAddress,
                reservedEnd))
        {
            return GuardedIstStackArenaStatus.ReservedRangeOverlap;
        }

        return GuardedIstStackArenaStatus.Success;
    }

    private static GuardedIstStackDescriptor CreateDescriptor(
        GuardedIstStackKind kind,
        byte istIndex,
        in GuardedIstStackAllocatorState state,
        ulong reservationSize,
        ulong ordinal)
    {
        ulong guardBaseAddress =
            state.ArenaBaseAddress + (reservationSize * ordinal);
        ulong stackBaseAddress = guardBaseAddress + state.GuardSizeBytes;
        ulong stackTopAddress = stackBaseAddress + state.StackSizeBytes;

        return new GuardedIstStackDescriptor(
            kind,
            istIndex,
            guardBaseAddress,
            state.GuardSizeBytes,
            stackBaseAddress,
            state.StackSizeBytes,
            stackTopAddress);
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

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
