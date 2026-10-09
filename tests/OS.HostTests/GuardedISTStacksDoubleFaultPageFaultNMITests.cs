using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class GuardedISTStacksDoubleFaultPageFaultNMITests
{
    private const ulong ArenaBaseAddress = 0xffff_8000_0040_0000UL;
    private const ulong ArenaSizeBytes = 10 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize;
    private const ulong StackSizeBytes = 2 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize;
    private const ulong GuardSizeBytes = GuardedISTStacksDoubleFaultPageFaultNMI.PageSize;
    private const ulong OwnerToken = 0x4f53_4953_5430_3038UL;
    private const ulong RequiredBytes = 9 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize;

    private const string HostReadyMarker =
        "CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 pageFaultIst=2 " +
        "nmiIst=3 writable=true executable=false allocationFree=true " +
        "guestAllocation=false guestTss=false guestIdt=false";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.008");

        int failures = 0;
        Run("m02.008 allocates exact guarded ist layout", TestExactLayout, ref failures);
        Run("m02.008 rejects zero and misaligned addresses", TestAddressValidation, ref failures);
        Run("m02.008 rejects overflow wrap and exhaustion", TestSizeAndRangeValidation, ref failures);
        Run("m02.008 rejects reserved range overlap", TestReservedRangeValidation, ref failures);
        Run("m02.008 enforces writable non executable stacks", TestProtectionValidation, ref failures);
        Run("m02.008 rejects stale ownership and double release", TestLeaseLifecycle, ref failures);
        Run("m02.008 hot path allocates no managed bytes", TestHotPathDoesNotAllocate, ref failures);
        Run("m02.008 defaults and source boundary fail closed", TestDefaultsAndSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.008 host-tests failures=" + failures);
            throw new InvalidOperationException(
                "Guarded IST stack host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS m02.008");
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestExactLayout()
    {
        GuardedIstStackArenaInitializationResult initialization =
            GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(
                CreateValidRequest(),
                out GuardedIstStackAllocatorState state);
        GuardedIstStackAllocationResult allocation =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);

        ulong reservationSize = GuardSizeBytes + StackSizeBytes;
        return initialization.Succeeded &&
            initialization.Status == GuardedIstStackArenaStatus.Success &&
            initialization.RequiredBytes == RequiredBytes &&
            initialization.AvailableBytes == ArenaSizeBytes &&
            state.IsInitialized &&
            state.HasActiveLease &&
            state.ActiveGeneration == 1 &&
            state.LastGeneration == 1 &&
            allocation.Succeeded &&
            allocation.Status == GuardedIstStackAllocationStatus.Success &&
            allocation.RequiredBytes == RequiredBytes &&
            allocation.AvailableBytes == ArenaSizeBytes &&
            allocation.StateMutated &&
            IsExactDescriptor(
                allocation.DoubleFault,
                GuardedIstStackKind.DoubleFault,
                GuardedISTStacksDoubleFaultPageFaultNMI.DoubleFaultIstIndex,
                ArenaBaseAddress) &&
            IsExactDescriptor(
                allocation.PageFault,
                GuardedIstStackKind.PageFault,
                GuardedISTStacksDoubleFaultPageFaultNMI.PageFaultIstIndex,
                ArenaBaseAddress + reservationSize) &&
            IsExactDescriptor(
                allocation.NonMaskableInterrupt,
                GuardedIstStackKind.NonMaskableInterrupt,
                GuardedISTStacksDoubleFaultPageFaultNMI.NmiIstIndex,
                ArenaBaseAddress + (2 * reservationSize)) &&
            allocation.DoubleFault.StackTopAddress == allocation.PageFault.GuardBaseAddress &&
            allocation.PageFault.StackTopAddress ==
                allocation.NonMaskableInterrupt.GuardBaseAddress &&
            allocation.NonMaskableInterrupt.StackTopAddress ==
                ArenaBaseAddress + RequiredBytes &&
            allocation.Lease.IsUsable &&
            allocation.Lease.OwnerToken == OwnerToken &&
            allocation.Lease.Generation == 1 &&
            allocation.Lease.ArenaBaseAddress == ArenaBaseAddress &&
            allocation.Lease.RequiredBytes == RequiredBytes &&
            GuardedISTStacksDoubleFaultPageFaultNMI.StackCount == 3;
    }

    private static bool TestAddressValidation()
    {
        GuardedIstStackArenaInitializationResult nullBase = InitializeWith(
            out GuardedIstStackAllocatorState nullBaseState,
            arenaBaseAddress: 0);
        GuardedIstStackArenaInitializationResult nonCanonicalBase = InitializeWith(
            out GuardedIstStackAllocatorState nonCanonicalState,
            arenaBaseAddress: 0x0000_8000_0000_0000UL);
        GuardedIstStackArenaInitializationResult unalignedBase = InitializeWith(
            out GuardedIstStackAllocatorState unalignedState,
            arenaBaseAddress: ArenaBaseAddress + 8);
        GuardedIstStackArenaInitializationResult nullOwner = InitializeWith(
            out GuardedIstStackAllocatorState nullOwnerState,
            ownerToken: 0);

        return IsAtomicInitializationFailure(
                nullBase,
                GuardedIstStackArenaStatus.NullArenaBaseAddress,
                nullBaseState) &&
            IsAtomicInitializationFailure(
                nonCanonicalBase,
                GuardedIstStackArenaStatus.NonCanonicalArenaBaseAddress,
                nonCanonicalState) &&
            IsAtomicInitializationFailure(
                unalignedBase,
                GuardedIstStackArenaStatus.UnalignedArenaBaseAddress,
                unalignedState) &&
            IsAtomicInitializationFailure(
                nullOwner,
                GuardedIstStackArenaStatus.NullOwnerToken,
                nullOwnerState) &&
            GuardedISTStacksDoubleFaultPageFaultNMI.IsCanonicalX64Address(
                ArenaBaseAddress) &&
            !GuardedISTStacksDoubleFaultPageFaultNMI.IsCanonicalX64Address(
                0xffff_7fff_ffff_ffffUL);
    }

    private static bool TestSizeAndRangeValidation()
    {
        GuardedIstStackArenaInitializationResult invalidArena = InitializeWith(
            out GuardedIstStackAllocatorState invalidArenaState,
            arenaSizeBytes: 0);
        GuardedIstStackArenaInitializationResult invalidStack = InitializeWith(
            out GuardedIstStackAllocatorState invalidStackState,
            stackSizeBytes: 2048);
        GuardedIstStackArenaInitializationResult invalidGuard = InitializeWith(
            out GuardedIstStackAllocatorState invalidGuardState,
            guardSizeBytes: 0);
        GuardedIstStackArenaInitializationResult sizeOverflow = InitializeWith(
            out GuardedIstStackAllocatorState sizeOverflowState,
            arenaBaseAddress: 0x0000_0000_0020_0000UL,
            arenaSizeBytes: 0xffff_ffff_ffff_f000UL,
            stackSizeBytes: 0xffff_ffff_ffff_f000UL,
            guardSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult endWrap = InitializeWith(
            out GuardedIstStackAllocatorState endWrapState,
            arenaBaseAddress: 0xffff_ffff_ffff_f000UL,
            arenaSizeBytes: 16 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            stackSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            guardSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult nonCanonicalEnd = InitializeWith(
            out GuardedIstStackAllocatorState nonCanonicalEndState,
            arenaBaseAddress: 0x0000_7fff_ffff_f000UL,
            arenaSizeBytes: 16 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            stackSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            guardSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult spansCanonicalGap = InitializeWith(
            out GuardedIstStackAllocatorState spansCanonicalGapState,
            arenaBaseAddress: 0x0000_0000_0020_0000UL,
            arenaSizeBytes: 0xffff_8000_0000_0000UL,
            stackSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            guardSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult exhausted = InitializeWith(
            out GuardedIstStackAllocatorState exhaustedState,
            arenaSizeBytes: 5 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            stackSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize,
            guardSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);

        return IsAtomicInitializationFailure(
                invalidArena,
                GuardedIstStackArenaStatus.InvalidArenaSize,
                invalidArenaState) &&
            IsAtomicInitializationFailure(
                invalidStack,
                GuardedIstStackArenaStatus.InvalidStackSize,
                invalidStackState) &&
            IsAtomicInitializationFailure(
                invalidGuard,
                GuardedIstStackArenaStatus.InvalidGuardSize,
                invalidGuardState) &&
            IsAtomicInitializationFailure(
                sizeOverflow,
                GuardedIstStackArenaStatus.SizeOverflow,
                sizeOverflowState) &&
            IsAtomicInitializationFailure(
                endWrap,
                GuardedIstStackArenaStatus.ArenaEndAddressWrap,
                endWrapState) &&
            IsAtomicInitializationFailure(
                nonCanonicalEnd,
                GuardedIstStackArenaStatus.NonCanonicalArenaEndAddress,
                nonCanonicalEndState) &&
            IsAtomicInitializationFailure(
                spansCanonicalGap,
                GuardedIstStackArenaStatus.NonCanonicalArenaEndAddress,
                spansCanonicalGapState) &&
            IsAtomicInitializationFailure(
                exhausted,
                GuardedIstStackArenaStatus.ArenaExhausted,
                exhaustedState);
    }

    private static bool TestReservedRangeValidation()
    {
        GuardedIstStackArenaInitializationResult incompleteRange = InitializeWith(
            out GuardedIstStackAllocatorState incompleteRangeState,
            reservedBaseAddress: ArenaBaseAddress + RequiredBytes,
            reservedSizeBytes: 0);
        GuardedIstStackArenaInitializationResult wrappingRange = InitializeWith(
            out GuardedIstStackAllocatorState wrappingRangeState,
            reservedBaseAddress: 0xffff_ffff_ffff_f000UL,
            reservedSizeBytes: 2 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult spanningRange = InitializeWith(
            out GuardedIstStackAllocatorState spanningRangeState,
            reservedBaseAddress: 0x0000_0000_0030_0000UL,
            reservedSizeBytes: 0xffff_8000_0000_0000UL);
        GuardedIstStackArenaInitializationResult overlap = InitializeWith(
            out GuardedIstStackAllocatorState overlapState,
            reservedBaseAddress: ArenaBaseAddress +
                (3 * GuardedISTStacksDoubleFaultPageFaultNMI.PageSize),
            reservedSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);
        GuardedIstStackArenaInitializationResult adjacent = InitializeWith(
            out GuardedIstStackAllocatorState adjacentState,
            reservedBaseAddress: ArenaBaseAddress + RequiredBytes,
            reservedSizeBytes: GuardedISTStacksDoubleFaultPageFaultNMI.PageSize);

        return IsAtomicInitializationFailure(
                incompleteRange,
                GuardedIstStackArenaStatus.InvalidReservedRange,
                incompleteRangeState) &&
            IsAtomicInitializationFailure(
                wrappingRange,
                GuardedIstStackArenaStatus.ReservedRangeAddressWrap,
                wrappingRangeState) &&
            IsAtomicInitializationFailure(
                spanningRange,
                GuardedIstStackArenaStatus.InvalidReservedRange,
                spanningRangeState) &&
            IsAtomicInitializationFailure(
                overlap,
                GuardedIstStackArenaStatus.ReservedRangeOverlap,
                overlapState) &&
            adjacent.Succeeded &&
            adjacentState.IsInitialized &&
            adjacentState.RequiredBytes == RequiredBytes;
    }

    private static bool TestProtectionValidation()
    {
        GuardedIstStackArenaInitializationResult readOnly = InitializeWith(
            out GuardedIstStackAllocatorState readOnlyState,
            stackWritable: false,
            stackExecutable: false);
        GuardedIstStackArenaInitializationResult writableExecutable = InitializeWith(
            out GuardedIstStackAllocatorState writableExecutableState,
            stackWritable: true,
            stackExecutable: true);

        return IsAtomicInitializationFailure(
                readOnly,
                GuardedIstStackArenaStatus.StackMustBeWritable,
                readOnlyState) &&
            IsAtomicInitializationFailure(
                writableExecutable,
                GuardedIstStackArenaStatus.ProhibitedWritableExecutable,
                writableExecutableState);
    }

    private static bool TestLeaseLifecycle()
    {
        GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(
            CreateValidRequest(),
            out GuardedIstStackAllocatorState state);
        GuardedIstStackAllocationResult first =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);
        GuardedIstStackAllocationResult duplicate =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);

        GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(
            CreateValidRequest(ownerToken: OwnerToken + 1),
            out GuardedIstStackAllocatorState foreignState);
        GuardedIstStackAllocationResult foreign =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref foreignState);
        GuardedIstStackReleaseResult ownerMismatch =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                foreign.Lease);

        GuardedIstStackReleaseResult released =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                first.Lease);
        GuardedIstStackReleaseResult doubleRelease =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                first.Lease);
        GuardedIstStackAllocationResult second =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);
        GuardedIstStackReleaseResult stale =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                first.Lease);
        GuardedIstStackReleaseResult invalidLease =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                default(GuardedIstStackLease));
        GuardedIstStackReleaseResult secondReleased =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                second.Lease);

        GuardedIstStackAllocatorState defaultState = default;
        GuardedIstStackAllocationResult invalidStateAllocation =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref defaultState);
        GuardedIstStackReleaseResult invalidStateRelease =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref defaultState,
                first.Lease);

        return first.Succeeded &&
            !duplicate.Succeeded &&
            duplicate.Status == GuardedIstStackAllocationStatus.AlreadyAllocated &&
            !duplicate.StateMutated &&
            state.LastGeneration == 2 &&
            foreign.Succeeded &&
            !ownerMismatch.Succeeded &&
            ownerMismatch.Status == GuardedIstStackReleaseStatus.OwnerMismatch &&
            !ownerMismatch.StateMutated &&
            released.Succeeded &&
            released.ReleasedGeneration == 1 &&
            !doubleRelease.Succeeded &&
            doubleRelease.Status == GuardedIstStackReleaseStatus.AlreadyReleased &&
            second.Succeeded &&
            second.Lease.Generation == 2 &&
            !stale.Succeeded &&
            stale.Status == GuardedIstStackReleaseStatus.StaleOwnership &&
            state.HasActiveLease == false &&
            !invalidLease.Succeeded &&
            invalidLease.Status == GuardedIstStackReleaseStatus.InvalidLease &&
            secondReleased.Succeeded &&
            secondReleased.ReleasedGeneration == 2 &&
            !invalidStateAllocation.Succeeded &&
            invalidStateAllocation.Status ==
                GuardedIstStackAllocationStatus.InvalidAllocatorState &&
            !invalidStateRelease.Succeeded &&
            invalidStateRelease.Status ==
                GuardedIstStackReleaseStatus.InvalidAllocatorState;
    }

    private static bool TestHotPathDoesNotAllocate()
    {
        for (int iteration = 0; iteration < 16; iteration++)
        {
            if (!RunAllocationCycle())
            {
                return false;
            }
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 1024; iteration++)
        {
            if (!RunAllocationCycle())
            {
                return false;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return after == before;
    }

    private static bool TestDefaultsAndSourceBoundary()
    {
        GuardedIstStackArenaInitializationResult initialization = default;
        GuardedIstStackAllocationResult allocation = default;
        GuardedIstStackReleaseResult release = default;
        GuardedIstStackDescriptor descriptor = default;
        GuardedIstStackLease lease = default;
        GuardedIstStackAllocatorState state = default;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "GuardedISTStacksDoubleFaultPageFaultNMI.cs"));

        return !initialization.IsInitialized &&
            !initialization.Succeeded &&
            initialization.Status == GuardedIstStackArenaStatus.Success &&
            !allocation.IsInitialized &&
            !allocation.Succeeded &&
            allocation.Status == GuardedIstStackAllocationStatus.Success &&
            !allocation.StateMutated &&
            !release.IsInitialized &&
            !release.Succeeded &&
            release.Status == GuardedIstStackReleaseStatus.Success &&
            !descriptor.IsInitialized &&
            !descriptor.IsUsable &&
            !lease.IsInitialized &&
            !lease.IsUsable &&
            !state.IsInitialized &&
            !state.HasActiveLease &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackArenaRequest>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackAllocatorState>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackDescriptor>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackLease>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackAllocationResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GuardedIstStackReleaseResult>() &&
            source.Contains("ReservedRangeOverlap", StringComparison.Ordinal) &&
            source.Contains("ProhibitedWritableExecutable", StringComparison.Ordinal) &&
            source.Contains("StaleOwnership", StringComparison.Ordinal) &&
            source.Contains("AlreadyReleased", StringComparison.Ordinal) &&
            !source.Contains("SharpOS", StringComparison.Ordinal) &&
            !source.Contains("Console", StringComparison.Ordinal) &&
            !source.Contains("throw", StringComparison.Ordinal) &&
            !source.Contains("new byte[", StringComparison.Ordinal) &&
            !source.Contains("Span<", StringComparison.Ordinal);
    }

    private static bool RunAllocationCycle()
    {
        GuardedIstStackArenaInitializationResult initialization =
            GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(
                CreateValidRequest(),
                out GuardedIstStackAllocatorState state);
        GuardedIstStackAllocationResult allocation =
            GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);
        GuardedIstStackReleaseResult release =
            GuardedISTStacksDoubleFaultPageFaultNMI.Release(
                ref state,
                allocation.Lease);

        return initialization.Succeeded &&
            allocation.Succeeded &&
            release.Succeeded &&
            !state.HasActiveLease;
    }

    private static GuardedIstStackArenaRequest CreateValidRequest(
        ulong ownerToken = OwnerToken)
    {
        return new GuardedIstStackArenaRequest(
            ArenaBaseAddress,
            ArenaSizeBytes,
            StackSizeBytes,
            GuardSizeBytes,
            ownerToken,
            stackWritable: true,
            stackExecutable: false);
    }

    private static GuardedIstStackArenaInitializationResult InitializeWith(
        out GuardedIstStackAllocatorState state,
        ulong arenaBaseAddress = ArenaBaseAddress,
        ulong arenaSizeBytes = ArenaSizeBytes,
        ulong stackSizeBytes = StackSizeBytes,
        ulong guardSizeBytes = GuardSizeBytes,
        ulong ownerToken = OwnerToken,
        bool stackWritable = true,
        bool stackExecutable = false,
        ulong reservedBaseAddress = 0,
        ulong reservedSizeBytes = 0)
    {
        GuardedIstStackArenaRequest request = new GuardedIstStackArenaRequest(
            arenaBaseAddress,
            arenaSizeBytes,
            stackSizeBytes,
            guardSizeBytes,
            ownerToken,
            stackWritable,
            stackExecutable,
            reservedBaseAddress,
            reservedSizeBytes);
        return GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(request, out state);
    }

    private static bool IsExactDescriptor(
        GuardedIstStackDescriptor descriptor,
        GuardedIstStackKind kind,
        byte istIndex,
        ulong guardBaseAddress)
    {
        return descriptor.IsUsable &&
            descriptor.Kind == kind &&
            descriptor.IstIndex == istIndex &&
            descriptor.GuardBaseAddress == guardBaseAddress &&
            descriptor.GuardSizeBytes == GuardSizeBytes &&
            descriptor.StackBaseAddress == guardBaseAddress + GuardSizeBytes &&
            descriptor.StackSizeBytes == StackSizeBytes &&
            descriptor.StackTopAddress ==
                guardBaseAddress + GuardSizeBytes + StackSizeBytes &&
            !descriptor.GuardPagePresent &&
            descriptor.StackWritable &&
            !descriptor.StackExecutable;
    }

    private static bool IsAtomicInitializationFailure(
        GuardedIstStackArenaInitializationResult result,
        GuardedIstStackArenaStatus expectedStatus,
        GuardedIstStackAllocatorState state)
    {
        return result.IsInitialized &&
            !result.Succeeded &&
            result.Status == expectedStatus &&
            !state.IsInitialized &&
            !state.HasActiveLease &&
            state.OwnerToken == 0 &&
            state.ActiveGeneration == 0;
    }
}
