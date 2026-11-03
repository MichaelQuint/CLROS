using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
{
    private sealed class StressArena
    {
        private readonly ulong _baseAddress;
        private readonly int _totalPages;
        private readonly int _logicalNodeCount;
        private readonly int _nodeCapacity;
        private readonly BootPageFreeListNode[] _nodes;
        private readonly BootReservedRange[] _reservations;
        private readonly int _reservationCount;
        private readonly bool[] _usable;
        private readonly bool[] _allocated;
        private readonly bool[] _ownershipMirror;
        private readonly int[] _sourceFreeSegment;
        private readonly int[] _sourceUsableRange;
        private readonly ulong[] _shadowContent;
        private readonly ulong[] _actualContent;
        private readonly List<AllocationFrame> _frames = [];
        private readonly BootPageAllocatorState _baselineState;
        private readonly byte[] _baselineNodeBytes;
        private readonly byte[] _baselineReservationBytes;
        private readonly byte[] _baselineCapacityTailBytes;
        private BootPageAllocatorState _state;
        private int _successfulAllocations;
        private int _successfulFrees;
        private int _expectedAllocationFailures;
        private int _exactRestorationCount;
        private int _maximumDepth;
        private bool _firstBoundaryPageSeen;
        private bool _lastBoundaryPageSeen;
        private bool _capacityTailPreserved = true;
        private bool _reservationsPreserved = true;
        private StressArena(ulong baseAddress, int totalPages, ArenaRegion[] regions, int[] activeNodeIndexes, int logicalNodeCount, int nodeCapacity)
        {
            if (baseAddress == 0 || baseAddress % PageSize != 0 || totalPages <= 0 || regions.Length == 0 || activeNodeIndexes.Length != regions.Length || logicalNodeCount < regions.Length || nodeCapacity < logicalNodeCount)
            {
                throw new ArgumentException("invalid_stress_arena");
            }

            _baseAddress = baseAddress;
            _totalPages = totalPages;
            _logicalNodeCount = logicalNodeCount;
            _nodeCapacity = nodeCapacity;
            _nodes = new BootPageFreeListNode[nodeCapacity];
            _usable = new bool[totalPages];
            _allocated = new bool[totalPages];
            _ownershipMirror = new bool[totalPages];
            _sourceFreeSegment = new int[totalPages];
            _sourceUsableRange = new int[totalPages];
            _shadowContent = new ulong[totalPages];
            _actualContent = new ulong[totalPages];
            Array.Fill(_sourceFreeSegment, -1);
            Array.Fill(_sourceUsableRange, -1);
            int freePages = 0;
            for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
            {
                ArenaRegion region = regions[regionIndex];
                if (region.StartPage < 0 || region.PageCount <= 0 || region.StartPage + region.PageCount > totalPages || activeNodeIndexes[regionIndex] < 0 || activeNodeIndexes[regionIndex] >= logicalNodeCount)
                {
                    throw new ArgumentException("invalid_stress_region");
                }

                for (int page = region.StartPage; page < region.StartPage + region.PageCount; page++)
                {
                    if (_usable[page])
                    {
                        throw new ArgumentException("overlapping_stress_regions");
                    }

                    _usable[page] = true;
                    _sourceFreeSegment[page] = region.SourceFreeSegmentIndex;
                    _sourceUsableRange[page] = region.SourceUsableRangeIndex;
                }

                int nodeIndex = activeNodeIndexes[regionIndex];
                int nextNodeIndex = regionIndex + 1 < regions.Length ? activeNodeIndexes[regionIndex + 1] : -1;
                _nodes[nodeIndex] = Node(region.SourceFreeSegmentIndex, region.SourceUsableRangeIndex, checked(baseAddress + (ulong)region.StartPage * PageSize), (ulong)region.PageCount, nextNodeIndex);
                freePages += region.PageCount;
            }

            for (int index = logicalNodeCount; index < nodeCapacity; index++)
            {
                _nodes[index] = CapacityTailCanary(index);
            }

            List<BootReservedRange> reservations = [];
            int cursor = 0;
            foreach (ArenaRegion region in regions)
            {
                if (region.StartPage > cursor)
                {
                    reservations.Add(Reservation(ReservationKindForIndex(reservations.Count), checked(baseAddress + (ulong)cursor * PageSize), (ulong)(region.StartPage - cursor)));
                }

                cursor = region.StartPage + region.PageCount;
            }

            if (cursor < totalPages)
            {
                reservations.Add(Reservation(ReservationKindForIndex(reservations.Count), checked(baseAddress + (ulong)cursor * PageSize), (ulong)(totalPages - cursor)));
            }

            _reservationCount = reservations.Count;
            _reservations = new BootReservedRange[_reservationCount + 1];
            reservations.CopyTo(_reservations);
            _reservations[^1] = Reservation(BootReservationKind.PageTableArena, checked(baseAddress + (ulong)(totalPages + 512) * PageSize), 1);
            _state = new BootPageAllocatorState(isInitialized: true, PageSize, activeNodeIndexes[0], regions.Length, (ulong)freePages, checked((ulong)freePages * PageSize), allocatedPageCount: 0, allocatedByteCount: 0);
            _baselineState = _state;
            _baselineNodeBytes = StructArrayBytes(_nodes);
            _baselineReservationBytes = StructArrayBytes(_reservations);
            _baselineCapacityTailBytes = _baselineNodeBytes.AsSpan(checked(logicalNodeCount * (int)BootPageFreeListNode.StructureSize)).ToArray();
            if (!Validate(out string reason))
            {
                throw new InvalidOperationException("invalid_stress_baseline:" + reason);
            }
        }

        public int AllocationDepth => _frames.Count;
        public int UsablePageCount => _usable.Count(value => value);

        public bool HasFreePage
        {
            get
            {
                for (int page = 0; page < _totalPages; page++)
                {
                    if (_usable[page] && !_allocated[page])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsAtBaseline => _frames.Count == 0 && StatesEqual(_state, _baselineState) && _baselineNodeBytes.AsSpan().SequenceEqual(StructArrayBytes(_nodes)) && _baselineReservationBytes.AsSpan().SequenceEqual(StructArrayBytes(_reservations)) && Validate(out _);

        public static StressArena CreateCanonical()
        {
            return new StressArena(baseAddress: 0x0020_0000, totalPages: 128, regions: [new ArenaRegion(0, 10, 10, 100), new ArenaRegion(16, 32, 20, 200), new ArenaRegion(52, 40, 30, 300), new ArenaRegion(96, 32, 40, 400)], activeNodeIndexes: [0, 7, 19, 31], logicalNodeCount: 40, nodeCapacity: 44);
        }

        public static StressArena CreateSingleRegion(ulong baseAddress, int totalPages, int regionStartPage, int regionPageCount, int logicalNodeCount, int nodeCapacity)
        {
            return new StressArena(baseAddress, totalPages, [new ArenaRegion(regionStartPage, regionPageCount, sourceFreeSegmentIndex: 10, sourceUsableRangeIndex: 100)], activeNodeIndexes: [0], logicalNodeCount, nodeCapacity);
        }

        public BootPageAllocatorOnePageResult GetAllocationResultFromBottom(int index)
        {
            if (index < 0 || index >= _frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _frames[index].Allocation;
        }

        public unsafe OperationOutcome Allocate(ulong alignment, ulong contentTag, int step)
        {
            bool hasExpectedPage = TryFindFirstFit(alignment, out int expectedPage);
            byte[] nodesBefore = StructArrayBytes(_nodes);
            byte[] reservationsBefore = StructArrayBytes(_reservations);
            ulong[] actualContentBefore = (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore = (ulong[])_shadowContent.Clone();
            bool[] allocatedBefore = (bool[])_allocated.Clone();
            bool[] ownershipBefore = (bool[])_ownershipMirror.Clone();
            BootPageAllocatorState stateBefore = _state;
            int depthBefore = _frames.Count;
            BootPageAllocatorOnePageResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer = _reservations)
            {
                result = BootPageAllocatorMutator.AllocateOnePage(_state, nodePointer, (ulong)_logicalNodeCount, (ulong)_nodeCapacity, reservationPointer, (ulong)_reservationCount, (ulong)_reservations.Length, pageCount: 1, alignment);
            }

            if (!actualContentBefore.AsSpan().SequenceEqual(_actualContent) || !shadowContentBefore.AsSpan().SequenceEqual(_shadowContent) || !reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(_reservations)))
            {
                throw new InvalidOperationException("allocator_touched_host_content_or_reservations:" + step.ToString(CultureInfo.InvariantCulture));
            }

            if (!hasExpectedPage)
            {
                if (!IsAllocationFailure(result, stateBefore, BootPageAllocatorMutationStatus.NoSuitableRange) || !nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(_nodes)) || !allocatedBefore.AsSpan().SequenceEqual(_allocated) || !ownershipBefore.AsSpan().SequenceEqual(_ownershipMirror) || _frames.Count != depthBefore)
                {
                    throw new InvalidOperationException("allocation_failure_not_atomic:" + step.ToString(CultureInfo.InvariantCulture));
                }

                _expectedAllocationFailures++;
                RequireValid(step, "allocate_failure");
                return new OperationOutcome(succeeded: false, result.Status, physicalStart: 0, BootPageAllocatorOnePageCoalescingKind.Uninitialized);
            }

            ulong expectedAddress = checked(_baseAddress + (ulong)expectedPage * PageSize);
            if (!result.Succeeded || !result.CanRestore || Raw(result.PhysicalStart) != expectedAddress || Raw(result.PhysicalEndExclusive) != expectedAddress + PageSize || !StatesEqual(result.BeforeState, stateBefore) || result.NodeCount != (ulong)_logicalNodeCount || result.NodeCapacity != (ulong)_nodeCapacity || result.ReservationCount != (ulong)_reservationCount || result.ReservationCapacity != (ulong)_reservations.Length)
            {
                throw new InvalidOperationException("allocation_model_mismatch:" + step.ToString(CultureInfo.InvariantCulture) + ":expected=0x" + expectedAddress.ToString("X16", CultureInfo.InvariantCulture) + ":status=" + result.Status);
            }

            _frames.Add(new AllocationFrame(result, expectedPage, contentTag, nodesBefore, reservationsBefore));
            _state = result.AfterState;
            _allocated[expectedPage] = true;
            _ownershipMirror[expectedPage] = true;
            _shadowContent[expectedPage] = contentTag;
            _actualContent[expectedPage] = contentTag;
            _successfulAllocations++;
            if (_frames.Count > _maximumDepth)
            {
                _maximumDepth = _frames.Count;
            }

            int firstUsable = FirstUsablePage();
            int lastUsable = LastUsablePage();
            _firstBoundaryPageSeen |= expectedPage == firstUsable;
            _lastBoundaryPageSeen |= expectedPage == lastUsable;
            RequireValid(step, "allocate_success");
            return new OperationOutcome(succeeded: true, result.Status, expectedAddress, BootPageAllocatorOnePageCoalescingKind.Uninitialized);
        }

        public unsafe OperationOutcome FreeTop(int step)
        {
            if (_frames.Count == 0)
            {
                throw new InvalidOperationException("free_empty_stack:" + step.ToString(CultureInfo.InvariantCulture));
            }

            AllocationFrame frame = _frames[^1];
            BootPageAllocatorOnePageCoalescingKind expectedKind = ExpectedCoalescingKind(frame.PageIndex);
            byte[] reservationsBefore = StructArrayBytes(_reservations);
            ulong[] actualContentBefore = (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore = (ulong[])_shadowContent.Clone();
            BootPageAllocatorState stateBefore = _state;
            BootPageAllocatorOnePageFreeResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer = _reservations)
            {
                result = BootPageAllocatorMutator.FreeOnePage(_state, nodePointer, (ulong)_logicalNodeCount, (ulong)_nodeCapacity, reservationPointer, (ulong)_reservationCount, (ulong)_reservations.Length, frame.Allocation);
            }

            if (!actualContentBefore.AsSpan().SequenceEqual(_actualContent) || !shadowContentBefore.AsSpan().SequenceEqual(_shadowContent) || !reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(_reservations)) || !result.Succeeded || result.CoalescingKind != expectedKind || !StatesEqual(result.BeforeState, stateBefore) || !StatesEqual(result.AfterState, frame.Allocation.BeforeState) || Raw(result.PhysicalStart) != checked(_baseAddress + (ulong)frame.PageIndex * PageSize) || _actualContent[frame.PageIndex] != frame.ContentTag || _shadowContent[frame.PageIndex] != frame.ContentTag)
            {
                throw new InvalidOperationException("free_model_mismatch:" + step.ToString(CultureInfo.InvariantCulture) + ":status=" + result.Status + ":kind=" + result.CoalescingKind + ":expected=" + expectedKind);
            }

            _state = result.AfterState;
            _allocated[frame.PageIndex] = false;
            _ownershipMirror[frame.PageIndex] = false;
            _shadowContent[frame.PageIndex] = 0;
            _actualContent[frame.PageIndex] = 0;
            _frames.RemoveAt(_frames.Count - 1);
            _successfulFrees++;
            if (!frame.NodeBytesBefore.AsSpan().SequenceEqual(StructArrayBytes(_nodes)) || !frame.ReservationBytesBefore.AsSpan().SequenceEqual(StructArrayBytes(_reservations)))
            {
                throw new InvalidOperationException("exact_inverse_bytes_mismatch:" + step.ToString(CultureInfo.InvariantCulture));
            }

            _exactRestorationCount++;
            RequireValid(step, "free_success");
            return new OperationOutcome(succeeded: true, result.Status, Raw(result.PhysicalStart), result.CoalescingKind);
        }

        public unsafe bool ExpectFreeFailure(BootPageAllocatorOnePageResult allocation, BootPageAllocatorMutationStatus expectedStatus, int step)
        {
            byte[] nodesBefore = StructArrayBytes(_nodes);
            byte[] reservationsBefore = StructArrayBytes(_reservations);
            ulong[] actualContentBefore = (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore = (ulong[])_shadowContent.Clone();
            bool[] allocatedBefore = (bool[])_allocated.Clone();
            bool[] ownershipBefore = (bool[])_ownershipMirror.Clone();
            BootPageAllocatorState stateBefore = _state;
            int depthBefore = _frames.Count;
            BootPageAllocatorOnePageFreeResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer = _reservations)
            {
                result = BootPageAllocatorMutator.FreeOnePage(_state, nodePointer, (ulong)_logicalNodeCount, (ulong)_nodeCapacity, reservationPointer, (ulong)_reservationCount, (ulong)_reservations.Length, allocation);
            }

            return IsFreeFailure(result, stateBefore, allocation, expectedStatus) && nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(_nodes)) && reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(_reservations)) && actualContentBefore.AsSpan().SequenceEqual(_actualContent) && shadowContentBefore.AsSpan().SequenceEqual(_shadowContent) && allocatedBefore.AsSpan().SequenceEqual(_allocated) && ownershipBefore.AsSpan().SequenceEqual(_ownershipMirror) && _frames.Count == depthBefore && StatesEqual(_state, stateBefore) && Validate(out _);
        }

        public bool CorruptActualContentAndDetect(out string detectedReason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (!_allocated[page])
                {
                    continue;
                }

                ulong original = _actualContent[page];
                _actualContent[page] ^= 1;
                bool detected = !Validate(out detectedReason);
                _actualContent[page] = original;
                return detected;
            }

            detectedReason = "no_allocated_page";
            return false;
        }

        public bool CorruptShadowOwnershipAndDetect(out string detectedReason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (!_usable[page])
                {
                    continue;
                }

                _ownershipMirror[page] = !_ownershipMirror[page];
                bool detected = !Validate(out detectedReason);
                _ownershipMirror[page] = !_ownershipMirror[page];
                return detected;
            }

            detectedReason = "no_usable_page";
            return false;
        }

        public void AppendTranscript(CanonicalTranscript transcript, int step, ReplayOperation operation, ulong selectorWord, ulong inputWord, ulong tokenWord, OperationOutcome outcome)
        {
            transcript.AddInt32(step);
            transcript.AddByte((byte)operation);
            transcript.AddUInt64(selectorWord);
            transcript.AddUInt64(inputWord);
            transcript.AddUInt64(tokenWord);
            transcript.AddBool(outcome.Succeeded);
            transcript.AddInt32((int)outcome.Status);
            transcript.AddUInt64(outcome.PhysicalStart);
            transcript.AddByte((byte)outcome.CoalescingKind);
            transcript.AddBool(_state.IsInitialized);
            transcript.AddUInt64(_state.PageSize);
            transcript.AddInt32(_state.HeadNodeIndex);
            transcript.AddInt32(_state.FreeNodeCount);
            transcript.AddUInt64(_state.FreePageCount);
            transcript.AddUInt64(_state.FreeByteCount);
            transcript.AddUInt64(_state.AllocatedPageCount);
            transcript.AddUInt64(_state.AllocatedByteCount);
            transcript.AddInt32(_frames.Count);
            transcript.AddInt32(_successfulAllocations);
            transcript.AddInt32(_successfulFrees);
            transcript.AddInt32(_expectedAllocationFailures);
            transcript.AddInt32(_exactRestorationCount);
            transcript.AddInt32(_maximumDepth);
            for (int index = 0; index < _logicalNodeCount; index++)
            {
                BootPageFreeListNode node = _nodes[index];
                transcript.AddInt32(node.SourceFreeSegmentIndex);
                transcript.AddInt32(node.SourceUsableRangeIndex);
                transcript.AddUInt64(node.PhysicalStart);
                transcript.AddUInt64(node.PhysicalEndExclusive);
                transcript.AddUInt64(node.PageCount);
                transcript.AddUInt64(node.ByteLength);
                transcript.AddInt32(node.NextNodeIndex);
            }

            transcript.AddInt32(_reservationCount);
            transcript.AddInt32(_reservations.Length);
            for (int index = 0; index < _reservations.Length; index++)
            {
                BootReservedRange reservation = _reservations[index];
                transcript.AddByte((byte)reservation.Kind);
                transcript.AddUInt64(reservation.PhysicalStart.IsValid ? Raw(reservation.PhysicalStart) : 0);
                transcript.AddUInt64(reservation.PhysicalEndExclusive.IsValid ? Raw(reservation.PhysicalEndExclusive) : 0);
                transcript.AddUInt64(reservation.PageCount);
                transcript.AddUInt64(reservation.ByteLength);
            }

            transcript.AddInt32(_totalPages);
            for (int page = 0; page < _totalPages; page++)
            {
                transcript.AddBool(_usable[page]);
                transcript.AddBool(_allocated[page]);
                transcript.AddBool(_ownershipMirror[page]);
                transcript.AddInt32(_sourceFreeSegment[page]);
                transcript.AddInt32(_sourceUsableRange[page]);
                transcript.AddUInt64(_shadowContent[page]);
                transcript.AddUInt64(_actualContent[page]);
            }
        }

        public ReplaySummary CreateSummary(ulong seed, int churnSteps, int operationCount, string transcriptHash)
        {
            return new ReplaySummary(seed, churnSteps, operationCount, _successfulAllocations, _successfulFrees, _expectedAllocationFailures, _exactRestorationCount, _maximumDepth, _firstBoundaryPageSeen, _lastBoundaryPageSeen, _capacityTailPreserved, _reservationsPreserved, IsAtBaseline, transcriptHash);
        }

        public bool Validate(out string reason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (_allocated[page] != _ownershipMirror[page])
                {
                    reason = "ownership_mismatch:" + page.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                if (_actualContent[page] != _shadowContent[page])
                {
                    reason = "content_mismatch:" + page.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                if (!_usable[page])
                {
                    if (_allocated[page] || _shadowContent[page] != 0 || _sourceFreeSegment[page] != -1 || _sourceUsableRange[page] != -1)
                    {
                        reason = "reserved_page_state:" + page.ToString(CultureInfo.InvariantCulture);
                        return false;
                    }
                }
                else if (_allocated[page] != (_shadowContent[page] != 0))
                {
                    reason = "content_ownership_mismatch:" + page.ToString(CultureInfo.InvariantCulture);
                    return false;
                }
            }

            List<ExpectedExtent> expected = BuildExpectedExtents();
            if (_state.FreeNodeCount != expected.Count)
            {
                reason = "free_node_count:" + _state.FreeNodeCount.ToString(CultureInfo.InvariantCulture) + ":" + expected.Count.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (expected.Count == 0)
            {
                if (_state.HeadNodeIndex != -1)
                {
                    reason = "nonempty_head_for_empty_model";
                    return false;
                }
            }
            else if (_state.HeadNodeIndex < 0 || _state.HeadNodeIndex >= _logicalNodeCount)
            {
                reason = "invalid_head";
                return false;
            }

            bool[] visited = new bool[_logicalNodeCount];
            int nodeIndex = _state.HeadNodeIndex;
            ulong freePages = 0;
            for (int extentIndex = 0; extentIndex < expected.Count; extentIndex++)
            {
                if (nodeIndex < 0 || nodeIndex >= _logicalNodeCount || visited[nodeIndex])
                {
                    reason = "chain_index:" + extentIndex.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                visited[nodeIndex] = true;
                ExpectedExtent extent = expected[extentIndex];
                BootPageFreeListNode node = _nodes[nodeIndex];
                ulong expectedStart = checked(_baseAddress + (ulong)extent.StartPage * PageSize);
                ulong expectedBytes = checked((ulong)extent.PageCount * PageSize);
                if (node.SourceFreeSegmentIndex != extent.SourceFreeSegmentIndex || node.SourceUsableRangeIndex != extent.SourceUsableRangeIndex || node.PhysicalStart != expectedStart || node.PhysicalEndExclusive != expectedStart + expectedBytes || node.PageCount != (ulong)extent.PageCount || node.ByteLength != expectedBytes)
                {
                    reason = "extent_mismatch:" + extentIndex.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                freePages += node.PageCount;
                nodeIndex = node.NextNodeIndex;
            }

            if (nodeIndex != -1)
            {
                reason = "chain_not_terminated";
                return false;
            }

            for (int index = 0; index < _logicalNodeCount; index++)
            {
                if (!visited[index] && !IsRawZeroNode(_nodes[index]))
                {
                    reason = "inactive_slot_not_zero:" + index.ToString(CultureInfo.InvariantCulture);
                    return false;
                }
            }

            ulong allocatedPages = 0;
            for (int page = 0; page < _totalPages; page++)
            {
                if (_allocated[page])
                {
                    allocatedPages++;
                }
            }

            if (!_state.IsInitialized || _state.PageSize != PageSize || _state.FreePageCount != freePages || _state.FreeByteCount != freePages * PageSize || _state.AllocatedPageCount != allocatedPages || _state.AllocatedByteCount != allocatedPages * PageSize || freePages + allocatedPages != (ulong)UsablePageCount)
            {
                reason = "state_accounting";
                return false;
            }

            byte[] nodeBytes = StructArrayBytes(_nodes);
            byte[] currentTail = nodeBytes.AsSpan(checked(_logicalNodeCount * (int)BootPageFreeListNode.StructureSize)).ToArray();
            if (!_baselineCapacityTailBytes.AsSpan().SequenceEqual(currentTail))
            {
                _capacityTailPreserved = false;
                reason = "capacity_tail_changed";
                return false;
            }

            if (!_baselineReservationBytes.AsSpan().SequenceEqual(StructArrayBytes(_reservations)))
            {
                _reservationsPreserved = false;
                reason = "reservations_changed";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void RequireValid(int step, string operation)
        {
            if (!Validate(out string reason))
            {
                throw new InvalidOperationException(operation + ":" + step.ToString(CultureInfo.InvariantCulture) + ":" + reason);
            }
        }

        private bool TryFindFirstFit(ulong alignment, out int pageIndex)
        {
            pageIndex = -1;
            if (alignment < PageSize || (alignment & (alignment - 1)) != 0 || alignment % PageSize != 0)
            {
                return false;
            }

            foreach (ExpectedExtent extent in BuildExpectedExtents())
            {
                ulong start = checked(_baseAddress + (ulong)extent.StartPage * PageSize);
                ulong end = checked(start + (ulong)extent.PageCount * PageSize);
                ulong mask = alignment - 1;
                if (start > ulong.MaxValue - mask)
                {
                    continue;
                }

                ulong aligned = (start + mask) & ~mask;
                if (aligned > ulong.MaxValue - PageSize || aligned + PageSize > end)
                {
                    continue;
                }

                ulong offset = aligned - _baseAddress;
                if (offset % PageSize != 0)
                {
                    throw new InvalidOperationException("model_alignment_not_page_multiple");
                }

                pageIndex = checked((int)(offset / PageSize));
                return true;
            }

            return false;
        }

        private List<ExpectedExtent> BuildExpectedExtents()
        {
            List<ExpectedExtent> extents = [];
            int page = 0;
            while (page < _totalPages)
            {
                if (!_usable[page] || _allocated[page])
                {
                    page++;
                    continue;
                }

                int start = page;
                int sourceFreeSegment = _sourceFreeSegment[page];
                int sourceUsableRange = _sourceUsableRange[page];
                page++;
                while (page < _totalPages && _usable[page] && !_allocated[page] && _sourceFreeSegment[page] == sourceFreeSegment && _sourceUsableRange[page] == sourceUsableRange)
                {
                    page++;
                }

                extents.Add(new ExpectedExtent(start, page - start, sourceFreeSegment, sourceUsableRange));
            }

            return extents;
        }

        private BootPageAllocatorOnePageCoalescingKind ExpectedCoalescingKind(int pageIndex)
        {
            int sourceSegment = _sourceFreeSegment[pageIndex];
            int sourceRange = _sourceUsableRange[pageIndex];
            bool hasPrevious = pageIndex > 0 && _usable[pageIndex - 1] && !_allocated[pageIndex - 1] && _sourceFreeSegment[pageIndex - 1] == sourceSegment && _sourceUsableRange[pageIndex - 1] == sourceRange;
            bool hasNext = pageIndex + 1 < _totalPages && _usable[pageIndex + 1] && !_allocated[pageIndex + 1] && _sourceFreeSegment[pageIndex + 1] == sourceSegment && _sourceUsableRange[pageIndex + 1] == sourceRange;
            if (hasPrevious && hasNext)
            {
                return BootPageAllocatorOnePageCoalescingKind.PreviousAndNext;
            }

            if (hasPrevious)
            {
                return BootPageAllocatorOnePageCoalescingKind.Previous;
            }

            return hasNext ? BootPageAllocatorOnePageCoalescingKind.Next : BootPageAllocatorOnePageCoalescingKind.None;
        }

        private int FirstUsablePage()
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (_usable[page])
                {
                    return page;
                }
            }

            return -1;
        }

        private int LastUsablePage()
        {
            for (int page = _totalPages - 1; page >= 0; page--)
            {
                if (_usable[page])
                {
                    return page;
                }
            }

            return -1;
        }

        private static bool IsRawZeroNode(BootPageFreeListNode node)
        {
            ReadOnlySpan<BootPageFreeListNode> nodeSpan = MemoryMarshal.CreateReadOnlySpan(ref node, 1);
            foreach (byte value in MemoryMarshal.AsBytes(nodeSpan))
            {
                if (value != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static BootReservationKind ReservationKindForIndex(int index)
        {
            return (index % 4) switch
            {
                0 => BootReservationKind.LoadedImage,
                1 => BootReservationKind.BootStack,
                2 => BootReservationKind.OwnedMemoryMap,
                _ => BootReservationKind.AcpiSnapshot
            };
        }
    }
}
