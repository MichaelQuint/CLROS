using OS.Kernel.Platform;

namespace OS.Drivers;

public enum DmaBufferStatus : byte
{
    Uninitialized, Success, InvalidStorage, InvalidWindow, InvalidEpoch, AlreadyInitialized,
    MetadataNotEmpty, InvalidPool, InvalidPageCount, Exhausted, GenerationExhausted,
    InvalidToken, StaleToken, InvalidOwnership
}

public enum DmaBufferOwnership : byte { Free, Cpu, Device }

public struct DmaBufferSlot
{
    internal ulong Generation;
    internal uint FirstPage, Pages;
    internal DmaBufferOwnership Owner;
}

public unsafe struct DmaBufferPool
{
    internal ulong Self, Epoch, Used;
    internal DmaAddressWindow Window;
    internal DmaBufferSlot* Slots;
    internal uint Count;
    public readonly bool IsInitialized => Self != 0;
    public readonly ulong AllocatedMask => Used;
    public readonly uint PageCount => Count;
}

/// <summary>Opaque identity for one allocation; copying a token does not transfer ownership.</summary>
public readonly struct DmaBufferToken
{
    internal readonly ulong Pool, Epoch, Generation;
    internal readonly uint Slot;
    public bool IsInitialized => Pool != 0 && Epoch != 0 && Generation != 0;
    internal DmaBufferToken(ulong pool, ulong epoch, ulong generation, uint slot)
    { Pool = pool; Epoch = epoch; Generation = generation; Slot = slot; }
}

public readonly struct DmaBufferAllocation
{
    public readonly DmaAddressRange Range;
    public readonly DmaBufferToken Token;
    public bool IsInitialized => Range.IsInitialized && Token.IsInitialized;
    internal DmaBufferAllocation(DmaAddressRange range, DmaBufferToken token) { Range = range; Token = token; }
}

/// <summary>
/// Metadata-only pool of at most 64 pages. The adapter supplies a unique nonzero lifetime
/// epoch, stable exclusive pool/slot storage, and mapped RW/NX WB backing. Do not copy,
/// clear, mutate externally or concurrently use a live pool or its slots. Ownership
/// transitions record adapter decisions; they do not synchronize hardware or prove DMA
/// completion. This code never reads, clears or writes the backing buffer.
/// </summary>
public static unsafe class PageAlignedDmaBuffers
{
    public static DmaBufferStatus TryInitialize(DmaBufferPool* pool, DmaAddressWindow window,
        DmaBufferSlot* slots, uint slotCount, ulong epoch)
    {
        if (!Storage(pool, (ulong)sizeof(DmaBufferPool))) return DmaBufferStatus.InvalidStorage;
        if (pool->Self != 0 || pool->Epoch != 0 || pool->Used != 0 || pool->Slots != null ||
            pool->Count != 0 || pool->Window.IsInitialized) return DmaBufferStatus.AlreadyInitialized;
        if (!window.IsInitialized || window.ByteLength > 64 * 4096 || slotCount == 0 ||
            slotCount != window.ByteLength / 4096) return DmaBufferStatus.InvalidWindow;
        if (epoch == 0) return DmaBufferStatus.InvalidEpoch;
        ulong metadataBytes = (ulong)slotCount * (ulong)sizeof(DmaBufferSlot);
        if (!Storage(slots, metadataBytes) ||
            Overlaps((ulong)pool, (ulong)sizeof(DmaBufferPool), (ulong)slots, metadataBytes) ||
            Overlaps((ulong)pool, (ulong)sizeof(DmaBufferPool), window.CpuVirtualBase, window.ByteLength) ||
            Overlaps((ulong)slots, metadataBytes, window.CpuVirtualBase, window.ByteLength))
            return DmaBufferStatus.InvalidStorage;
        for (uint i = 0; i < slotCount; i++)
            if (slots[i].Generation != 0 || slots[i].FirstPage != 0 || slots[i].Pages != 0 || slots[i].Owner != DmaBufferOwnership.Free)
                return DmaBufferStatus.MetadataNotEmpty;
        pool->Window = window; pool->Slots = slots; pool->Count = slotCount; pool->Epoch = epoch;
        pool->Self = (ulong)pool;
        return DmaBufferStatus.Success;
    }

    public static DmaBufferStatus TryAllocate(DmaBufferPool* pool, uint pages, out DmaBufferAllocation allocation)
    {
        allocation = default;
        if (!Valid(pool)) return DmaBufferStatus.InvalidPool;
        if (pages == 0 || pages > pool->Count) return DmaBufferStatus.InvalidPageCount;
        uint first = 0;
        for (; first <= pool->Count - pages; first++)
            if ((pool->Used & Mask(first, pages)) == 0) break;
        if (first > pool->Count - pages) return DmaBufferStatus.Exhausted;
        uint slot = 0;
        for (; slot < pool->Count; slot++)
            if (pool->Slots[slot].Owner == DmaBufferOwnership.Free && pool->Slots[slot].Generation != ~0UL) break;
        if (slot == pool->Count) return DmaBufferStatus.GenerationExhausted;
        if (pool->Window.TrySlice((ulong)first * 4096, (ulong)pages * 4096, out var range) != DmaAddressStatus.Success)
            return DmaBufferStatus.InvalidPool;
        DmaBufferSlot* entry = pool->Slots + slot;
        entry->Generation++; entry->FirstPage = first; entry->Pages = pages; entry->Owner = DmaBufferOwnership.Cpu;
        pool->Used |= Mask(first, pages);
        allocation = new(range, new(pool->Self, pool->Epoch, entry->Generation, slot));
        return DmaBufferStatus.Success;
    }

    public static DmaBufferStatus TryGiveToDevice(DmaBufferPool* pool, DmaBufferToken token) =>
        Transition(pool, token, DmaBufferOwnership.Cpu, DmaBufferOwnership.Device);

    public static DmaBufferStatus TryReturnToCpu(DmaBufferPool* pool, DmaBufferToken token) =>
        Transition(pool, token, DmaBufferOwnership.Device, DmaBufferOwnership.Cpu);

    public static DmaBufferStatus TryRelease(DmaBufferPool* pool, DmaBufferToken token)
    {
        DmaBufferStatus status = Find(pool, token, out DmaBufferSlot* slot);
        if (status != DmaBufferStatus.Success) return status;
        if (slot->Owner != DmaBufferOwnership.Cpu) return DmaBufferStatus.InvalidOwnership;
        pool->Used &= ~Mask(slot->FirstPage, slot->Pages);
        slot->FirstPage = 0; slot->Pages = 0; slot->Owner = DmaBufferOwnership.Free;
        return DmaBufferStatus.Success; // Generation survives release, including the terminal value.
    }

    private static DmaBufferStatus Transition(DmaBufferPool* pool, DmaBufferToken token,
        DmaBufferOwnership expected, DmaBufferOwnership next)
    {
        DmaBufferStatus status = Find(pool, token, out DmaBufferSlot* slot);
        if (status != DmaBufferStatus.Success) return status;
        if (slot->Owner != expected) return DmaBufferStatus.InvalidOwnership;
        slot->Owner = next;
        return DmaBufferStatus.Success;
    }

    private static DmaBufferStatus Find(DmaBufferPool* pool, DmaBufferToken token, out DmaBufferSlot* slot)
    {
        slot = null;
        if (!Valid(pool)) return DmaBufferStatus.InvalidPool;
        if (!token.IsInitialized || token.Pool != pool->Self || token.Epoch != pool->Epoch || token.Slot >= pool->Count)
            return DmaBufferStatus.InvalidToken;
        DmaBufferSlot* found = pool->Slots + token.Slot;
        if (found->Generation != token.Generation || found->Owner == DmaBufferOwnership.Free)
            return DmaBufferStatus.StaleToken;
        if (found->Pages == 0 || found->FirstPage >= pool->Count || found->Pages > pool->Count - found->FirstPage ||
            (pool->Used & Mask(found->FirstPage, found->Pages)) != Mask(found->FirstPage, found->Pages))
            return DmaBufferStatus.InvalidPool;
        slot = found;
        return DmaBufferStatus.Success;
    }

    private static bool Valid(DmaBufferPool* pool) => Storage(pool, (ulong)sizeof(DmaBufferPool)) &&
        pool->Self == (ulong)pool && pool->Epoch != 0 && pool->Count >= 1 && pool->Count <= 64 && pool->Slots != null;
    private static ulong Mask(uint first, uint count) => count == 64 ? ~0UL : ((1UL << (int)count) - 1) << (int)first;
    private static bool Storage(void* pointer, ulong bytes) => pointer != null && ((ulong)pointer & 7) == 0 &&
        bytes != 0 && bytes <= ~0UL - (ulong)pointer;
    private static bool Overlaps(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
}
