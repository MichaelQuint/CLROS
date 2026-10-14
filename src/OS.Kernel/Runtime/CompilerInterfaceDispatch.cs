using OS.Boot;

namespace OS.Kernel.Runtime;

public enum CompilerInterfaceDispatchStatus : byte
{
    Uninitialized = 0, Success = 1, StorageRejected = 2, AlreadyBound = 3,
    NotBound = 4, StateChanged = 5, ModuleRejected = 6, AllocatorRejected = 7,
    CellRejected = 8, CellChanged = 9, UnsupportedCell = 10, NullReceiver = 11,
    ReceiverRejected = 12, MetadataRejected = 13, MetadataChanged = 14,
    UnsupportedType = 15, MapRejected = 16, SlotRejected = 17,
    TargetRejected = 18, IncompatibleInterface = 19, AmbiguousImplementation = 20,
    CapacityExceeded = 21, CacheChanged = 22
}

public struct CompilerInterfaceDispatchResolution
{
    public ulong Target, ReceiverType, RequestedInterface, ImplementedInterface, DeclaringType;
    public ulong AllocationSequence;
    public ushort InterfaceSlot, ImplementationSlot;
    public bool CacheHit;
}

public struct CompilerInterfaceDispatchProgress
{
    public uint CellCount, CacheCount, MetadataWords;
    public ulong Resolutions, CacheHits;
}

/// <summary>
/// Caller-authenticated native symbol extent. The pinned sealed table has no
/// in-band length. Bytes must be the exact original COFF contribution length,
/// including folded-table identity, not the surrounding PE section length.
/// Bind copies this short list; the input buffer need not remain alive.
/// </summary>
public struct CompilerInterfaceDispatchSealedTable
{
    public ulong DeclaringType, Table, Bytes;
}

/// <summary>
/// Exclusive, finite .NET 11 RC2 Win64 ordinary instance dispatch. Layout facts
/// are pinned to dotnet/dotnet 820230c386db5b0106c5430b3e5aee92a7826bf5.
/// The caller authenticates the compiler cell section and initial stub, retains
/// original image/heap mappings and owns the zero POD buffer for its lifetime.
/// Address checks are not mapping probes or a forgery detector. No target is
/// invoked here; the caller's ABI bridge must halt on every rejected resolution.
/// Cells remain unchanged. Metadata snapshots and the cache are external to them.
/// Dynamic types, arrays, value types, generic classes, default/static interface
/// implementations and generic virtual methods are outside this bounded slice.
/// </summary>
public static unsafe class CompilerInterfaceDispatch
{
    public const uint MaximumCells = 64, MaximumCacheEntries = 64, MaximumMetadataWords = 1024;
    public const uint MaximumRunCells = 100, MaximumHierarchyDepth = 16;
    public const uint MaximumSlots = 64, MaximumInterfaces = 64, MaximumMapRows = 64, MaximumGenericArguments = 8;
    public const uint MaximumSealedTables = 16;
    private const ulong Bound = 0x4D30343031364431;
    private const uint Class = 0x50000000, Interface = 0x54000000, ElementMask = 0x7C000000;
    private const uint Dispatch = 0x00040000, Sealed = 0x00400000, Variance = 0x00800000;
    private const uint Generic = 0x02000000, Definition = 0x00030000;

    private struct State
    {
        internal ulong Phase, Self, Capacity, ModuleOwner, ModuleCapacity, ModuleHandle, ModuleSlot;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal RhpNewFastHeapAccess Heap;
        internal RhpNewFastBumpState* Allocator;
        internal ulong AllocatorCapacity, InitialStub, CellsStart, CellsBytes;
        internal uint CellCapacity, CacheCapacity, CellCount, CacheCount, WordCount, SealedTableCount;
        internal ulong Resolutions, Hits;
    }
    private struct Word { internal ulong Address, Value, Bytes; }
    private struct Cache
    {
        internal ulong Receiver, Cell;
        internal CompilerInterfaceDispatchResolution Resolution;
    }
    private struct Type
    {
        internal ulong Address, Parent, Vtable, Interfaces, Map, SealedTable, GenericDefinition, Composition;
        internal uint Flags, BaseSize;
        internal ushort Slots, InterfaceCount;
    }

    public static ulong StateBytes => (ulong)sizeof(State);
    public static ulong RequiredStorage(uint cellCapacity, uint cacheCapacity) =>
        cellCapacity == 0 || cellCapacity > MaximumCells || cacheCapacity == 0 || cacheCapacity > MaximumCacheEntries
        ? 0 : StateBytes + cellCapacity * 32UL + cacheCapacity * (ulong)sizeof(Cache) +
            MaximumMetadataWords * (ulong)sizeof(Word) + MaximumSealedTables * (ulong)sizeof(CompilerInterfaceDispatchSealedTable);

    /// <summary>
    /// cellsStart/cellsBytes delimit the exact compiler-origin cell section,
    /// matching ReadyToRun section 210, not an arbitrary enclosing data section.
    /// Bind admits zero-initialized {MethodTable,Code} cells and the bounded
    /// NativeFormat array in section 209. Section 331 supplies relative type
    /// references. No monomorphic cache or executable cell pointer is installed.
    /// </summary>
    public static CompilerInterfaceDispatchStatus Bind(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle,
        RhpNewFastHeapAccess heap, RhpNewFastBumpState* allocator, ulong allocatorCapacity,
        byte* storage, ulong storageCapacity, ulong initialStub, ulong cellsStart, ulong cellsBytes,
        uint cellCapacity, uint cacheCapacity,
        CompilerInterfaceDispatchSealedTable* sealedTables = null, uint sealedTableCount = 0)
    {
        ulong required = RequiredStorage(cellCapacity, cacheCapacity);
        if (required == 0 || !Span((ulong)storage, storageCapacity, 8) || storageCapacity < required)
            return CompilerInterfaceDispatchStatus.StorageRejected;
        State candidate = default;
        candidate.Phase = Bound; candidate.Self = (ulong)storage; candidate.Capacity = storageCapacity;
        candidate.Capture = capture; candidate.Mapping = mapping;
        candidate.ModuleOwner = (ulong)moduleOwner; candidate.ModuleCapacity = moduleCapacity; candidate.ModuleHandle = moduleHandle;
        candidate.Heap = heap; candidate.Allocator = allocator; candidate.AllocatorCapacity = allocatorCapacity;
        candidate.InitialStub = initialStub; candidate.CellsStart = cellsStart; candidate.CellsBytes = cellsBytes;
        candidate.CellCapacity = cellCapacity; candidate.CacheCapacity = cacheCapacity;
        candidate.SealedTableCount = sealedTableCount;
        var module = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, 204);
        if (!module.Succeeded) return CompilerInterfaceDispatchStatus.ModuleRejected;
        candidate.ModuleSlot = module.Section;
        if (GuestTestRhpNewFast.ValidateCollectingOwner(heap, allocator, allocatorCapacity) != RhpNewFastStatus.Success)
            return CompilerInterfaceDispatchStatus.AllocatorRejected;
        if (!StorageRange(&candidate)) return CompilerInterfaceDispatchStatus.StorageRejected;
        if (sealedTableCount > MaximumSealedTables || (sealedTableCount == 0 ? sealedTables != null :
            !Span((ulong)sealedTables, sealedTableCount * 24UL, 8) ||
            Overlap((ulong)sealedTables, sealedTableCount * 24UL, (ulong)storage, storageCapacity)))
            return CompilerInterfaceDispatchStatus.StorageRejected;
        if (!SealedTableRanges(&candidate, sealedTables)) return CompilerInterfaceDispatchStatus.SlotRejected;
        if (!Executable(&candidate, initialStub)) return CompilerInterfaceDispatchStatus.TargetRejected;
        CompilerInterfaceDispatchStatus status = Cells(&candidate, 0, out _, out _, out uint count, false);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        for (ulong i = 0; i < required; i++) if (storage[i] != 0) return CompilerInterfaceDispatchStatus.AlreadyBound;
        candidate.CellCount = count;
        *(State*)storage = candidate;
        byte* snapshot = CellSnapshot((State*)storage);
        for (ulong i = 0; i < cellsBytes; i++) snapshot[i] = ((byte*)cellsStart)[i];
        CompilerInterfaceDispatchSealedTable* tables = SealedTables((State*)storage);
        for (uint i = 0; i < sealedTableCount; i++) tables[i] = sealedTables[i];
        // Admission above is read-only. Only after the exclusive owner buffer is
        // accepted do we retain every metadata word used by the new cell ABI.
        status = Cells((State*)storage, 0, out _, out _, out _, true);
        if (status != CompilerInterfaceDispatchStatus.Success)
        {
            for (ulong i = 0; i < required; i++) storage[i] = 0;
            return status;
        }
        return CompilerInterfaceDispatchStatus.Success;
    }

    public static CompilerInterfaceDispatchStatus Resolve(byte* storage, ulong capacity,
        ulong receiver, ulong cell, out CompilerInterfaceDispatchResolution resolution)
    {
        resolution = default;
        CompilerInterfaceDispatchStatus status = Validate(storage, capacity, out State* s);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if (s->Resolutions == ulong.MaxValue) return CompilerInterfaceDispatchStatus.CapacityExceeded;
        if (receiver == 0) return CompilerInterfaceDispatchStatus.NullReceiver;
        // The original ledger supplies the receipt. A caller's numeric object/MT
        // pair cannot authorize the first receiver read, including on a cache hit.
        RhpNewFastResult receipt = default;
        for (uint i = 0; i < GuestTestRhpNewFast.MaximumTrackedAllocations; i++)
        {
            RhpNewFastResult current = GuestTestRhpNewFast.ReadTrackedAllocation(s->Allocator, i, out bool staticBase);
            if (current.AllocationSucceeded && !staticBase && current.ObjectReference == receiver) { receipt = current; break; }
        }
        if (!receipt.AllocationSucceeded || GuestTestRhpNewFast.TryValidateRetainedAllocation(s->Heap,
            s->Allocator, s->AllocatorCapacity, receipt) != RhpNewFastStatus.Success)
            return CompilerInterfaceDispatchStatus.ReceiverRejected;
        status = Cells(s, cell, out ulong requested, out ushort slot, out _);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        uint oldWords = s->WordCount;
        status = Find(s, receipt.MethodTableToken, receipt.BytesConsumed, requested, slot, out CompilerInterfaceDispatchResolution found);
        if (status == CompilerInterfaceDispatchStatus.Success)
        {
            found.AllocationSequence = receipt.AllocationCount;
            Cache* cache = Entries(s);
            uint index = s->CacheCount;
            for (uint i = 0; i < s->CacheCount; i++)
                if (cache[i].Receiver == receiver && cache[i].Cell == cell) { index = i; break; }
            if (index < s->CacheCount && cache[index].Resolution.AllocationSequence == receipt.AllocationCount)
            {
                if (!Equal(cache[index].Resolution, found) || cache[index].Resolution.CacheHit)
                    status = CompilerInterfaceDispatchStatus.CacheChanged;
                else { found.CacheHit = true; s->Hits++; }
            }
            else if (index == s->CacheCapacity) status = CompilerInterfaceDispatchStatus.CapacityExceeded;
            else
            {
                cache[index].Receiver = receiver; cache[index].Cell = cell; cache[index].Resolution = found;
                if (index == s->CacheCount) s->CacheCount++;
            }
            if (status == CompilerInterfaceDispatchStatus.Success) { s->Resolutions++; resolution = found; return status; }
        }
        // Rejections do not retain a partial metadata observation or cache entry.
        Word* words = Words(s);
        while (s->WordCount > oldWords) words[--s->WordCount] = default;
        return status;
    }

    public static CompilerInterfaceDispatchStatus Observe(byte* storage, ulong capacity, out CompilerInterfaceDispatchProgress progress)
    {
        progress = default;
        CompilerInterfaceDispatchStatus status = Validate(storage, capacity, out State* s);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        progress.CellCount = s->CellCount; progress.CacheCount = s->CacheCount; progress.MetadataWords = s->WordCount;
        progress.Resolutions = s->Resolutions; progress.CacheHits = s->Hits;
        return status;
    }

    private static CompilerInterfaceDispatchStatus Validate(byte* storage, ulong capacity, out State* state)
    {
        state = null;
        if (!Span((ulong)storage, capacity, 8) || capacity < StateBytes) return CompilerInterfaceDispatchStatus.StorageRejected;
        State* s = (State*)storage;
        if (s->Phase == 0) return CompilerInterfaceDispatchStatus.NotBound;
        ulong required = RequiredStorage(s->CellCapacity, s->CacheCapacity);
        if (s->Phase != Bound || s->Self != (ulong)storage || s->Capacity != capacity || required == 0 || capacity < required ||
            s->CellCount == 0 || s->CellCount > s->CellCapacity || s->CacheCount > s->CacheCapacity ||
            s->WordCount > MaximumMetadataWords || s->SealedTableCount > MaximumSealedTables || s->Hits > s->Resolutions)
            return CompilerInterfaceDispatchStatus.StateChanged;
        var module = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, 204);
        if (!module.Succeeded || module.Section != s->ModuleSlot) return CompilerInterfaceDispatchStatus.ModuleRejected;
        if (GuestTestRhpNewFast.ValidateCollectingOwner(s->Heap, s->Allocator, s->AllocatorCapacity) != RhpNewFastStatus.Success)
            return CompilerInterfaceDispatchStatus.AllocatorRejected;
        if (!StorageRange(s)) return CompilerInterfaceDispatchStatus.StorageRejected;
        if (!SealedTableRanges(s, SealedTables(s))) return CompilerInterfaceDispatchStatus.SlotRejected;
        if (!Executable(s, s->InitialStub)) return CompilerInterfaceDispatchStatus.TargetRejected;
        CompilerInterfaceDispatchStatus status = Cells(s, 0, out _, out _, out uint count);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if (count != s->CellCount) return CompilerInterfaceDispatchStatus.CellChanged;
        byte* snapshot = CellSnapshot(s);
        for (ulong i = 0; i < s->CellsBytes; i++)
            if (snapshot[i] != ((byte*)s->CellsStart)[i]) return CompilerInterfaceDispatchStatus.CellChanged;
        Word* words = Words(s);
        for (uint i = 0; i < s->WordCount; i++)
        {
            Word w = words[i];
            if ((w.Bytes != 1 && w.Bytes != 2 && w.Bytes != 4 && w.Bytes != 8) || !Metadata(s, w.Address, w.Bytes))
                return CompilerInterfaceDispatchStatus.MetadataRejected;
            if (Load(w.Address, w.Bytes) != w.Value) return CompilerInterfaceDispatchStatus.MetadataChanged;
        }
        state = s;
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus Cells(State* s, ulong selected, out ulong requested, out ushort slot, out uint count,
        bool record = true)
    {
        requested = 0; slot = 0; count = 0;
        if (!Span(s->CellsStart, s->CellsBytes, 8) || (s->CellsBytes & 15) != 0 ||
            s->CellsBytes < 16 || s->CellsBytes > s->CellCapacity * 16UL ||
            !Range(s, s->CellsStart, s->CellsBytes, TypeManagerRegisteredRangeKind.ReadableSectionData))
            return CompilerInterfaceDispatchStatus.CellRejected;
        var cells = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, 210);
        var info = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, 209);
        var references = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, 331);
        if (!cells.Succeeded || cells.Section != s->CellsStart || cells.Length < 0 || (ulong)cells.Length != s->CellsBytes)
            return CompilerInterfaceDispatchStatus.CellRejected;
        if (!info.Succeeded || info.Length <= 0 || info.Length > 4096 || !references.Succeeded ||
            references.Length <= 0 || (references.Length & 3) != 0 ||
            !Metadata(s, info.Section, (ulong)info.Length) || !Metadata(s, references.Section, (ulong)references.Length))
            return CompilerInterfaceDispatchStatus.MetadataRejected;
        ulong cursor = 0;
        var status = CellUnsigned(s, info.Section, (ulong)info.Length, ref cursor, record, out uint header);
        count = (uint)(s->CellsBytes / 16);
        uint elements = header >> 2, widthTag = header & 3;
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if (elements == 0 || elements > count || widthTag > 2) return CompilerInterfaceDispatchStatus.MetadataRejected;
        ulong arrayBase = cursor;
        bool found = selected == 0;
        for (uint index = 0; index < count; index++)
        {
            ulong address = s->CellsStart + index * 16UL;
            if (*(ulong*)address != 0 || *(ulong*)(address + 8) != 0)
                return s->CellCount == 0 ? CompilerInterfaceDispatchStatus.CellRejected : CompilerInterfaceDispatchStatus.CellChanged;
            bool tupleFound = false;
            for (uint back = 0; back <= index && back < MaximumRunCells; back++)
            {
                status = CellTuple(s, info.Section, (ulong)info.Length, arrayBase, elements, widthTag,
                    index - back, record, out cursor, out tupleFound);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                if (tupleFound) break;
            }
            if (!tupleFound) return CompilerInterfaceDispatchStatus.MetadataRejected;
            status = CellUnsigned(s, info.Section, (ulong)info.Length, ref cursor, record, out uint referenceIndex);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            status = CellUnsigned(s, info.Section, (ulong)info.Length, ref cursor, record, out uint interfaceSlot);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if (interfaceSlot >= MaximumSlots) return CompilerInterfaceDispatchStatus.SlotRejected;
            ulong referenceOffset = referenceIndex * 4UL;
            status = CellWord(s, references.Section, (ulong)references.Length, referenceOffset, 4, record, out ulong relative);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if (!Relative(references.Section + referenceOffset, (uint)relative, out ulong target) ||
                (target & 7) != 0 || !Metadata(s, target, 24)) return CompilerInterfaceDispatchStatus.MetadataRejected;
            if (address == selected) { requested = target; slot = (ushort)interfaceSlot; found = true; }
        }
        return found ? CompilerInterfaceDispatchStatus.Success : CompilerInterfaceDispatchStatus.CellRejected;
    }

    // Bounded NativeFormat NativeArray decoding. A missing sparse entry falls
    // back to its preceding same-target run; malformed storage never does.
    private static CompilerInterfaceDispatchStatus CellTuple(State* s, ulong start, ulong bytes, ulong arrayBase,
        uint elements, uint widthTag, uint index, bool record, out ulong tuple, out bool found)
    {
        tuple = 0; found = false;
        if (index >= elements) return CompilerInterfaceDispatchStatus.Success;
        ulong width = 1UL << (int)widthTag;
        var status = CellWord(s, start, bytes, arrayBase + (index / 16) * width, width, record, out ulong relative);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        ulong cursor = arrayBase + relative;
        for (uint bit = 8; bit != 0; bit >>= 1)
        {
            ulong next = cursor;
            status = CellUnsigned(s, start, bytes, ref next, record, out uint value);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if ((index & bit) != 0)
            {
                if ((value & 2) != 0) { cursor += value >> 2; continue; }
            }
            else if ((value & 1) != 0) { cursor = next; continue; }
            if ((value & 3) == 0 && (value >> 2) == (index & 15)) { cursor = next; break; }
            return CompilerInterfaceDispatchStatus.Success;
        }
        tuple = cursor; found = true;
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus CellUnsigned(State* s, ulong start, ulong bytes,
        ref ulong cursor, bool record, out uint value)
    {
        value = 0;
        var status = CellWord(s, start, bytes, cursor, 1, record, out ulong first);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        uint count = 1;
        while (count <= 5 && (first & (1UL << (int)(count - 1))) != 0) count++;
        if (count > 5) return CompilerInterfaceDispatchStatus.MetadataRejected;
        value = count == 5 ? 0 : (uint)first >> (int)count;
        for (uint i = 1; i < count; i++)
        {
            status = CellWord(s, start, bytes, cursor + i, 1, record, out ulong part);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            value |= (uint)part << (count == 5 ? (int)((i - 1) * 8) : (int)(8 - count + (i - 1) * 8));
        }
        cursor += count;
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus CellWord(State* s, ulong start, ulong bytes,
        ulong offset, ulong width, bool record, out ulong value)
    {
        value = 0;
        if (offset > bytes || width > bytes - offset || !Metadata(s, start + offset, width))
            return CompilerInterfaceDispatchStatus.MetadataRejected;
        if (record) return Read(s, start + offset, width, out value);
        value = Load(start + offset, width);
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus Find(State* s, ulong receiver, ulong allocationBytes, ulong requested, ushort slot,
        out CompilerInterfaceDispatchResolution resolution)
    {
        resolution = default;
        CompilerInterfaceDispatchStatus status = DecodeType(s, receiver, out Type receiverType);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if ((receiverType.Flags & ElementMask) != Class || (receiverType.Flags & Generic) != 0)
            return CompilerInterfaceDispatchStatus.UnsupportedType;
        if (receiverType.BaseSize != allocationBytes) return CompilerInterfaceDispatchStatus.ReceiverRejected;
        status = DecodeType(s, requested, out Type requestedType);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if ((requestedType.Flags & ElementMask) != Interface || (requestedType.Flags & Definition) != 0)
            return CompilerInterfaceDispatchStatus.UnsupportedType;
        ulong current = receiver;
        for (uint depth = 0; current != 0 && depth < MaximumHierarchyDepth; depth++)
        {
            status = DecodeType(s, current, out Type owner);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if ((owner.Flags & ElementMask) != Class || (owner.Flags & Generic) != 0)
                return CompilerInterfaceDispatchStatus.UnsupportedType;
            if (owner.Map != 0)
            {
                status = Map(s, owner, requestedType, slot, out ulong implemented, out ushort implementation, out bool matched);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                if (matched)
                {
                    ulong target;
                    if (implementation < owner.Slots)
                    {
                        if (implementation >= receiverType.Slots) return CompilerInterfaceDispatchStatus.SlotRejected;
                        status = Read(s, receiverType.Vtable + implementation * 8UL, 8, out target);
                    }
                    else
                    {
                        uint sealedIndex = (uint)implementation - owner.Slots;
                        if (!SealedSlot(s, owner, sealedIndex)) return CompilerInterfaceDispatchStatus.SlotRejected;
                        status = Rel(s, owner.SealedTable + sealedIndex * 4UL, out target);
                    }
                    if (status != CompilerInterfaceDispatchStatus.Success) return status;
                    if (!Executable(s, target)) return CompilerInterfaceDispatchStatus.TargetRejected;
                    resolution.Target = target; resolution.ReceiverType = receiver; resolution.RequestedInterface = requested;
                    resolution.ImplementedInterface = implemented; resolution.DeclaringType = current;
                    resolution.InterfaceSlot = slot; resolution.ImplementationSlot = implementation;
                    return CompilerInterfaceDispatchStatus.Success;
                }
            }
            current = owner.Parent;
        }
        return current != 0 ? CompilerInterfaceDispatchStatus.MetadataRejected : CompilerInterfaceDispatchStatus.IncompatibleInterface;
    }

    private static CompilerInterfaceDispatchStatus Map(State* s, Type owner, Type requested, ushort slot,
        out ulong implemented, out ushort implementation, out bool matched)
    {
        implemented = 0; implementation = 0; matched = false;
        CompilerInterfaceDispatchStatus status = Read(s, owner.Map, 8, out ulong counts);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        uint rows = (ushort)counts;
        if ((counts >> 16) != 0 || rows > MaximumMapRows) return CompilerInterfaceDispatchStatus.MapRejected;
        if (!Metadata(s, owner.Map, 8 + rows * 6UL)) return CompilerInterfaceDispatchStatus.MapRejected;
        for (uint pass = 0; pass < 2; pass++)
        {
            for (uint i = 0; i < rows; i++)
            {
                ulong row = owner.Map + 8 + i * 6UL;
                status = Read(s, row, 4, out ulong pair);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                status = Read(s, row + 4, 2, out ulong impl);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                uint index = (ushort)pair;
                if (index >= owner.InterfaceCount || impl >= 0xFFFE) return CompilerInterfaceDispatchStatus.MapRejected;
                status = Read(s, owner.Interfaces + index * 8UL, 8, out ulong candidateAddress);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                status = DecodeType(s, candidateAddress, out Type candidate);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                if ((candidate.Flags & ElementMask) != Interface || (candidate.Flags & Definition) != 0)
                    return CompilerInterfaceDispatchStatus.MapRejected;
                if ((ushort)(pair >> 16) != slot) continue;
                bool compatible = candidateAddress == requested.Address;
                if (pass == 1 && !compatible)
                {
                    status = Compatible(s, candidate, requested, out compatible);
                    if (status != CompilerInterfaceDispatchStatus.Success) return status;
                }
                if (!compatible) continue;
                if (matched) return CompilerInterfaceDispatchStatus.AmbiguousImplementation;
                implemented = candidateAddress; implementation = (ushort)impl; matched = true;
            }
            if (matched) break; // exact match at this owner precedes its variance pass and all base owners.
        }
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus Compatible(State* s, Type implemented, Type requested, out bool compatible)
    {
        compatible = false;
        if ((implemented.Flags & (Generic | Variance)) != (Generic | Variance) ||
            (requested.Flags & (Generic | Variance)) != (Generic | Variance) ||
            implemented.GenericDefinition != requested.GenericDefinition)
            return CompilerInterfaceDispatchStatus.Success;
        CompilerInterfaceDispatchStatus status = DecodeType(s, requested.GenericDefinition, out Type definition);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        uint arity = definition.BaseSize;
        if ((definition.Flags & (ElementMask | Definition | Variance)) != (Interface | Definition | Variance) ||
            arity == 0 || arity > MaximumGenericArguments || definition.Composition == 0)
            return CompilerInterfaceDispatchStatus.UnsupportedType;
        for (uint i = 0; i < arity; i++)
        {
            status = Read(s, definition.Composition + i, 1, out ulong variance);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if (variance > 2) return CompilerInterfaceDispatchStatus.UnsupportedType;
            status = Argument(s, implemented, arity, i, out ulong from);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            status = Argument(s, requested, arity, i, out ulong to);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if (variance == 0) { if (from != to) return CompilerInterfaceDispatchStatus.Success; }
            else
            {
                status = Assignable(s, variance == 1 ? from : to, variance == 1 ? to : from, out bool allowed);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                if (!allowed) return CompilerInterfaceDispatchStatus.Success;
            }
        }
        compatible = true;
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus Argument(State* s, Type type, uint arity, uint index, out ulong argument) =>
        Rel(s, arity == 1 ? type.Composition : type.Composition + index * 4UL, out argument);

    // Ordinary class reference conversions. Arrays, boxing and generic class
    // variance are not inferred from hashes, element sizes or identical layouts.
    private static CompilerInterfaceDispatchStatus Assignable(State* s, ulong from, ulong to, out bool allowed)
    {
        allowed = false;
        CompilerInterfaceDispatchStatus status = DecodeType(s, to, out Type target);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if ((target.Flags & ElementMask) != Class || (target.Flags & Generic) != 0)
            return CompilerInterfaceDispatchStatus.UnsupportedType;
        for (uint depth = 0; from != 0 && depth < MaximumHierarchyDepth; depth++)
        {
            status = DecodeType(s, from, out Type source);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if ((source.Flags & ElementMask) != Class || (source.Flags & Generic) != 0)
                return CompilerInterfaceDispatchStatus.UnsupportedType;
            if (from == to) { allowed = true; return CompilerInterfaceDispatchStatus.Success; }
            from = source.Parent;
        }
        return from == 0 ? CompilerInterfaceDispatchStatus.Success : CompilerInterfaceDispatchStatus.MetadataRejected;
    }

    private static CompilerInterfaceDispatchStatus DecodeType(State* s, ulong address, out Type type)
    {
        type = default;
        if ((address & 7) != 0 || !Metadata(s, address, 24)) return CompilerInterfaceDispatchStatus.MetadataRejected;
        CompilerInterfaceDispatchStatus status = Read(s, address, 8, out ulong header);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        uint flags = (uint)header, kind = flags & Definition, element = flags & ElementMask;
        bool definition = kind == Definition;
        uint permitted = ElementMask | Dispatch | Sealed | Variance | Generic | 0x01000000;
        if (definition) permitted |= Definition;
        if ((flags & ~permitted) != 0 || (element != Class && element != Interface) ||
            (kind != 0 && !definition) || (element == Class && (flags & Generic) != 0) ||
            (definition && (element != Interface || (flags & Generic) != 0)))
            return CompilerInterfaceDispatchStatus.UnsupportedType;
        status = Read(s, address + 8, 8, out ulong parent);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        status = Read(s, address + 16, 8, out ulong counts);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        ushort slots = (ushort)counts, interfaces = (ushort)(counts >> 16);
        if (slots > MaximumSlots || interfaces > MaximumInterfaces || (definition && (parent != 0 || slots != 0 || interfaces != 0)))
            return CompilerInterfaceDispatchStatus.MetadataRejected;
        ulong tail = address + 24 + (slots + (ulong)interfaces) * 8;
        ulong bytes = 32 + (slots + (ulong)interfaces) * 8 + ((flags & Dispatch) != 0 ? 4UL : 0) +
            ((flags & Sealed) != 0 ? 4UL : 0) + ((flags & Generic) != 0 ? 8UL : definition && (flags & Variance) != 0 ? 4UL : 0);
        if (!Metadata(s, address, bytes)) return CompilerInterfaceDispatchStatus.MetadataRejected;
        // Capture even unused vtable/interface words: a later cache hit must not
        // silently acquire a changed hierarchy or interface ordering.
        for (uint i = 0; i < slots + (uint)interfaces; i++)
        {
            status = Read(s, address + 24 + i * 8UL, 8, out _);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
        }
        for (ulong at = tail; at < address + bytes; at += 4)
        {
            status = Read(s, at, 4, out _);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
        }
        status = Rel(s, tail, out ulong moduleSlot);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        if (moduleSlot != s->ModuleSlot) return CompilerInterfaceDispatchStatus.ModuleRejected;
        status = Read(s, tail + 4, 4, out _); // writable-data relocation is observed, never dereferenced.
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        type.Address = address; type.Flags = flags; type.BaseSize = (uint)(header >> 32);
        type.Parent = parent; type.Slots = slots; type.InterfaceCount = interfaces;
        type.Vtable = address + 24; type.Interfaces = address + 24 + slots * 8UL;
        tail += 8;
        if ((flags & Dispatch) != 0)
        {
            status = Rel(s, tail, out type.Map); tail += 4;
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
        }
        if ((flags & Sealed) != 0)
        {
            status = Rel(s, tail, out type.SealedTable); tail += 4;
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
        }
        if ((flags & Generic) != 0)
        {
            status = Rel(s, tail, out type.GenericDefinition); tail += 4;
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            // A definition is not another constructed type: validating its kind
            // before decoding bounds this recursion to one definition level.
            status = Read(s, type.GenericDefinition, 8, out ulong genericHeader);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            uint arity = (uint)(genericHeader >> 32);
            if (arity == 0 || arity > MaximumGenericArguments ||
                ((uint)genericHeader & (ElementMask | Definition | Generic | Variance)) !=
                    (Interface | Definition | (flags & Variance)))
                return CompilerInterfaceDispatchStatus.UnsupportedType;
            status = DecodeType(s, type.GenericDefinition, out _);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
            if (arity == 1) type.Composition = tail;
            else { status = Rel(s, tail, out type.Composition); if (status != CompilerInterfaceDispatchStatus.Success) return status; }
            for (uint i = 0; i < arity; i++)
            {
                status = Argument(s, type, arity, i, out ulong argument);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                status = Read(s, argument, 4, out ulong argumentFlags);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
                if (((uint)argumentFlags & (ElementMask | Definition | Generic)) != Class)
                    return CompilerInterfaceDispatchStatus.UnsupportedType;
                status = DecodeType(s, argument, out _);
                if (status != CompilerInterfaceDispatchStatus.Success) return status;
            }
        }
        else if (definition && (flags & Variance) != 0)
        {
            status = Rel(s, tail, out type.Composition);
            if (status != CompilerInterfaceDispatchStatus.Success) return status;
        }
        return CompilerInterfaceDispatchStatus.Success;
    }

    private static CompilerInterfaceDispatchStatus Read(State* s, ulong address, ulong bytes, out ulong value)
    {
        value = 0;
        if (!Metadata(s, address, bytes)) return CompilerInterfaceDispatchStatus.MetadataRejected;
        value = Load(address, bytes);
        Word* words = Words(s);
        for (uint i = 0; i < s->WordCount; i++)
            if (words[i].Address == address && words[i].Bytes == bytes)
                return words[i].Value == value ? CompilerInterfaceDispatchStatus.Success : CompilerInterfaceDispatchStatus.MetadataChanged;
        if (s->WordCount == MaximumMetadataWords) return CompilerInterfaceDispatchStatus.CapacityExceeded;
        words[s->WordCount].Address = address; words[s->WordCount].Value = value; words[s->WordCount++].Bytes = bytes;
        return CompilerInterfaceDispatchStatus.Success;
    }
    private static CompilerInterfaceDispatchStatus Rel(State* s, ulong address, out ulong target)
    {
        target = 0;
        CompilerInterfaceDispatchStatus status = Read(s, address, 4, out ulong value);
        if (status != CompilerInterfaceDispatchStatus.Success) return status;
        return value != 0 && Relative(address, (uint)value, out target)
            ? CompilerInterfaceDispatchStatus.Success : CompilerInterfaceDispatchStatus.MetadataRejected;
    }
    private static bool Relative(ulong address, uint value, out ulong target)
    {
        long offset = (int)value;
        target = offset >= 0 ? address <= ulong.MaxValue - (ulong)offset ? address + (ulong)offset : 0
            : address >= (ulong)-offset ? address - (ulong)-offset : 0;
        return target != 0;
    }
    private static ulong Load(ulong address, ulong bytes) => bytes == 8 ? *(ulong*)address :
        bytes == 4 ? *(uint*)address : bytes == 2 ? *(ushort*)address : *(byte*)address;
    private static bool Metadata(State* s, ulong address, ulong bytes) =>
        Range(s, address, bytes, TypeManagerRegisteredRangeKind.ImmutableMetadata);
    private static bool Executable(State* s, ulong address) =>
        Range(s, address, 1, TypeManagerRegisteredRangeKind.ExecutableEntry);
    private static bool Range(State* s, ulong address, ulong bytes, TypeManagerRegisteredRangeKind kind) =>
        TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, address, bytes, kind) == TypeManagerModuleStatus.Success;
    private static bool StorageRange(State* s)
    {
        RhpNewFastHeapAccess h = s->Heap;
        ulong* starts = stackalloc ulong[8] { s->ModuleOwner, (ulong)h.Allocator, (ulong)h.Nodes,
            (ulong)h.Reservations, (ulong)h.Heap, (ulong)h.Backing, (ulong)s->Allocator, s->CellsStart };
        ulong* lengths = stackalloc ulong[8] { s->ModuleCapacity, 56, h.NodeCapacity * 48,
            h.ReservationCapacity * 40, h.HeapCapacity, h.BackingCapacity, s->AllocatorCapacity, s->CellsBytes };
        for (int i = 0; i < 8; i++)
            if (Overlap(s->Self, s->Capacity, starts[i], lengths[i])) return false;
        if (s->Mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success) return false;
        if (Overlap((ulong)h.Backing, h.BackingCapacity, image, s->Capture.Range.ImageSize) ||
            Overlap((ulong)h.Backing, h.BackingCapacity, s->ModuleOwner, s->ModuleCapacity) ||
            Overlap((ulong)s->Allocator, s->AllocatorCapacity, s->ModuleOwner, s->ModuleCapacity) ||
            (Overlap((ulong)s->Allocator, s->AllocatorCapacity, image, s->Capture.Range.ImageSize) &&
                !Range(s, (ulong)s->Allocator, s->AllocatorCapacity, TypeManagerRegisteredRangeKind.MutableData))) return false;
        for (int i = 0; i < 7; i++)
            if (Overlap(s->CellsStart, s->CellsBytes, starts[i], lengths[i])) return false;
        return !Overlap(s->Self, s->Capacity, image, s->Capture.Range.ImageSize) ||
            Range(s, s->Self, s->Capacity, TypeManagerRegisteredRangeKind.MutableData);
    }
    private static bool SealedTableRanges(State* s, CompilerInterfaceDispatchSealedTable* tables)
    {
        for (uint i = 0; i < s->SealedTableCount; i++)
        {
            CompilerInterfaceDispatchSealedTable table = tables[i];
            if (!Span(table.DeclaringType, 24, 8) || !Metadata(s, table.DeclaringType, 24) ||
                !Span(table.Table, table.Bytes, 4) || (table.Bytes & 3) != 0 ||
                table.Bytes > MaximumSlots * 4UL || !Metadata(s, table.Table, table.Bytes)) return false;
            uint flags = *(uint*)table.DeclaringType;
            uint slots = *(ushort*)(table.DeclaringType + 16), interfaces = *(ushort*)(table.DeclaringType + 18);
            if ((flags & (ElementMask | Sealed)) != (Class | Sealed) ||
                (flags & ~(ElementMask | Sealed | Dispatch | 0x01000000U)) != 0 ||
                slots > MaximumSlots || interfaces > MaximumInterfaces) return false;
            ulong offset = 32 + (slots + (ulong)interfaces) * 8 + ((flags & Dispatch) != 0 ? 4UL : 0);
            if (!Metadata(s, table.DeclaringType, offset + 4) ||
                !Relative(table.DeclaringType + offset, *(uint*)(table.DeclaringType + offset), out ulong actual) ||
                actual != table.Table) return false;
            for (uint j = 0; j < i; j++)
                if (tables[j].DeclaringType == table.DeclaringType ||
                    (Overlap(tables[j].Table, tables[j].Bytes, table.Table, table.Bytes) &&
                        (tables[j].Table != table.Table || tables[j].Bytes != table.Bytes))) return false;
        }
        return true;
    }
    private static bool SealedSlot(State* s, Type owner, uint index)
    {
        CompilerInterfaceDispatchSealedTable* tables = SealedTables(s);
        for (uint i = 0; i < s->SealedTableCount; i++)
            if (tables[i].DeclaringType == owner.Address)
                return tables[i].Table == owner.SealedTable && index < tables[i].Bytes / 4;
        return false;
    }
    private static byte* CellSnapshot(State* s) => (byte*)s + sizeof(State);
    private static Cache* Entries(State* s) => (Cache*)(CellSnapshot(s) + s->CellCapacity * 32UL);
    private static Word* Words(State* s) => (Word*)(Entries(s) + s->CacheCapacity);
    private static CompilerInterfaceDispatchSealedTable* SealedTables(State* s) =>
        (CompilerInterfaceDispatchSealedTable*)(Words(s) + MaximumMetadataWords);
    private static bool Span(ulong address, ulong bytes, ulong alignment) =>
        address != 0 && (address & (alignment - 1)) == 0 && bytes != 0 && address <= ulong.MaxValue - bytes;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) =>
        an != 0 && bn != 0 && (a <= b ? b - a < an : a - b < bn);
    private static bool Equal(CompilerInterfaceDispatchResolution a, CompilerInterfaceDispatchResolution b) =>
        a.Target == b.Target && a.ReceiverType == b.ReceiverType && a.RequestedInterface == b.RequestedInterface &&
        a.ImplementedInterface == b.ImplementedInterface && a.DeclaringType == b.DeclaringType &&
        a.AllocationSequence == b.AllocationSequence && a.InterfaceSlot == b.InterfaceSlot && a.ImplementationSlot == b.ImplementationSlot;
}
