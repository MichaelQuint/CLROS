using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01LivePagingIntegrationTests
{
    private sealed unsafe class ArenaFixture : IDisposable
    {
        private const ulong DefaultDescriptorPages = 32;
        private readonly CanonicalFixture _canonical;
        private byte* _arenaBacking;
        internal ArenaFixture()
        {
            _canonical = new CanonicalFixture();
            _canonical.UseLiveIntervalProfile();
            _arenaBacking = (byte*)NativeMemory.AlignedAlloc((nuint)M01LivePagingIntegration.MaximumArenaTotalByteLength, (nuint)BootReservedRange.PageSize);
            if (_arenaBacking == null)
                throw new OutOfMemoryException();
            for (ulong index = 0; index < M01LivePagingIntegration.MaximumArenaTotalByteLength; index++)
            {
                _arenaBacking[index] = 0xa5;
            }

            FixedRequirements = GC.AllocateArray<X64BootIdentityMappingRequirement>(checked((int)M01LivePagingIntegration.RequiredArenaFixedRequirementCount), pinned: true);
            X64BootIdentityMappingPurpose[] fixedPurposes = [X64BootIdentityMappingPurpose.BootStackGuard, X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, X64BootIdentityMappingPurpose.CompositeOwnedStack, X64BootIdentityMappingPurpose.ProtectionProbePage];
            for (int index = 0; index < fixedPurposes.Length; index++)
            {
                int source = FindRequirementIndex(_canonical.MappingRequirements, fixedPurposes[index]);
                if (source < 0)
                {
                    throw new InvalidOperationException("arena fixed requirement missing: " + fixedPurposes[index]);
                }

                FixedRequirements[index] = _canonical.MappingRequirements[source];
            }

            List<BootReservedRange> reservations = [];
            foreach (BootReservedRange reservation in _canonical.CopyLedger())
            {
                if (reservation.Kind != BootReservationKind.PageTableArena)
                {
                    reservations.Add(reservation);
                }
            }

            Reservations = [.. reservations];
            RequirementWorkspace = GC.AllocateArray<X64BootIdentityMappingRequirement>(checked((int)M01LivePagingIntegration.RequiredArenaRequirementWorkspaceCount), pinned: true);
            RangeWorkspace = GC.AllocateArray<X64BootIdentityMappingRange>(checked((int)X64BootIdentityMappingPlan.MaximumDestinationCount), pinned: true);
            StateSnapshot = GC.AllocateArray<byte>(checked((int)M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount), pinned: true);
        }

        internal ulong Candidate => _canonical.TableStateStart;
        internal byte PhysicalAddressWidth => _canonical.Preconditions.PhysicalAddressWidth;
        internal X64PageTableEncodingPreconditions Preconditions => _canonical.Preconditions;
        internal byte* ArenaBacking => _arenaBacking;
        internal X64BootIdentityMappingRequirement[] FixedRequirements
        {
            get;
        }
        internal BootReservedRange[] Reservations
        {
            get;
        }
        internal X64BootIdentityMappingRequirement[] RequirementWorkspace
        {
            get;
        }
        internal X64BootIdentityMappingRange[] RangeWorkspace
        {
            get;
        }
        internal X64PageTableImagePage[] PageImages => _canonical.PageImages;
        internal ulong[] Entries => _canonical.Entries;
        internal byte[] StateSnapshot
        {
            get;
        }

        internal void UseRelocatedFixedRequirements(ulong candidate)
        {
            ulong page = BootReservedRange.PageSize;
            if ((candidate & (page - 1)) != 0 || candidate < 8 * page)
            {
                throw new ArgumentOutOfRangeException(nameof(candidate));
            }

            FixedRequirements[0] = Requirement(X64BootIdentityMappingPurpose.BootStackGuard, candidate - 8 * page, candidate - 7 * page, PageProtection.None, PageCachePolicy.Uninitialized);
            FixedRequirements[1] = Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, candidate - 7 * page, candidate - 6 * page, M01014TestData.ReadExecute);
            FixedRequirements[2] = Requirement(X64BootIdentityMappingPurpose.CompositeOwnedStack, candidate - 6 * page, candidate - 3 * page, M01014TestData.ReadWrite);
            FixedRequirements[3] = Requirement(X64BootIdentityMappingPurpose.ProtectionProbePage, candidate - 2 * page, candidate - page, M01014TestData.ReadWrite);
        }

        internal M01LivePagingArenaPlanResult Plan(ulong? candidate = null, ulong? descriptorStart = null, ulong descriptorPages = DefaultDescriptorPages, ulong descriptorAttribute = 0, ulong iterationLimit = M01LivePagingIntegration.MaximumArenaIterationCount, BootReservedRange[]? reservations = null, PhysicalAddress candidateAddress = default, bool useCandidateAddress = false, X64PageTableEncodingPreconditions? preconditions = null)
        {
            ClearWorkspace();
            ulong rawCandidate = candidate ?? Candidate;
            ulong rawDescriptorStart = descriptorStart ?? rawCandidate;
            ulong descriptorBytes = descriptorPages <= ~0UL / BootReservedRange.PageSize ? descriptorPages * BootReservedRange.PageSize : 0;
            ulong descriptorEnd = descriptorBytes != 0 && rawDescriptorStart <= ~0UL - descriptorBytes ? rawDescriptorStart + descriptorBytes : 0;
            BootMemoryRegionRecord descriptor = new((uint)UefiMemoryType.ConventionalMemory, rawDescriptorStart, 0, descriptorPages, descriptorAttribute, descriptorBytes, descriptorEnd);
            return PlanCore(useCandidateAddress ? candidateAddress : Physical(rawCandidate), descriptor, iterationLimit, reservations ?? Reservations, preconditions ?? _canonical.Preconditions);
        }

        internal M01LivePagingArenaPlanResult PlanWithStorage(ulong fixedCount = M01LivePagingIntegration.RequiredArenaFixedRequirementCount, ulong fixedCapacity = M01LivePagingIntegration.RequiredArenaFixedRequirementCount, ulong? requirementCapacity = null, ulong? rangeCapacity = null, bool missingFixed = false, bool misalignFixed = false, ulong? reservationCount = null, ulong? reservationCapacity = null, bool missingReservations = false, bool misalignReservations = false, bool missingRequirements = false, bool misalignRequirements = false, bool missingRanges = false, bool misalignRanges = false, bool aliasWorkspaces = false)
        {
            ClearWorkspace();
            BootMemoryRegionRecord descriptor = Record(UefiMemoryType.ConventionalMemory, Candidate, DefaultDescriptorPages);
            fixed (X64BootIdentityMappingRequirement* fixedPointer = FixedRequirements)
            fixed (BootReservedRange* reservationPointer = Reservations)
            fixed (X64BootIdentityMappingRequirement* requirementPointer = RequirementWorkspace)
            fixed (X64BootIdentityMappingRange* rangePointer = RangeWorkspace)
            {
                X64BootIdentityMappingRequirement* actualFixed = missingFixed ? null : misalignFixed ? (X64BootIdentityMappingRequirement*)((byte*)fixedPointer + 1) : fixedPointer;
                BootReservedRange* actualReservations = missingReservations ? null : misalignReservations ? (BootReservedRange*)((byte*)reservationPointer + 1) : reservationPointer;
                X64BootIdentityMappingRequirement* actualRequirement = missingRequirements ? null : misalignRequirements ? (X64BootIdentityMappingRequirement*)((byte*)requirementPointer + 1) : requirementPointer;
                X64BootIdentityMappingRange* actualRange = missingRanges ? null : misalignRanges ? (X64BootIdentityMappingRange*)((byte*)rangePointer + 1) : aliasWorkspaces ? (X64BootIdentityMappingRange*)requirementPointer : rangePointer;
                return M01LivePagingIntegration.PlanOwnedTableArena(actualFixed, fixedCount, fixedCapacity, descriptor, actualReservations, reservationCount ?? (ulong)Reservations.Length, reservationCapacity ?? (ulong)Reservations.Length, Physical(Candidate), _canonical.Preconditions, M01LivePagingIntegration.MaximumArenaIterationCount, actualRequirement, 0, requirementCapacity ?? (ulong)RequirementWorkspace.Length, actualRange, 0, rangeCapacity ?? (ulong)RangeWorkspace.Length);
            }
        }

        internal BootReservedRange[] CreateReservationsWithCandidateOverlap()
        {
            BootReservedRange[] result = new BootReservedRange[Reservations.Length + 1];
            Reservations.CopyTo(result, 0);
            result[^1] = CreateRange(BootReservationKind.PageTableArena, Candidate, 1);
            Array.Sort(result, static (left, right) => Raw(left.PhysicalStart).CompareTo(Raw(right.PhysicalStart)));
            return result;
        }

        internal M01LivePagingPreparationResult Prepare()
        {
            _canonical.ClearPreparationState();
            return _canonical.PrepareCurrent();
        }

        internal byte* StateBacking(M01LivePagingArenaPlanResult plan)
        {
            return ArenaBacking + plan.TableByteLength;
        }

        internal M01LivePagingArenaMaterializationResult Materialize(M01LivePagingArenaPlanResult plan, M01LivePagingArenaByteMode byteMode, BootMemoryRegionRecord? allocationRecord = null)
        {
            return MaterializeRaw(plan, byteMode, allocationRecord: allocationRecord);
        }

        internal M01LivePagingArenaMaterializationResult MaterializeRaw(M01LivePagingArenaPlanResult plan, M01LivePagingArenaByteMode byteMode = M01LivePagingArenaByteMode.EmitAndVerify, BootMemoryRegionRecord? allocationRecord = null, ulong? pageImageCount = null, ulong? pageImageCapacity = null, ulong? entryCount = null, ulong? entryCapacity = null, ulong? arenaByteCount = null, ulong? arenaCapacity = null, bool missingPageImages = false, bool misalignPageImages = false, bool overflowPageImageSpan = false, bool missingEntries = false, bool misalignEntries = false, bool overflowEntrySpan = false, bool missingArena = false, bool misalignArena = false, bool overflowArenaSpan = false, bool arenaAliasesPageImages = false, bool arenaAliasesEntries = false, ulong? stateSnapshotByteCount = null, ulong? stateSnapshotByteCapacity = null, bool missingStateSnapshot = false, bool misalignStateSnapshot = false, bool overflowStateSnapshotSpan = false, bool stateSnapshotAliasesArena = false, bool stateSnapshotAliasesPageImages = false, bool stateSnapshotAliasesEntries = false)
        {
            fixed (X64PageTableImagePage* pagePointer = PageImages)
            fixed (ulong* entryPointer = Entries)
            fixed (byte* stateSnapshotPointer = StateSnapshot)
            {
                X64PageTableImagePage* actualPages = missingPageImages ? null : overflowPageImageSpan ? (X64PageTableImagePage*)(~0UL - 7) : misalignPageImages ? (X64PageTableImagePage*)((byte*)pagePointer + 1) : arenaAliasesPageImages ? (X64PageTableImagePage*)ArenaBacking : pagePointer;
                ulong* actualEntries = missingEntries ? null : overflowEntrySpan ? (ulong*)(~0UL - 7) : misalignEntries ? (ulong*)((byte*)entryPointer + 1) : arenaAliasesEntries ? (ulong*)ArenaBacking : entryPointer;
                byte* actualArena = missingArena ? null : overflowArenaSpan ? (byte*)(~0UL & ~(BootReservedRange.PageSize - 1)) : misalignArena ? ArenaBacking + 1 : ArenaBacking;
                byte* actualStateSnapshot = missingStateSnapshot ? null : overflowStateSnapshotSpan ? (byte*)(~0UL - 7) : misalignStateSnapshot ? stateSnapshotPointer + 1 : stateSnapshotAliasesArena ? ArenaBacking : stateSnapshotAliasesPageImages ? (byte*)pagePointer : stateSnapshotAliasesEntries ? (byte*)entryPointer : stateSnapshotPointer;
                return M01LivePagingIntegration.MaterializeOwnedTableArena(plan, allocationRecord ?? plan.AllocationRecord, actualPages, pageImageCount ?? plan.RequiredPageImageCount, pageImageCapacity ?? (ulong)PageImages.Length, actualEntries, entryCount ?? plan.RequiredPageTableEntryCount, entryCapacity ?? (ulong)Entries.Length, actualArena, arenaByteCount ?? plan.TotalByteLength, arenaCapacity ?? plan.TotalByteLength, byteMode, actualStateSnapshot, stateSnapshotByteCount ?? M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount, stateSnapshotByteCapacity ?? M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount);
            }
        }

        internal M01LivePagingArenaMaterializationResult MaterializeWithPageImages(M01LivePagingArenaPlanResult plan, X64PageTableImagePage[] pageImages)
        {
            fixed (X64PageTableImagePage* pagePointer = pageImages)
            fixed (ulong* entryPointer = Entries)
            fixed (byte* stateSnapshotPointer = StateSnapshot)
            {
                return M01LivePagingIntegration.MaterializeOwnedTableArena(plan, plan.AllocationRecord, pagePointer, (ulong)pageImages.Length, (ulong)pageImages.Length, entryPointer, plan.RequiredPageTableEntryCount, (ulong)Entries.Length, ArenaBacking, plan.TotalByteLength, plan.TotalByteLength, M01LivePagingArenaByteMode.EmitAndVerify, stateSnapshotPointer, M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount, M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount);
            }
        }

        internal X64PageTableImagePage[] CopyUsedPageImages(M01LivePagingArenaPlanResult plan)
        {
            X64PageTableImagePage[] result = new X64PageTableImagePage[checked((int)plan.RequiredPageImageCount)];
            Array.Copy(PageImages, result, result.Length);
            return result;
        }

        internal bool MaterializedPagesMatch(M01LivePagingArenaPlanResult plan)
        {
            for (ulong pageOrdinal = 0; pageOrdinal < plan.RequiredPageImageCount; pageOrdinal++)
            {
                X64PageTableImagePage page = PageImages[checked((int)pageOrdinal)];
                ulong pageAddress = Raw(page.PhysicalStart);
                if (!page.IsInitialized || pageAddress < Candidate || pageAddress >= Candidate + plan.TableByteLength)
                {
                    return false;
                }

                ulong pageOffset = pageAddress - Candidate;
                ulong* actual = (ulong*)(ArenaBacking + pageOffset);
                ulong entryStart = page.EntryStartIndex;
                for (ulong entryOffset = 0; entryOffset < X64PageTableImagePage.EntryCountPerPage; entryOffset++)
                {
                    if (actual[entryOffset] != Entries[checked((int)(entryStart + entryOffset))])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_arenaBacking != null)
            {
                NativeMemory.AlignedFree(_arenaBacking);
                _arenaBacking = null;
            }

            _canonical.Dispose();
        }

        private M01LivePagingArenaPlanResult PlanCore(PhysicalAddress candidate, BootMemoryRegionRecord descriptor, ulong iterationLimit, BootReservedRange[] reservations, X64PageTableEncodingPreconditions preconditions)
        {
            fixed (X64BootIdentityMappingRequirement* fixedPointer = FixedRequirements)
            fixed (BootReservedRange* reservationPointer = reservations)
            fixed (X64BootIdentityMappingRequirement* requirementPointer = RequirementWorkspace)
            fixed (X64BootIdentityMappingRange* rangePointer = RangeWorkspace)
            {
                return M01LivePagingIntegration.PlanOwnedTableArena(fixedPointer, (ulong)FixedRequirements.Length, (ulong)FixedRequirements.Length, descriptor, reservationPointer, (ulong)reservations.Length, (ulong)reservations.Length, candidate, preconditions, iterationLimit, requirementPointer, 0, (ulong)RequirementWorkspace.Length, rangePointer, 0, (ulong)RangeWorkspace.Length);
            }
        }

        private void ClearWorkspace()
        {
            Array.Clear(RequirementWorkspace);
            Array.Clear(RangeWorkspace);
        }
    }
}
