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
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe partial class M01LiveAllocatorIntegrationTests
{
    private sealed class CanonicalFixture : IDisposable
    {
        public const ulong StageOneImagePages = 3;
        public const ulong StageOneImageBytes = StageOneImagePages * BootReservedRange.PageSize;
        public const ulong StageOneMapKey = 0x0010;
        public const ulong StageOneMapGeneration = 8;
        public const ulong ArenaAllocationOrdinal = 5;
        public const ulong StageTwoMapGeneration = 9;
        public const ulong StageTwoMapCaptureOrdinal = 6;
        public const ulong StageTwoLedgerBuildOrdinal = 7;
        private const ulong BackingPages = 48;
        private const ulong BackingBytes = BackingPages * BootReservedRange.PageSize;
        private const ulong StageOnePreviousMapKey = 0xf000;
        private const ulong StageOnePreviousMapGeneration = 7;
        private const ulong StageOneCodeAllocationOrdinal = 1;
        private const ulong StageOneDataAllocationOrdinal = 2;
        private const ulong StageOneMapCaptureOrdinal = 3;
        private const ulong StageOneLedgerBuildOrdinal = 4;
        private const ulong StageTwoMapKey = 0x0020;
        private const ulong GuardPages = 1;
        private const ulong StackPages = 3;
        private const ulong StageOneDataPages = GuardPages + StackPages;
        private const ulong StubEntryOffset = 16;
        private const ulong StageOneStaticStateOffset = 128;
        private const ulong ContinuationOffset = BootReservedRange.PageSize + 256;
        private const ulong CodePageOffset = 4 * BootReservedRange.PageSize;
        private const ulong DataArenaOffset = 6 * BootReservedRange.PageSize;
        private const ulong LiveArenaOffset = 12 * BootReservedRange.PageSize;
        private const ulong StageOneAllocationTableOffset = 18 * BootReservedRange.PageSize;
        private const ulong StageOneLedgerOffset = 19 * BootReservedRange.PageSize;
        private const ulong StageTwoAllocationTableOffset = 21 * BootReservedRange.PageSize;
        private const ulong StageTwoLedgerOffset = 23 * BootReservedRange.PageSize;
        private const ulong StageTwoStaticStateOffset = 25 * BootReservedRange.PageSize;
        private const ulong StageTwoMapDestinationOffset = 30 * BootReservedRange.PageSize;
        private const ulong StageTwoMapSourceOffset = 31 * BootReservedRange.PageSize;
        private const ulong StageOneMapSourceOffset = 40 * BootReservedRange.PageSize;
        private const ulong StageOneLedgerCapacity = 8;
        private const ulong StageTwoLedgerCapacity = 8;
        private byte* _backing;
        private BootMemoryRegionRecord* _stageOneRecords;
        private BootReservedRange* _stageOneLedger;
        private byte* _codePage;
        private byte* _dataArena;
        private ulong _ledgerCount;
        public CanonicalFixture()
        {
            try
            {
                _backing = (byte*)NativeMemory.AlignedAlloc((nuint)BackingBytes, (nuint)PageSize);
                if (_backing == null)
                {
                    throw new OutOfMemoryException("Unable to allocate the M01-018 fixture.");
                }

                _codePage = _backing + CodePageOffset;
                _dataArena = _backing + DataArenaOffset;
                _stageOneRecords = (BootMemoryRegionRecord*)(_backing + StageOneAllocationTableOffset);
                _stageOneLedger = (BootReservedRange*)(_backing + StageOneLedgerOffset);
                Records = (BootMemoryRegionRecord*)(_backing + StageTwoAllocationTableOffset);
                Ledger = (BootReservedRange*)(_backing + StageTwoLedgerOffset);
                StageOneState = (M01LoadedImageStackStaticState*)(_backing + StageOneStaticStateOffset);
                StaticState = (M01LiveAllocatorStaticState*)(_backing + StageTwoStaticStateOffset);
                ResetCanonical();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public BootMemoryRegionRecord* Records { get; private set; }
        public BootReservedRange* Ledger { get; private set; }
        public M01LoadedImageStackStaticState* StageOneState { get; private set; }
        public M01LiveAllocatorStaticState* StaticState { get; private set; }
        public ulong ArenaPages => 4;
        public ulong ArenaBytes => ArenaPages * BootReservedRange.PageSize;
        public ulong ArenaStart => (ulong)(_backing + LiveArenaOffset);
        public ulong UnrelatedRangeStart => (ulong)(_backing + 34 * BootReservedRange.PageSize);
        public ulong LedgerCount => _ledgerCount;
        public ulong LedgerCapacity => StageTwoLedgerCapacity;

        public ulong ArenaReservationIndex
        {
            get
            {
                for (ulong index = 0; index < _ledgerCount; index++)
                {
                    if (Ledger[index].Kind == BootReservationKind.AllocatorArena)
                    {
                        return index;
                    }
                }

                return M01LiveAllocatorIntegration.NoIndex;
            }
        }

        public bool HasFinalStageOne => StageOneState->IsInitialized && StageOneState->Status == M01LoadedImageStackStatus.Success && StageOneState->Checkpoint == M01LoadedImageStackCheckpoint.ContinuationRspContained && StageOneState->SwitchState.State == OwnedBootStackSwitchState.ContinuationObserved && StageOneState->StackLayout.Lifetime == OwnedBootStackLifetime.Active;
        public FirmwareMemoryMapRecord StageTwoMap { get; private set; }

        public void ResetCanonical(StageOneCompletion completion = StageOneCompletion.Final)
        {
            Clear(_backing, BackingBytes);
            EfiLoadedImageProtocolObservation observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 1, (ulong)_backing, (ulong)_backing, StageOneImageBytes);
            EfiLoadedImageRangeCapturePlan loadedImage = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
            {
                throw new InvalidOperationException("Canonical loaded-image capture failed.");
            }

            ulong codeStart = (ulong)_codePage;
            ulong dataStart = (ulong)_dataArena;
            _stageOneRecords[0] = new BootMemoryRegionRecord((uint)UefiMemoryType.LoaderCode, codeStart, codeStart, 1, 0, BootMemoryRegionRecord.PageSize, codeStart + BootMemoryRegionRecord.PageSize);
            _stageOneRecords[1] = new BootMemoryRegionRecord((uint)UefiMemoryType.LoaderData, dataStart, dataStart, StageOneDataPages, 0, StageOneDataPages * BootMemoryRegionRecord.PageSize, dataStart + StageOneDataPages * BootMemoryRegionRecord.PageSize);
            _stageOneLedger[0] = loadedImage.Range.Reservation;
            _stageOneLedger[1] = CreateRange(BootReservationKind.BootStackSwitchStub, codeStart, 1);
            _stageOneLedger[2] = CreateRange(BootReservationKind.BootStackGuard, dataStart, GuardPages);
            _stageOneLedger[3] = CreateRange(BootReservationKind.BootStack, dataStart + GuardPages * BootReservedRange.PageSize, StackPages);
            FirmwareMemoryMapRecord stageOneMap = new FirmwareMemoryMapRecord
            {
                BufferAddress = (ulong)_backing + 2 * BootReservedRange.PageSize,
                BufferSize = 80,
                SourceBufferAddress = (ulong)(_backing + StageOneMapSourceOffset),
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = StageOneMapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
            M01StageOneAllocationEvidence stageOneAllocation = M01StageOneAllocationEvidence.CaptureCallerAttested(_stageOneRecords, 2, 2, StageOneCodeAllocationOrdinal, StageOneDataAllocationOrdinal, M01StageOneAllocationEvidence.EfiSuccess, M01StageOneAllocationEvidence.EfiSuccess);
            M01StageOneMapLedgerEvidence stageOneMapLedger = M01StageOneMapLedgerEvidence.CaptureCallerAttested(StageOnePreviousMapKey, StageOnePreviousMapGeneration, stageOneMap, StageOneMapGeneration, stageOneAllocation, StageOneMapCaptureOrdinal, StageOneLedgerBuildOrdinal, _stageOneLedger, 4, StageOneLedgerCapacity);
            M01LoadedImageStackIntegrationResult prepared = M01LoadedImageStackIntegration.PrepareWithStubByteMode(loadedImage, M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(loadedImage), M01StageOneAddressMappingEvidence.CaptureCallerAttestedIdentity(_stageOneRecords, 2, 2, stageOneMap), _stageOneRecords, 2, 2, stageOneAllocation, stageOneMapLedger, _stageOneLedger, 4, StageOneLedgerCapacity, _codePage, BootReservedRange.PageSize, BootReservedRange.PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Virtual((ulong)_backing + ContinuationOffset), StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
            if (!prepared.IsPrepared)
            {
                throw new InvalidOperationException("Canonical stage-one prepare failed: " + prepared.Status);
            }

            if (completion >= StageOneCompletion.TransferRecorded)
            {
                M01LoadedImageStackStaticTransitionResult transfer = M01LoadedImageStackIntegration.RecordTransferFromStaticState(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes);
                if (!transfer.TransferRecorded)
                {
                    throw new InvalidOperationException("Canonical static transfer failed: " + transfer.Status);
                }

                if (completion == StageOneCompletion.Final)
                {
                    M01LoadedImageStackStaticTransitionResult observed = M01LoadedImageStackIntegration.ObserveContinuationRspFromStaticState(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, transfer.StateResult.Plan.ExpectedContinuationStackPointer);
                    if (!observed.ContinuationRspContained)
                    {
                        throw new InvalidOperationException("Canonical continuation failed: " + observed.Status);
                    }
                }
            }

            Records[0] = new BootMemoryRegionRecord((uint)UefiMemoryType.LoaderData, ArenaStart, ArenaStart, ArenaPages, 0, ArenaBytes, ArenaStart + ArenaBytes);
            BootReservedRange[] ranges = [loadedImage.Range.Reservation, _stageOneLedger[1], _stageOneLedger[2], _stageOneLedger[3], CreateRange(BootReservationKind.AllocatorArena, ArenaStart, ArenaPages)];
            WriteSortedLedger(ranges);
            StageTwoMap = new FirmwareMemoryMapRecord
            {
                BufferAddress = (ulong)(_backing + StageTwoMapDestinationOffset),
                BufferSize = 80,
                SourceBufferAddress = (ulong)(_backing + StageTwoMapSourceOffset),
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = StageTwoMapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
        }

        public void ClearStageOneState()
        {
            Clear((byte*)StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public M01LiveAllocatorAllocationEvidence CreateAllocationEvidence()
        {
            return M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(Records, 1, 1, ArenaAllocationOrdinal, M01LiveAllocatorAllocationEvidence.EfiSuccess);
        }

        public M01LiveAllocatorMapLedgerEvidence CreateMapEvidence(M01LiveAllocatorAllocationEvidence allocation, ulong previousMapKey = StageOneMapKey, ulong previousMapGeneration = StageOneMapGeneration, FirmwareMemoryMapRecord? refreshedMap = null, ulong refreshedMapGeneration = StageTwoMapGeneration, ulong mapCaptureOrdinal = StageTwoMapCaptureOrdinal, ulong ledgerBuildOrdinal = StageTwoLedgerBuildOrdinal, BootReservedRange* ledger = null, ulong? ledgerCount = null, ulong? ledgerCapacity = null)
        {
            BootReservedRange* selectedLedger = ledger == null ? Ledger : ledger;
            return M01LiveAllocatorMapLedgerEvidence.CaptureCallerAttested(previousMapKey, previousMapGeneration, refreshedMap ?? StageTwoMap, refreshedMapGeneration, allocation, mapCaptureOrdinal, ledgerBuildOrdinal, selectedLedger, ledgerCount ?? LedgerCount, ledgerCapacity ?? LedgerCapacity);
        }

        public M01LiveAllocatorIntegrationResult ExecuteCurrent()
        {
            M01LiveAllocatorAllocationEvidence allocation = CreateAllocationEvidence();
            return Invoke(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, Records, 1, 1, allocation, CreateMapEvidence(allocation), Ledger, LedgerCount, LedgerCapacity, StaticState, M01LiveAllocatorStaticState.RequiredStorageBytes);
        }

        public M01LiveAllocatorIntegrationResult Invoke(M01LoadedImageStackStaticState* stageOne, ulong stageOneCapacity, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LiveAllocatorAllocationEvidence allocation, M01LiveAllocatorMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, M01LiveAllocatorStaticState* state, ulong stateCapacity)
        {
            return M01LiveAllocatorIntegration.Execute(stageOne, stageOneCapacity, records, recordCount, recordCapacity, allocation, map, ledger, ledgerCount, ledgerCapacity, state, stateCapacity);
        }

        public void AddReservation(BootReservedRange reservation)
        {
            BootReservedRange[] ranges = new BootReservedRange[checked((int)_ledgerCount + 1)];
            for (int index = 0; index < (int)_ledgerCount; index++)
            {
                ranges[index] = Ledger[index];
            }

            ranges[^1] = reservation;
            WriteSortedLedger(ranges);
        }

        public void RemoveReservationKind(BootReservationKind kind)
        {
            BootReservedRange[] ranges = new BootReservedRange[checked((int)_ledgerCount - 1)];
            int destination = 0;
            bool removed = false;
            for (int index = 0; index < (int)_ledgerCount; index++)
            {
                if (!removed && Ledger[index].Kind == kind)
                {
                    removed = true;
                    continue;
                }

                if (destination < ranges.Length)
                {
                    ranges[destination++] = Ledger[index];
                }
            }

            if (!removed || destination != ranges.Length)
            {
                throw new InvalidOperationException("Reservation kind was not present exactly once.");
            }

            WriteSortedLedger(ranges);
        }

        public void ReplaceReservationKind(BootReservationKind kind, BootReservedRange replacement)
        {
            BootReservedRange[] ranges = CopyLedger();
            bool replaced = false;
            for (int index = 0; index < ranges.Length; index++)
            {
                if (!replaced && ranges[index].Kind == kind)
                {
                    ranges[index] = replacement;
                    replaced = true;
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException("Reservation kind was not present.");
            }

            WriteSortedLedger(ranges);
        }

        public void SwapLedgerEntries(int first, int second)
        {
            (Ledger[first], Ledger[second]) = (Ledger[second], Ledger[first]);
        }

        public byte[] CopyStageOneState()
        {
            return CopyBytes((byte*)StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyStaticState()
        {
            return CopyBytes((byte*)StaticState, M01LiveAllocatorStaticState.RequiredStorageBytes);
        }

        public BootMemoryRegionRecord[] CopyAllocationRecords()
        {
            return[Records[0]];
        }

        public BootReservedRange[] CopyLedger()
        {
            BootReservedRange[] copy = new BootReservedRange[(int)_ledgerCount];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = Ledger[index];
            }

            return copy;
        }

        public void Dispose()
        {
            if (_backing != null)
            {
                NativeMemory.AlignedFree(_backing);
                _backing = null;
            }

            _stageOneRecords = null;
            _stageOneLedger = null;
            _codePage = null;
            _dataArena = null;
            Records = null;
            Ledger = null;
            StageOneState = null;
            StaticState = null;
        }

        private void WriteSortedLedger(BootReservedRange[] ranges)
        {
            if ((ulong)ranges.Length > StageTwoLedgerCapacity)
            {
                throw new InvalidOperationException("M01-018 ledger capacity exceeded.");
            }

            Array.Sort(ranges, static (left, right) =>
            {
                left.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong leftStart);
                right.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong rightStart);
                return leftStart.CompareTo(rightStart);
            });
            Clear((byte*)Ledger, StageTwoLedgerCapacity * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < ranges.Length; index++)
            {
                Ledger[index] = ranges[index];
            }

            _ledgerCount = (ulong)ranges.Length;
        }

        private static void Clear(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                start[index] = 0;
            }
        }

        private static byte[] CopyBytes(byte* start, ulong length)
        {
            byte[] copy = new byte[checked((int)length)];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = start[index];
            }

            return copy;
        }
    }
}
