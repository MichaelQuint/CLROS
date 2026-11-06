using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01OwnedCr3CompositeTransitionTests
{
    private sealed unsafe class CompositeFixture : IDisposable
    {
        private object? _priorFixture;
        private CompositeFixture()
        {
        }

        internal required BootMemoryRegionRecord[] Allocations
        {
            get; init;
        }
        internal required BootReservedRange[] Reservations
        {
            get; init;
        }
        internal required X64BootIdentityMappingRange[] Mappings
        {
            get; init;
        }
        internal required ulong MappingCount
        {
            get; init;
        }
        internal required M01OwnedCr3CompositeTransitionRangeBinding[] Bindings
        {
            get; init;
        }
        internal required ulong CodeMappingIndex
        {
            get; init;
        }
        internal required ulong TableMappingIndex
        {
            get; init;
        }

        internal M01LivePagingPreparationResult PriorEvidence;
        internal BootMemoryRegionRecord* AllocationPointer;
        internal ulong AllocationCount;
        internal ulong AllocationCapacity;
        internal BootReservedRange* ReservationPointer;
        internal ulong ReservationCount;
        internal ulong ReservationCapacity;
        internal X64PageTableImagePage[] PageImages { get; init; } = [];
        internal ulong[] PageEntries { get; init; } = [];

        internal ulong PageImageCount;
        internal ulong PageEntryCount;
        internal M01OwnedCr3CompositeTransitionRuntimeState* StateDestination;
        internal ulong RuntimeStateAddress;
        internal ulong ExpectedReturnAddressValue;
        internal ulong CanaryAddressValue;
        internal ulong* CanaryDestination;
        internal ulong OwnedCr3Value;
        internal ulong ProbeStartValue;
        internal ulong ProbeEndValue;
        internal ulong ExpectedProbePteAddressValue;
        internal ulong InitialProbePteValue;
        internal ulong FinalProbePteValue;
        internal static CompositeFixture Create()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type fixtureType = typeof(M01LivePagingIntegrationTests).GetNestedType("CanonicalFixture", BindingFlags.NonPublic) ?? throw new InvalidOperationException("M01-019 canonical fixture type is unavailable.");
            object inner = Activator.CreateInstance(fixtureType, Flags, binder: null, args: null, culture: null) ?? throw new InvalidOperationException("M01-019 canonical fixture construction failed.");
            try
            {
                MethodInfo execute = fixtureType.GetMethod("PrepareCurrent", Flags) ?? throw new InvalidOperationException("M01-029 fixture preparer is unavailable.");
                object? boxedPrior = execute.Invoke(inner, [M01LivePagingByteMode.EmitAndVerify]);
                if (boxedPrior is not M01LivePagingPreparationResult prior || !prior.Succeeded || !prior.PreparedForCompositeTransition || !prior.IsPreparationOnly || prior.IsModelOnly || prior.IsExecutionEvidence)
                {
                    throw new InvalidOperationException("M01-029 canonical preparation was not successful.");
                }

                BootMemoryRegionRecord* records = (BootMemoryRegionRecord*)UnboxPointer(GetProperty(inner, fixtureType, "Records"));
                BootReservedRange* ledger = (BootReservedRange*)UnboxPointer(GetProperty(inner, fixtureType, "Ledger"));
                ulong ledgerCount = (ulong)GetProperty(inner, fixtureType, "LedgerCount");
                ulong ledgerCapacity = (ulong)GetProperty(inner, fixtureType, "LedgerCapacity");
                X64BootIdentityMappingRange[] mappings = (X64BootIdentityMappingRange[])GetProperty(inner, fixtureType, "MappingRanges");
                X64PageTableImagePage[] pageImages = (X64PageTableImagePage[])GetProperty(inner, fixtureType, "PageImages");
                ulong[] entries = (ulong[])GetProperty(inner, fixtureType, "Entries");
                ulong allocationCount = prior.Evidence.AllocationEvidence.AllocationRecordCount;
                ulong allocationCapacity = prior.Evidence.AllocationEvidence.AllocationRecordCapacity;
                ulong mappingCount = prior.Evidence.MappingPlan.OutputRangeCount;
                ulong pageImageCount = prior.Evidence.TableBuild.TablePageCount;
                ulong pageEntryCount = prior.Evidence.TableBuild.EntryCount;
                ulong codeStart = records[0].PhysicalStart;
                ulong codeEnd = records[0].PhysicalEndExclusive;
                ulong probeStart = records[1].PhysicalStart;
                ulong probeEnd = records[1].PhysicalEndExclusive;
                ulong tableStart = records[2].PhysicalStart;
                ulong tableEnd = records[2].PhysicalEndExclusive;
                ulong stateStart = checked(tableStart + pageImageCount * 4096);
                ulong codeReservation = FindReservation(ledger, ledgerCount, BootReservationKind.Cr3TransitionStub, codeStart, codeEnd, exact: true);
                ulong probeReservation = FindReservation(ledger, ledgerCount, BootReservationKind.PageProtectionProbe, probeStart, probeEnd, exact: true);
                ulong tableReservation = FindReservation(ledger, ledgerCount, BootReservationKind.PageTableArena, tableStart, tableEnd, exact: true);
                ulong stackReservation = FindReservationByKind(ledger, ledgerCount, BootReservationKind.BootStack);
                ulong stackStart = Raw(ledger[stackReservation].PhysicalStart);
                ulong stackEnd = Raw(ledger[stackReservation].PhysicalEndExclusive);
                ulong codeMapping = FindMapping(mappings, mappingCount, codeStart, codeEnd);
                ulong probeMapping = FindMapping(mappings, mappingCount, probeStart, probeEnd);
                ulong tableMapping = FindMapping(mappings, mappingCount, tableStart, tableEnd);
                ulong stackMapping = FindMapping(mappings, mappingCount, stackStart, stackEnd);
                ulong root = Raw(prior.Evidence.TableBuild.RootTable);
                ulong probeLeafIndex = prior.Evidence.TableBuild.ProbeLeafEntryIndex;
                ulong probePteAddress = checked(Raw(pageImages[probeLeafIndex / 512].PhysicalStart) + (probeLeafIndex % 512) * sizeof(ulong));
                ulong initialProbePte = entries[probeLeafIndex];
                ulong finalProbePte = initialProbePte & ~(M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit);
                M01OwnedCr3CompositeTransitionRangeBinding[] bindings = [Binding(M01OwnedCr3CompositeTransitionRangeRole.CompositeCodeAndCallGate, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, codeStart, codeEnd, 0, codeReservation, codeMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.CompositeOwnedStack, M01OwnedCr3CompositeTransitionRangeProvenance.ReservationAndIdentityMapping, stackStart, stackEnd, M01OwnedCr3CompositeTransitionRangeBinding.NoIndex, stackReservation, stackMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.ProtectionProbePage, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, probeStart, probeEnd, 1, probeReservation, probeMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.CompleteTableAndStateArena, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, tableStart, tableEnd, 2, tableReservation, tableMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.RuntimeStateAndCanaryPage, M01OwnedCr3CompositeTransitionRangeProvenance.ContainedInAllocationReservationAndIdentityMapping, stateStart, tableEnd, 2, tableReservation, tableMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.OwnedRootPage, M01OwnedCr3CompositeTransitionRangeProvenance.ContainedInAllocationReservationAndIdentityMapping, root, root + 4096, 2, tableReservation, tableMapping)];
                BootMemoryRegionRecord[] allocationMirror = new BootMemoryRegionRecord[checked((int)allocationCount)];
                for (int index = 0; index < allocationMirror.Length; index++)
                {
                    allocationMirror[index] = records[index];
                }

                BootReservedRange[] reservationMirror = new BootReservedRange[checked((int)ledgerCount)];
                for (int index = 0; index < reservationMirror.Length; index++)
                {
                    reservationMirror[index] = ledger[index];
                }

                return new CompositeFixture
                {
                    _priorFixture = inner,
                    PriorEvidence = prior,
                    AllocationPointer = records,
                    AllocationCount = allocationCount,
                    AllocationCapacity = allocationCapacity,
                    ReservationPointer = ledger,
                    ReservationCount = ledgerCount,
                    ReservationCapacity = ledgerCapacity,
                    Allocations = allocationMirror,
                    Reservations = reservationMirror,
                    Mappings = mappings,
                    MappingCount = mappingCount,
                    PageImages = pageImages,
                    PageEntries = entries,
                    PageImageCount = pageImageCount,
                    PageEntryCount = pageEntryCount,
                    Bindings = bindings,
                    StateDestination = (M01OwnedCr3CompositeTransitionRuntimeState*)stateStart,
                    RuntimeStateAddress = stateStart,
                    ExpectedReturnAddressValue = checked(codeStart + M01OwnedCr3CompositeTransition.CallNextReturnSiteOffset),
                    CanaryAddressValue = stateStart + M01OwnedCr3CompositeTransitionRuntimeState.StructureSize,
                    CanaryDestination = (ulong*)(stateStart + M01OwnedCr3CompositeTransitionRuntimeState.StructureSize),
                    OwnedCr3Value = root,
                    ProbeStartValue = probeStart,
                    ProbeEndValue = probeEnd,
                    ExpectedProbePteAddressValue = probePteAddress,
                    InitialProbePteValue = initialProbePte,
                    FinalProbePteValue = finalProbePte,
                    CodeMappingIndex = codeMapping,
                    TableMappingIndex = tableMapping
                };
            }
            catch
            {
                ((IDisposable)inner).Dispose();
                throw;
            }
        }

        private static CompositeFixture CreateSynthetic()
        {
            X64BootIdentityMappingRequirement[] requirements = [Requirement(X64BootIdentityMappingPurpose.BootStackGuard, 0x1000, 0x2000, PageProtection.None, PageCachePolicy.Uninitialized), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, TablePageArenaStart, TablePageArenaEnd, PageProtection.Read | PageProtection.Write), Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x0030_0000, 0x0030_1000, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, CompositeCodeStart, CompositeCodeEnd, PageProtection.Read | PageProtection.Execute), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, CompositeCodeStart, CompositeCodeEnd, PageProtection.Read | PageProtection.Execute), Requirement(X64BootIdentityMappingPurpose.SerialStub, 0x0050_0000, 0x0050_1000, PageProtection.Read | PageProtection.Execute), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, ContinuationPageStart, ContinuationPageEnd, PageProtection.Read | PageProtection.Execute), Requirement(X64BootIdentityMappingPurpose.OwnedStack, StackStart, StackEnd, PageProtection.Read | PageProtection.Write), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, ProbePageStart, ProbePageEnd, PageProtection.Read | PageProtection.Write), Requirement(X64BootIdentityMappingPurpose.AcpiScratch, 0x00a0_0000, 0x00a0_1000, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.FramebufferMetadata, 0x00b0_0000, 0x00b0_1000, PageProtection.Read)];
            X64BootIdentityMappingRange[] mappings = new X64BootIdentityMappingRange[32];
            X64BootIdentityMappingPlanResult plan;
            fixed (X64BootIdentityMappingRequirement* source = requirements)
            fixed (X64BootIdentityMappingRange* destination = mappings)
            {
                plan = X64BootIdentityMappingPlan.Create(source, (ulong)requirements.Length, (ulong)requirements.Length, destination, (ulong)mappings.Length, 64, EncodingPreconditions());
            }

            if (!plan.Succeeded)
            {
                throw new InvalidOperationException("Canonical identity plan failed: " + plan.Status);
            }

            BootMemoryRegionRecord[] allocations = [Record(UefiMemoryType.LoaderCode, CompositeCodeStart, 1), Record(UefiMemoryType.LoaderData, StackStart, 1), Record(UefiMemoryType.LoaderCode, ContinuationPageStart, 1), Record(UefiMemoryType.LoaderCode, ProbePageStart, 1), Record(UefiMemoryType.LoaderData, TablePageArenaStart, 5)];
            BootReservedRange[] reservations = [Reservation(BootReservationKind.Cr3TransitionStub, CompositeCodeStart, 1), Reservation(BootReservationKind.BootStack, StackStart, 1), Reservation(BootReservationKind.LoadedImage, ContinuationPageStart, 1), Reservation(BootReservationKind.PageProtectionProbe, ProbePageStart, 1), Reservation(BootReservationKind.PageTableArena, TablePageArenaStart, 5)];
            ulong codeMapping = FindMapping(mappings, plan.OutputRangeCount, CompositeCodeStart, CompositeCodeEnd);
            ulong continuationMapping = FindMapping(mappings, plan.OutputRangeCount, ContinuationPageStart, ContinuationPageEnd);
            ulong stackMapping = FindMapping(mappings, plan.OutputRangeCount, StackStart, StackEnd);
            ulong probeMapping = FindMapping(mappings, plan.OutputRangeCount, ProbePageStart, ProbePageEnd);
            ulong tableMapping = FindMapping(mappings, plan.OutputRangeCount, TablePageArenaStart, TablePageArenaEnd);
            M01OwnedCr3CompositeTransitionRangeBinding[] bindings = [Binding(M01OwnedCr3CompositeTransitionRangeRole.CompositeCode, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, CompositeCodeStart, CompositeCodeEnd, 0, 0, codeMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.OwnedStack, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, StackStart, StackEnd, 1, 1, stackMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.LoadedImageContinuationPage, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, ContinuationPageStart, ContinuationPageEnd, 2, 2, continuationMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.ProbePage, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, ProbePageStart, ProbePageEnd, 3, 3, probeMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.CompleteTableArena, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, TablePageArenaStart, TablePageArenaEnd, 4, 4, tableMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.RuntimeStateAndCanary, M01OwnedCr3CompositeTransitionRangeProvenance.ContainedInAllocationReservationAndIdentityMapping, TableStateArenaStart, TableStateArenaEnd, 4, 4, tableMapping), Binding(M01OwnedCr3CompositeTransitionRangeRole.OwnedRoot, M01OwnedCr3CompositeTransitionRangeProvenance.ContainedInAllocationReservationAndIdentityMapping, TablePageArenaStart, TablePageArenaStart + 4096, 4, 4, tableMapping)];
            return new CompositeFixture
            {
                Allocations = allocations,
                Reservations = reservations,
                Mappings = mappings,
                MappingCount = plan.OutputRangeCount,
                Bindings = bindings,
                CodeMappingIndex = codeMapping,
                TableMappingIndex = tableMapping
            };
        }

        internal void SetMappingProtection(ulong mappingIndex, PageProtection protection)
        {
            if (mappingIndex >= (ulong)Mappings.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(mappingIndex));
            }

            int fieldOffset = checked((int)Marshal.OffsetOf<X64BootIdentityMappingRange>("_protection"));
            fixed (X64BootIdentityMappingRange* mappings = Mappings)
            {
                byte* field = (byte*)(mappings + mappingIndex) + fieldOffset;
                *field = (byte)protection;
            }
        }

        internal void SetMappingCachePolicy(ulong mappingIndex, PageCachePolicy cachePolicy)
        {
            if (mappingIndex >= (ulong)Mappings.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(mappingIndex));
            }

            int fieldOffset = checked((int)Marshal.OffsetOf<X64BootIdentityMappingRange>("_cachePolicy"));
            fixed (X64BootIdentityMappingRange* mappings = Mappings)
            {
                byte* field = (byte*)(mappings + mappingIndex) + fieldOffset;
                *field = (byte)cachePolicy;
            }
        }

        internal void SetMappingUlong(ulong mappingIndex, string fieldName, ulong value)
        {
            if (mappingIndex >= (ulong)Mappings.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(mappingIndex));
            }

            int fieldOffset = checked((int)Marshal.OffsetOf<X64BootIdentityMappingRange>(fieldName));
            fixed (X64BootIdentityMappingRange* mappings = Mappings)
            {
                *(ulong*)((byte*)(mappings + mappingIndex) + fieldOffset) = value;
            }
        }

        internal M01OwnedCr3CompositeTransitionResult Execute(ulong? allocationCount = null, ulong? allocationCapacity = null, ulong? reservationCount = null, ulong? reservationCapacity = null, ulong? mappingCount = null, ulong? mappingCapacity = null, ulong? pageImageCount = null, ulong? pageImageCapacity = null, ulong? pageEntryCount = null, ulong? pageEntryCapacity = null, ulong? bindingCount = null, ulong? bindingCapacity = null, ulong? stateDestinationCount = null, ulong? stateDestinationCapacity = null, M01OwnedCr3CompositeTransitionRuntimeState* stateDestination = null, bool useNullStateDestination = false, ulong* canaryDestination = null, bool useNullCanaryDestination = false, ulong? canaryDestinationCount = null, ulong? canaryDestinationCapacity = null, ulong? ownedCr3Raw = null, ulong? expectedReturnAddress = null, ulong? canaryAddress = null, ulong expectedCanary = ExpectedCanary, ulong replacementCanary = ReplacementCanary)
        {
            fixed (X64BootIdentityMappingRange* mappings = Mappings)
            fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = Bindings)
            {
                return ExecuteRaw(this, AllocationPointer, allocationCount ?? AllocationCount, allocationCapacity ?? AllocationCapacity, ReservationPointer, reservationCount ?? ReservationCount, reservationCapacity ?? ReservationCapacity, mappings, mappingCount ?? MappingCount, mappingCapacity ?? (ulong)Mappings.Length, bindings, bindingCount ?? (ulong)Bindings.Length, bindingCapacity ?? (ulong)Bindings.Length, ownedCr3Raw ?? OwnedCr3Value, expectedReturnAddress ?? ExpectedReturnAddressValue, canaryAddress ?? CanaryAddressValue, expectedCanary, replacementCanary, pageImageCount ?? PageImageCount, pageImageCapacity ?? (ulong)PageImages.Length, pageEntryCount ?? PageEntryCount, pageEntryCapacity ?? (ulong)PageEntries.Length, stateDestinationCount ?? 1, stateDestinationCapacity ?? 1, stateDestination, useNullStateDestination, canaryDestination == null ? CanaryDestination : canaryDestination, useNullCanaryDestination, canaryDestinationCount ?? 1, canaryDestinationCapacity ?? 1);
            }
        }

        internal void SetAllocation(ulong index, BootMemoryRegionRecord value)
        {
            if (index >= AllocationCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            AllocationPointer[index] = value;
        }

        internal void SetStateUlong(uint byteOffset, ulong value)
        {
            if (byteOffset > M01OwnedCr3CompositeTransitionRuntimeState.StructureSize - sizeof(ulong) || (byteOffset & (sizeof(ulong) - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset));
            }

            *(ulong*)((byte*)StateDestination + byteOffset) = value;
        }

        internal void SetProbeLeaf(ulong value)
        {
            ulong index = PriorEvidence.Evidence.TableBuild.ProbeLeafEntryIndex;
            PageEntries[index] = value;
        }

        internal void ReplaceBinding(int index, M01OwnedCr3CompositeTransitionRangeProvenance provenance, ulong allocationIndex, ulong reservationIndex, ulong mappingIndex)
        {
            M01OwnedCr3CompositeTransitionRangeBinding current = Bindings[index];
            Bindings[index] = Binding(current.Role, provenance, Raw(current.PhysicalStart), Raw(current.PhysicalEndExclusive), allocationIndex, reservationIndex, mappingIndex);
        }

        internal (ulong Pml4, ulong Pdpt, ulong Pd, ulong Pt) ProbeWalkEntryIndexes()
        {
            ulong virtualAddress = ProbeStartValue;
            ulong rootPage = FindPageByPhysical(OwnedCr3Value);
            ulong pml4 = checked(PageImages[rootPage].EntryStartIndex + ((virtualAddress >> 39) & 0x1ff));
            ulong pdptPage = FindPageByPhysical(PageEntries[pml4] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pdpt = checked(PageImages[pdptPage].EntryStartIndex + ((virtualAddress >> 30) & 0x1ff));
            ulong pdPage = FindPageByPhysical(PageEntries[pdpt] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pd = checked(PageImages[pdPage].EntryStartIndex + ((virtualAddress >> 21) & 0x1ff));
            ulong ptPage = FindPageByPhysical(PageEntries[pd] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pt = checked(PageImages[ptPage].EntryStartIndex + ((virtualAddress >> 12) & 0x1ff));
            return (pml4, pdpt, pd, pt);
        }

        internal (ulong Pml4e, ulong Pdpte, ulong Pde, ulong Pte, M01OwnedCr3CompositeTransitionPageZeroLevel Level) PageZeroObservation()
        {
            ulong rootPage = FindPageByPhysical(OwnedCr3Value);
            ulong pml4Index = PageImages[rootPage].EntryStartIndex;
            ulong pml4e = PageEntries[pml4Index];
            if ((pml4e & M01OwnedCr3CompositeTransition.PagePresentBit) == 0)
            {
                return (pml4e, 0, 0, 0, M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPml4);
            }

            ulong pdptPage = FindPageByPhysical(pml4e & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pdpte = PageEntries[PageImages[pdptPage].EntryStartIndex];
            if ((pdpte & M01OwnedCr3CompositeTransition.PagePresentBit) == 0)
            {
                return (pml4e, pdpte, 0, 0, M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPdpt);
            }

            ulong pdPage = FindPageByPhysical(pdpte & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pde = PageEntries[PageImages[pdPage].EntryStartIndex];
            if ((pde & M01OwnedCr3CompositeTransition.PagePresentBit) == 0)
            {
                return (pml4e, pdpte, pde, 0, M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPd);
            }

            ulong ptPage = FindPageByPhysical(pde & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pte = PageEntries[PageImages[ptPage].EntryStartIndex];
            return (pml4e, pdpte, pde, pte, (pte & M01OwnedCr3CompositeTransition.PagePresentBit) == 0 ? M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPt : M01OwnedCr3CompositeTransitionPageZeroLevel.Present);
        }

        internal ulong LeafEntryIndex(ulong virtualAddress)
        {
            ulong rootPage = FindPageByPhysical(OwnedCr3Value);
            ulong pml4 = checked(PageImages[rootPage].EntryStartIndex + ((virtualAddress >> 39) & 0x1ff));
            ulong pdptPage = FindPageByPhysical(PageEntries[pml4] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pdpt = checked(PageImages[pdptPage].EntryStartIndex + ((virtualAddress >> 30) & 0x1ff));
            ulong pdPage = FindPageByPhysical(PageEntries[pdpt] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            ulong pd = checked(PageImages[pdPage].EntryStartIndex + ((virtualAddress >> 21) & 0x1ff));
            ulong ptPage = FindPageByPhysical(PageEntries[pd] & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask);
            return checked(PageImages[ptPage].EntryStartIndex + ((virtualAddress >> 12) & 0x1ff));
        }

        private ulong FindPageByPhysical(ulong address)
        {
            for (ulong index = 0; index < PageImageCount; index++)
            {
                if (Raw(PageImages[index].PhysicalStart) == address)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Walk referenced a page outside the canonical image.");
        }

        public void Dispose()
        {
            object? inner = _priorFixture;
            _priorFixture = null;
            if (inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private static object GetProperty(object instance, Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance) ?? throw new InvalidOperationException("M01-019 fixture property is unavailable: " + name);
        }

        private static void* UnboxPointer(object boxedPointer)
        {
            return Pointer.Unbox(boxedPointer);
        }

        private static ulong FindReservationByKind(BootReservedRange* ledger, ulong count, BootReservationKind kind)
        {
            for (ulong index = 0; index < count; index++)
            {
                if (ledger[index].Kind == kind)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Canonical reservation is unavailable: " + kind);
        }

        private static ulong FindReservation(BootReservedRange* ledger, ulong count, BootReservationKind kind, ulong start, ulong end, bool exact)
        {
            for (ulong index = 0; index < count; index++)
            {
                if (ledger[index].Kind != kind)
                {
                    continue;
                }

                ulong candidateStart = Raw(ledger[index].PhysicalStart);
                ulong candidateEnd = Raw(ledger[index].PhysicalEndExclusive);
                if (exact ? candidateStart == start && candidateEnd == end : start >= candidateStart && end <= candidateEnd)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Canonical reservation geometry is unavailable.");
        }

        private static ulong FindMappingForPurpose(X64BootIdentityMappingRange[] mappings, ulong count, ulong outerStart, ulong outerEnd, X64BootIdentityMappingPurpose purpose, PageProtection protection, out ulong pageStart, out ulong pageEnd)
        {
            ulong purposeBit = 1UL << (byte)purpose;
            for (ulong index = 0; index < count; index++)
            {
                X64BootIdentityMappingRange mapping = mappings[index];
                ulong mappingStart = Raw(mapping.PhysicalStart);
                ulong mappingEnd = Raw(mapping.PhysicalEndExclusive);
                ulong candidateStart = mappingStart > outerStart ? mappingStart : outerStart;
                ulong candidateEnd = mappingEnd < outerEnd ? mappingEnd : outerEnd;
                if ((mapping.ContributingPurposeMask & purposeBit) != 0 && mapping.Protection == protection && mapping.CachePolicy == PageCachePolicy.WriteBack && candidateStart < candidateEnd && (candidateStart & 4095) == 0 && candidateEnd - candidateStart >= 4096)
                {
                    pageStart = candidateStart;
                    pageEnd = candidateStart + 4096;
                    return index;
                }
            }

            throw new InvalidOperationException("Canonical purpose mapping is unavailable.");
        }

        private static ulong FindMapping(X64BootIdentityMappingRange[] mappings, ulong count, ulong start, ulong end)
        {
            for (ulong index = 0; index < count; index++)
            {
                X64BootIdentityMappingRange mapping = mappings[index];
                ulong mappingStart = Raw(mapping.PhysicalStart);
                ulong mappingEnd = Raw(mapping.PhysicalEndExclusive);
                if (start >= mappingStart && end <= mappingEnd)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Canonical identity mapping was absent.");
        }
    }
}
