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

internal static unsafe partial class M01LoadedImageStackIntegrationTests
{
    private sealed class CanonicalFixture : IDisposable
    {
        public const ulong ImagePages = 3;
        public const ulong ImageBytes = ImagePages * BootReservedRange.PageSize;
        private const ulong BackingPages = 24;
        private const ulong BackingBytes = BackingPages * BootReservedRange.PageSize;
        private const ulong LedgerSlotCapacity = 8;
        private const ulong StaticStateOffset = 128;
        private const ulong EvidenceScratchOffset = 1024;
        private const ulong ContinuationOffset = BootReservedRange.PageSize + 256;
        private const ulong CodePageOffset = 4 * BootReservedRange.PageSize;
        private const ulong DataArenaOffset = 6 * BootReservedRange.PageSize;
        private const ulong AllocationTableOffset = 12 * BootReservedRange.PageSize;
        private const ulong AlternateOwnedMapOffset = 13 * BootReservedRange.PageSize;
        private const ulong LedgerOffset = 17 * BootReservedRange.PageSize;
        private const ulong MapSourceOffset = 22 * BootReservedRange.PageSize;
        private byte* _backingMemory;
        private byte* _imageMemory;
        private byte* _dataArena;
        public CanonicalFixture()
        {
            try
            {
                _backingMemory = (byte*)NativeMemory.AlignedAlloc((nuint)BackingBytes, (nuint)PageSize);
                if (_backingMemory == null)
                {
                    throw new OutOfMemoryException("Unable to allocate the M01-017 test fixture.");
                }

                _imageMemory = _backingMemory;
                CodePage = _backingMemory + CodePageOffset;
                _dataArena = _backingMemory + DataArenaOffset;
                Records = (BootMemoryRegionRecord*)(_backingMemory + AllocationTableOffset);
                Ledger = (BootReservedRange*)(_backingMemory + LedgerOffset);
                StaticState = (M01LoadedImageStackStaticState*)(_imageMemory + StaticStateOffset);
                EvidenceScratch = (M01LoadedImageStackEvidence*)(_imageMemory + EvidenceScratchOffset);
                ResetCanonical();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public byte* CodePage { get; private set; }
        public BootMemoryRegionRecord* Records { get; private set; }
        public BootReservedRange* Ledger { get; private set; }
        public M01LoadedImageStackStaticState* StaticState { get; private set; }
        public M01LoadedImageStackEvidence* EvidenceScratch { get; private set; }
        public ulong ImageBase => (ulong)_imageMemory;
        public ulong AlternateOwnedMapAddress => (ulong)(_backingMemory + AlternateOwnedMapOffset);
        public ulong CodePhysicalStart { get; private set; }
        public ulong DataPhysicalStart { get; private set; }
        public ulong DataVirtualStart { get; private set; }
        public ulong LedgerCount { get; private set; }
        public ulong LedgerCapacity => LedgerSlotCapacity;
        public EfiLoadedImageRangeCapturePlan LoadedImage { get; private set; }
        public VirtualAddress Continuation { get; private set; }
        public FirmwareMemoryMapRecord RefreshedMap { get; set; }

        public void ResetCanonical()
        {
            Clear(_backingMemory, BackingBytes);
            EfiLoadedImageProtocolObservation observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 1, ImageBase, ImageBase, ImageBytes);
            LoadedImage = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, EfiLoadedImageRange.PageSize);
            if (!LoadedImage.Succeeded)
            {
                throw new InvalidOperationException("Canonical loaded-image capture failed.");
            }

            CodePhysicalStart = (ulong)CodePage;
            DataPhysicalStart = (ulong)_dataArena;
            DataVirtualStart = DataPhysicalStart;
            Records[0] = new BootMemoryRegionRecord((uint)UefiMemoryType.LoaderCode, CodePhysicalStart, CodePhysicalStart, 1, 0, BootMemoryRegionRecord.PageSize, CodePhysicalStart + BootMemoryRegionRecord.PageSize);
            Records[1] = new BootMemoryRegionRecord((uint)UefiMemoryType.LoaderData, DataPhysicalStart, DataVirtualStart, ArenaPages, 0, ArenaPages * BootMemoryRegionRecord.PageSize, DataPhysicalStart + ArenaPages * BootMemoryRegionRecord.PageSize);
            Ledger[0] = LoadedImage.Range.Reservation;
            Ledger[1] = CreateRange(BootReservationKind.BootStackSwitchStub, CodePhysicalStart, 1);
            Ledger[2] = CreateRange(BootReservationKind.BootStackGuard, DataPhysicalStart, GuardPages);
            Ledger[3] = CreateRange(BootReservationKind.BootStack, DataPhysicalStart + GuardPages * BootReservedRange.PageSize, StackPages);
            LedgerCount = 4;
            Continuation = Virtual(ImageBase + ContinuationOffset);
            if (!Continuation.IsValid)
            {
                throw new InvalidOperationException("Canonical continuation address failed.");
            }

            RefreshedMap = new FirmwareMemoryMapRecord
            {
                BufferAddress = ImageBase + 2 * BootReservedRange.PageSize,
                BufferSize = 80,
                SourceBufferAddress = (ulong)(_backingMemory + MapSourceOffset),
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = RefreshedMapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
        }

        public M01StageOneAllocationEvidence CreateAllocationEvidence()
        {
            return M01StageOneAllocationEvidence.CaptureCallerAttested(Records, 2, 2, CodeAllocationOrdinal, DataAllocationOrdinal, M01StageOneAllocationEvidence.EfiSuccess, M01StageOneAllocationEvidence.EfiSuccess);
        }

        public M01LoadedImageVirtualMappingEvidence CreateVirtualMapping()
        {
            return M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(LoadedImage);
        }

        public M01StageOneAddressMappingEvidence CreateAddressMapping()
        {
            return M01StageOneAddressMappingEvidence.CaptureCallerAttestedIdentity(Records, 2, 2, RefreshedMap);
        }

        public M01StageOneMapLedgerEvidence CreateMapEvidence(M01StageOneAllocationEvidence allocations, ulong previousMapKey = PreviousMapKey, ulong previousMapGeneration = PreviousMapGeneration, FirmwareMemoryMapRecord? refreshedMap = null, ulong refreshedMapGeneration = RefreshedMapGeneration, ulong mapCaptureOrdinal = MapCaptureOrdinal, ulong ledgerBuildOrdinal = LedgerBuildOrdinal, BootReservedRange* ledger = null, ulong? ledgerCount = null, ulong? ledgerCapacity = null)
        {
            BootReservedRange* selectedLedger = ledger == null ? Ledger : ledger;
            return M01StageOneMapLedgerEvidence.CaptureCallerAttested(previousMapKey, previousMapGeneration, refreshedMap ?? RefreshedMap, refreshedMapGeneration, allocations, mapCaptureOrdinal, ledgerBuildOrdinal, selectedLedger, ledgerCount ?? LedgerCount, ledgerCapacity ?? LedgerCapacity);
        }

        public M01LoadedImageStackIntegrationResult PrepareCurrent()
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackStaticPreparationResult PrepareFromStaticScratchCurrent()
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return InvokeStatic(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, EvidenceScratch, M01LoadedImageStackIntegration.RequiredEvidenceScratchBytes);
        }

        public M01LoadedImageStackStaticPreparationResult InvokeStatic(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence virtualMapping, M01StageOneAddressMappingEvidence addressMapping, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01StageOneAllocationEvidence allocations, M01StageOneMapLedgerEvidence mapLedger, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, byte* codePage, ulong codeCapacity, ulong materializedLength, ulong stubOffset, ulong declaredLength, VirtualAddress continuation, M01LoadedImageStackStaticState* staticState, ulong staticCapacity, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity)
        {
            return M01LoadedImageStackIntegration.PrepareFromStaticScratch(loadedImage, virtualMapping, addressMapping, records, recordCount, recordCapacity, allocations, mapLedger, ledger, ledgerCount, ledgerCapacity, codePage, codeCapacity, materializedLength, stubOffset, declaredLength, continuation, staticState, staticCapacity, evidenceScratch, evidenceScratchCapacity);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithLoadedImage(EfiLoadedImageRangeCapturePlan loadedImage)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(loadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithAllocationTable(BootMemoryRegionRecord* records, ulong count, ulong capacity)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), records, count, capacity, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithAllocationEvidence(M01StageOneAllocationEvidence allocations)
        {
            M01StageOneAllocationEvidence canonical = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(canonical), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithEvidence(M01StageOneAllocationEvidence allocations, M01StageOneMapLedgerEvidence mapLedger)
        {
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, mapLedger, Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithVirtualMapping(M01LoadedImageVirtualMappingEvidence mapping)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, mapping, CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithAddressMapping(M01StageOneAddressMappingEvidence mapping)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), mapping, Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithStorage(byte* codePage, ulong codeCapacity, ulong materializedLength, ulong stubOffset, ulong declaredLength, VirtualAddress continuation, M01LoadedImageStackStaticState* staticState, ulong staticCapacity)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, codePage, codeCapacity, materializedLength, stubOffset, declaredLength, continuation, staticState, staticCapacity, M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithStub(ulong stubOffset, ulong declaredLength)
        {
            return PrepareWithStorage(CodePage, PageSize, PageSize, stubOffset, declaredLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithMode(M01StageOneStubByteMode mode)
        {
            M01StageOneAllocationEvidence allocations = CreateAllocationEvidence();
            return Invoke(LoadedImage, CreateVirtualMapping(), CreateAddressMapping(), Records, 2, 2, allocations, CreateMapEvidence(allocations), Ledger, LedgerCount, LedgerCapacity, CodePage, PageSize, PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Continuation, StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes, mode);
        }

        public void InsertLedgerEntry(int index, BootReservedRange value)
        {
            if (index < 0 || (ulong)index > LedgerCount || LedgerCount == LedgerCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            for (ulong current = LedgerCount; current > (ulong)index; current--)
            {
                Ledger[current] = Ledger[current - 1];
            }

            Ledger[index] = value;
            LedgerCount++;
        }

        public void RebuildCanonicalLedgerFromRecords()
        {
            BootReservedRange[] ranges = [LoadedImage.Range.Reservation, CreateRange(BootReservationKind.BootStackSwitchStub, Records[0].PhysicalStart, Records[0].PageCount), CreateRange(BootReservationKind.BootStackGuard, Records[1].PhysicalStart, 1), CreateRange(BootReservationKind.BootStack, Records[1].PhysicalStart + BootReservedRange.PageSize, Records[1].PageCount - 1)];
            WriteSortedLedger(ranges);
        }

        public void AddOwnedMapReservation(ulong physicalStart)
        {
            BootReservedRange[] ranges = new BootReservedRange[checked((int)LedgerCount + 1)];
            for (int index = 0; index < (int)LedgerCount; index++)
            {
                ranges[index] = Ledger[index];
            }

            ranges[^1] = CreateRange(BootReservationKind.OwnedMemoryMap, physicalStart, 1);
            WriteSortedLedger(ranges);
        }

        public void SwapLedgerEntries(int first, int second)
        {
            if (first < 0 || second < 0 || (ulong)first >= LedgerCount || (ulong)second >= LedgerCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            (Ledger[first], Ledger[second]) = (Ledger[second], Ledger[first]);
        }

        public void WriteAuditedStub()
        {
            CodePage[StubEntryOffset + 0] = OwnedBootStackSwitchPlan.StubByte00;
            CodePage[StubEntryOffset + 1] = OwnedBootStackSwitchPlan.StubByte01;
            CodePage[StubEntryOffset + 2] = OwnedBootStackSwitchPlan.StubByte02;
            CodePage[StubEntryOffset + 3] = OwnedBootStackSwitchPlan.StubByte03;
            CodePage[StubEntryOffset + 4] = OwnedBootStackSwitchPlan.StubByte04;
            CodePage[StubEntryOffset + 5] = OwnedBootStackSwitchPlan.StubByte05;
            CodePage[StubEntryOffset + 6] = OwnedBootStackSwitchPlan.StubByte06;
            CodePage[StubEntryOffset + 7] = OwnedBootStackSwitchPlan.StubByte07;
            CodePage[StubEntryOffset + 8] = OwnedBootStackSwitchPlan.StubByte08;
            CodePage[StubEntryOffset + 9] = OwnedBootStackSwitchPlan.StubByte09;
            CodePage[StubEntryOffset + 10] = OwnedBootStackSwitchPlan.StubByte10;
            CodePage[StubEntryOffset + 11] = OwnedBootStackSwitchPlan.StubByte11;
            CodePage[StubEntryOffset + 12] = OwnedBootStackSwitchPlan.StubByte12;
            CodePage[StubEntryOffset + 13] = OwnedBootStackSwitchPlan.StubByte13;
        }

        public void ZeroMutableStorage()
        {
            Clear(CodePage, PageSize);
            Clear(_imageMemory, ImageBytes);
        }

        public bool CodeAndStateAreZero()
        {
            return IsZero(CodePage, PageSize) && IsZero((byte*)StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyCodePage()
        {
            return CopyBytes(CodePage, PageSize);
        }

        public byte[] CopyStaticState()
        {
            return CopyBytes((byte*)StaticState, M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyBackingMemory()
        {
            return CopyBytes(_backingMemory, BackingBytes);
        }

        public BootMemoryRegionRecord[] CopyAllocationRecords()
        {
            return[Records[0], Records[1]];
        }

        public BootReservedRange[] CopyLedger()
        {
            BootReservedRange[] copy = new BootReservedRange[(int)LedgerCount];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = Ledger[index];
            }

            return copy;
        }

        public void Dispose()
        {
            if (_backingMemory != null)
            {
                NativeMemory.AlignedFree(_backingMemory);
                _backingMemory = null;
            }

            _imageMemory = null;
            _dataArena = null;
            CodePage = null;
            Records = null;
            Ledger = null;
            StaticState = null;
            EvidenceScratch = null;
        }

        private static M01LoadedImageStackIntegrationResult Invoke(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence virtualMapping, M01StageOneAddressMappingEvidence addressMapping, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01StageOneAllocationEvidence allocations, M01StageOneMapLedgerEvidence mapLedger, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, byte* codePage, ulong codeCapacity, ulong materializedLength, ulong stubOffset, ulong declaredLength, VirtualAddress continuation, M01LoadedImageStackStaticState* staticState, ulong staticCapacity, M01StageOneStubByteMode mode)
        {
            return M01LoadedImageStackIntegration.PrepareWithStubByteMode(loadedImage, virtualMapping, addressMapping, records, recordCount, recordCapacity, allocations, mapLedger, ledger, ledgerCount, ledgerCapacity, codePage, codeCapacity, materializedLength, stubOffset, declaredLength, continuation, staticState, staticCapacity, mode);
        }

        private void WriteSortedLedger(BootReservedRange[] ranges)
        {
            if ((ulong)ranges.Length > LedgerCapacity)
            {
                throw new InvalidOperationException("M01-017 ledger fixture capacity exceeded.");
            }

            Array.Sort(ranges, static (left, right) =>
            {
                left.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong leftStart);
                right.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong rightStart);
                return leftStart.CompareTo(rightStart);
            });
            Clear((byte*)Ledger, LedgerSlotCapacity * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < ranges.Length; index++)
            {
                Ledger[index] = ranges[index];
            }

            LedgerCount = (ulong)ranges.Length;
        }

        private static void Clear(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                start[index] = 0;
            }
        }

        private static bool IsZero(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                if (start[index] != 0)
                {
                    return false;
                }
            }

            return true;
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
