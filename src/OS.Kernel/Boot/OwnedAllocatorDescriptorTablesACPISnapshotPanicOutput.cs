using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

namespace OS.Boot;

public enum OwnedAllocatorRoundTripStatus : uint
{
    Empty, AdmissionFailed, AlreadyConsumed, DiagnosticsFailed, OwnerRejected,
    AllocationFailed, PageRejected, ReadbackFailed, FreeFailed, AccountingFailed, Completed
}

public struct OwnedAllocatorRoundTripState
{
    internal OwnedAllocatorRoundTripStatus StatusValue;
    internal ulong PageValue;
    internal bool AllocatedValue, WrittenValue, RestoredValue, FreedValue;
    public OwnedAllocatorRoundTripStatus Status => StatusValue;
    public ulong Page => PageValue;
    public bool Allocated => AllocatedValue;
    public bool Written => WrittenValue;
    public bool Restored => RestoredValue;
    public bool Freed => FreedValue;
}

/// <summary>
/// The allocator portion of M03-010 only. The adapter must supply the original,
/// exclusively retained state, diagnostic, arena, M01 owner and identity backing;
/// address declarations cannot prove their provenance or accessible lifetime.
/// This operation invokes the actual M03-009 reporter once. It emits no M03-010
/// success marker and does not establish ACPI, descriptor execution or panic proof.
/// Never reset/copy the consumed state or retry a partial attempt. The terminal
/// adapter must halt after return. Arbitrary hardware faults are not rolled back.
/// </summary>
public static unsafe class OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput
{
    private const ulong PageBytes = 4096;
    private const ulong LowEnd = 0x0000800000000000;
    private const PageProtection Data = PageProtection.Read | PageProtection.Write;

    public static OwnedAllocatorRoundTripStatus RunAllocatorRoundTrip(
        ref OwnedAllocatorRoundTripState state, ref FirstExitDiagnosticState diagnostic,
        ref FirstExitOwnedArenaState arena, FirstExitDiagnosticMemory memory,
        M01LiveAllocatorStaticState* owner)
    {
        fixed (OwnedAllocatorRoundTripState* operation = &state)
        fixed (FirstExitDiagnosticState* report = &diagnostic)
        fixed (FirstExitOwnedArenaState* handle = &arena)
            if (!Admit(operation, report, handle, memory, owner))
                return OwnedAllocatorRoundTripStatus.AdmissionFailed;
        if (state.StatusValue != OwnedAllocatorRoundTripStatus.Empty)
            return OwnedAllocatorRoundTripStatus.AlreadyConsumed;
        if (state.PageValue != 0 || state.AllocatedValue || state.WrittenValue || state.RestoredValue || state.FreedValue)
            return OwnedAllocatorRoundTripStatus.AdmissionFailed;
        // Structural refusal leaves every admitted storage unchanged. Consumed
        // failures below retain the last actual operation/cleanup observations.
        if (!owner->IsCompleted || !MatchesBacking(*owner, memory))
            return OwnedAllocatorRoundTripStatus.OwnerRejected;
        state.StatusValue = OwnedAllocatorRoundTripStatus.DiagnosticsFailed;
        if (!FirstExitPostExitDiagnostics.Report(ref diagnostic, ref arena, memory)) return state.StatusValue;
        state.StatusValue = OwnedAllocatorRoundTripStatus.OwnerRejected;
        if (!owner->IsCompleted || !MatchesBacking(*owner, memory)) return state.StatusValue;

        BootPageAllocatorState before = owner->AllocatorStateValue;
        BootPageFreeListNode node = owner->FreeNodeValue;
        state.StatusValue = OwnedAllocatorRoundTripStatus.AllocationFailed;
        // This dedicated M01 arena was already reserved as a whole. Passing the
        // enclosing ledger as exclusions would exclude its own allocatable pages.
        BootPageAllocatorOnePageResult allocation = BootPageAllocatorMutator.AllocateOnePage(
            before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, PageBytes);
        if (!allocation.Succeeded) return state.StatusValue;
        owner->AllocatorStateValue = allocation.AfterState;
        state.AllocatedValue = true;
        state.StatusValue = OwnedAllocatorRoundTripStatus.PageRejected;
        bool admitted = allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) == AddressValueStatus.Success &&
            allocation.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong end) == AddressValueStatus.Success &&
            allocation.PageCount == 1 && allocation.ByteLength == PageBytes && (page & (PageBytes - 1)) == 0 &&
            page >= memory.Allocator.Start && page - memory.Allocator.Start <= memory.Allocator.PageCount * PageBytes - PageBytes &&
            end == page + PageBytes;
        state.PageValue = page;
        bool verified = false;
        if (admitted)
        {
            ulong* first = (ulong*)page;
            ulong* last = (ulong*)(page + PageBytes - sizeof(ulong));
            ulong savedFirst = ReadWord(first), savedLast = ReadWord(last);
            ulong pattern = page ^ 0x67a15c3ed209b480UL;
            WriteWord(first, pattern); WriteWord(last, ~pattern);
            state.WrittenValue = true;
            verified = ReadWord(first) == pattern && ReadWord(last) == ~pattern;
            WriteWord(first, savedFirst); WriteWord(last, savedLast);
            state.RestoredValue = ReadWord(first) == savedFirst && ReadWord(last) == savedLast;
            state.StatusValue = OwnedAllocatorRoundTripStatus.ReadbackFailed;
        }
        // Even a rejected page is returned using the exact mutator receipt; it
        // was never dereferenced. Always commit a successful free's state.
        BootPageAllocatorOnePageFreeResult free = BootPageAllocatorMutator.FreeOnePage(
            owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
        if (!free.Succeeded) { state.StatusValue = OwnedAllocatorRoundTripStatus.FreeFailed; return state.StatusValue; }
        owner->AllocatorStateValue = free.AfterState;
        state.FreedValue = true;
        if (!SameState(before, owner->AllocatorStateValue) || !SameNode(node, owner->FreeNodeValue) || !owner->IsCompleted)
            state.StatusValue = OwnedAllocatorRoundTripStatus.AccountingFailed;
        else if (admitted && verified && state.RestoredValue)
            state.StatusValue = OwnedAllocatorRoundTripStatus.Completed;
        return state.StatusValue;
    }

    private static bool Admit(OwnedAllocatorRoundTripState* operation, FirstExitDiagnosticState* diagnostic,
        FirstExitOwnedArenaState* arena, FirstExitDiagnosticMemory memory, M01LiveAllocatorStaticState* owner)
    {
        FirstExitImageReferences references = memory.References;
        if ((ulong)sizeof(M01LiveAllocatorStaticState) != M01LiveAllocatorStaticState.RequiredStorageBytes ||
            references.AllocatorState != (ulong)owner || references.AllocatorStateBytes != M01LiveAllocatorStaticState.RequiredStorageBytes ||
            !Extent(references.AdapterState, references.AdapterStateBytes) ||
            memory.PageCapacity == 0 || memory.PageCapacity > SeparateDisabledDefaultPageFaultPageTables.MaximumTablePages ||
            memory.EntryCapacity != memory.PageCapacity * 512 || memory.Tables.ArenaPageCount != memory.PageCapacity ||
            memory.Tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != AddressValueStatus.Success ||
            root != (ulong)memory.Entries || !memory.Allocator.IsInitialized || memory.Allocator.Protection != Data ||
            memory.Allocator.CachePolicy != PageCachePolicy.WriteBack || (memory.Allocator.Start & 4095) != 0 ||
            memory.Allocator.PageCount < M01LiveAllocatorIntegration.MinimumArenaPageCount ||
            memory.Allocator.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount) return false;

        ulong* starts = stackalloc ulong[6];
        ulong* sizes = stackalloc ulong[6];
        starts[0] = (ulong)operation; sizes[0] = (ulong)sizeof(OwnedAllocatorRoundTripState);
        starts[1] = (ulong)diagnostic; sizes[1] = (ulong)sizeof(FirstExitDiagnosticState);
        starts[2] = (ulong)arena; sizes[2] = (ulong)sizeof(FirstExitOwnedArenaState);
        starts[3] = (ulong)owner; sizes[3] = references.AllocatorStateBytes;
        starts[4] = references.CopiedAcpi; sizes[4] = references.CopiedAcpiBytes;
        starts[5] = (ulong)memory.Pages; sizes[5] = memory.PageCapacity * (ulong)sizeof(X64PageTableImagePage);
        SeparatePageFaultMappingRange* image = stackalloc SeparatePageFaultMappingRange[(int)FirstExitImageMappings.MaximumSections + 1];
        if (!FirstExitImageMappings.WriteRanges(memory.Capture, memory.Mapping, image,
            FirstExitImageMappings.MaximumSections + 1, out ulong count)) return false;
        for (int i = 0; i < 6; i++)
        {
            if (!Extent(starts[i], sizes[i]) || !FirstExitImageMappings.Contains(image, count, starts[i], sizes[i], Data)) return false;
            if (i < 3 && (starts[i] < references.AdapterState || sizes[i] > references.AdapterStateBytes ||
                starts[i] - references.AdapterState > references.AdapterStateBytes - sizes[i])) return false;
            for (int j = 0; j < i; j++)
                if (FirstExitDescriptors.Overlaps(starts[i], sizes[i], starts[j], sizes[j])) return false;
        }
        // No operation/diagnostic write may touch its inputs or allocator pages.
        // Retained allocation declarations are pairwise disjoint from the image
        // and each other before either consumed state or free-list mutation.
        starts[0] = image[0].Start; sizes[0] = memory.Capture.Range.ImageSize;
        starts[1] = (ulong)arena->Storage; sizes[1] = FirstExitOwnedArena.Bytes;
        starts[2] = (ulong)memory.Code; sizes[2] = FirstExitCodeBundle.Bytes;
        starts[3] = memory.Allocator.Start; sizes[3] = memory.Allocator.PageCount * PageBytes;
        starts[4] = root; sizes[4] = memory.PageCapacity * PageBytes;
        starts[5] = references.RuntimeSupportCode; sizes[5] = references.RuntimeSupportCodeBytes;
        int allocations = starts[5] == 0 && sizes[5] == 0 ? 5 : 6;
        for (int i = 0; i < allocations; i++)
        {
            if (!Extent(starts[i], sizes[i])) return false;
            for (int j = 0; j < i; j++)
                if (FirstExitDescriptors.Overlaps(starts[i], sizes[i], starts[j], sizes[j])) return false;
        }
        return true;
    }

    private static bool MatchesBacking(M01LiveAllocatorStaticState owner, FirstExitDiagnosticMemory memory)
        => owner.ArenaReservation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start) == AddressValueStatus.Success &&
            start == memory.Allocator.Start && owner.ArenaReservation.PageCount == memory.Allocator.PageCount;

    private static bool Extent(ulong start, ulong bytes)
        => start != 0 && (start & 7) == 0 && start < LowEnd && bytes != 0 && bytes < LowEnd - start;

    // Keep distinct actual memory reads/writes in the native review surface.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ReadWord(ulong* address) => *address;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteWord(ulong* address, ulong value) => *address = value;

    private static bool SameState(BootPageAllocatorState a, BootPageAllocatorState b)
        => a.IsInitialized == b.IsInitialized && a.PageSize == b.PageSize && a.HeadNodeIndex == b.HeadNodeIndex &&
            a.FreeNodeCount == b.FreeNodeCount && a.FreePageCount == b.FreePageCount && a.FreeByteCount == b.FreeByteCount &&
            a.AllocatedPageCount == b.AllocatedPageCount && a.AllocatedByteCount == b.AllocatedByteCount;

    private static bool SameNode(BootPageFreeListNode a, BootPageFreeListNode b)
        => a.SourceFreeSegmentIndex == b.SourceFreeSegmentIndex && a.SourceUsableRangeIndex == b.SourceUsableRangeIndex &&
            a.PhysicalStart == b.PhysicalStart && a.PhysicalEndExclusive == b.PhysicalEndExclusive &&
            a.PageCount == b.PageCount && a.ByteLength == b.ByteLength && a.NextNodeIndex == b.NextNodeIndex;
}
