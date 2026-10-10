namespace OS.Drivers;

public enum VirtqueueDescriptorStatus : byte
{
    Uninitialized, Success, InvalidStorage, InvalidSize, InvalidEpoch, AlreadyInitialized,
    MetadataNotEmpty, InvalidPool, Exhausted, GenerationExhausted, InvalidToken, StaleToken,
    InvalidOwnership
}

public enum VirtqueueDescriptorOwnership : byte { Free, Reserved, Device }

public struct VirtqueueDescriptorSlot
{
    internal ulong Pool, Generation;
    internal VirtqueueDescriptorOwnership Owner;
}

public unsafe struct VirtqueueDescriptorPoolState
{
    internal ulong Self, Epoch;
    internal VirtqueueDescriptorSlot* Slots;
    internal ushort Count, Allocated;
    public readonly bool IsInitialized => Self != 0;
    public readonly ushort Capacity => Count;
    public readonly ushort AllocatedCount => Allocated;
}

/// <summary>Opaque allocation identity. Copying a token does not transfer ownership.</summary>
public readonly struct VirtqueueDescriptorToken
{
    internal readonly ulong Pool, Epoch, Generation;
    public readonly ushort Index;
    public bool IsInitialized => Pool != 0 && Epoch != 0 && Generation != 0;
    internal VirtqueueDescriptorToken(ulong pool, ulong epoch, ulong generation, ushort index)
    { Pool = pool; Epoch = epoch; Generation = generation; Index = index; }
}

/// <summary>
/// Metadata only; publish/reclaim record caller decisions and never prove device completion.
/// The adapter supplies a unique nonzero lifetime epoch and stable exclusive pool/slot
/// storage. Do not copy, clear, externally mutate or concurrently use live storage.
/// No descriptor ring, device register or DMA buffer is accessed here.
/// </summary>
public static unsafe class VirtqueueDescriptors
{
    public static VirtqueueDescriptorStatus TryInitialize(VirtqueueDescriptorPoolState* pool,
        VirtqueueDescriptorSlot* slots, uint capacity, ulong epoch)
    {
        if (!Storage(pool, (ulong)sizeof(VirtqueueDescriptorPoolState))) return VirtqueueDescriptorStatus.InvalidStorage;
        if (pool->Self != 0 || pool->Epoch != 0 || pool->Slots != null || pool->Count != 0 || pool->Allocated != 0)
            return VirtqueueDescriptorStatus.AlreadyInitialized;
        if (BoundedVirtqueueSize.Validate(capacity, BoundedVirtqueueSize.Maximum, out ushort size) != VirtqueueSizeStatus.Success)
            return VirtqueueDescriptorStatus.InvalidSize;
        if (epoch == 0) return VirtqueueDescriptorStatus.InvalidEpoch;
        ulong bytes = (ulong)size * (ulong)sizeof(VirtqueueDescriptorSlot);
        if (!Storage(slots, bytes) || Overlaps((ulong)pool, (ulong)sizeof(VirtqueueDescriptorPoolState), (ulong)slots, bytes))
            return VirtqueueDescriptorStatus.InvalidStorage;
        for (uint i = 0; i < size; i++)
            if (slots[i].Pool != 0 || slots[i].Generation != 0 || slots[i].Owner != VirtqueueDescriptorOwnership.Free)
                return VirtqueueDescriptorStatus.MetadataNotEmpty;
        for (uint i = 0; i < size; i++) slots[i].Pool = (ulong)pool;
        pool->Slots = slots; pool->Count = size; pool->Epoch = epoch; pool->Self = (ulong)pool;
        return VirtqueueDescriptorStatus.Success;
    }

    public static VirtqueueDescriptorStatus TryAllocate(VirtqueueDescriptorPoolState* pool, out VirtqueueDescriptorToken token)
    {
        token = default;
        if (!Valid(pool)) return VirtqueueDescriptorStatus.InvalidPool;
        for (ushort i = 0; i < pool->Count; i++)
        {
            VirtqueueDescriptorSlot* slot = pool->Slots + i;
            if (slot->Pool != pool->Self || slot->Owner > VirtqueueDescriptorOwnership.Device)
                return VirtqueueDescriptorStatus.InvalidPool;
            if (slot->Owner != VirtqueueDescriptorOwnership.Free) continue;
            // Preserve lowest-free ordering: a terminal generation refuses allocation, never wraps.
            if (slot->Generation == ~0UL) return VirtqueueDescriptorStatus.GenerationExhausted;
            if (pool->Allocated == pool->Count) return VirtqueueDescriptorStatus.InvalidPool;
            slot->Generation++; slot->Owner = VirtqueueDescriptorOwnership.Reserved; pool->Allocated++;
            token = new(pool->Self, pool->Epoch, slot->Generation, i);
            return VirtqueueDescriptorStatus.Success;
        }
        return VirtqueueDescriptorStatus.Exhausted;
    }

    public static VirtqueueDescriptorStatus TryPublish(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token) =>
        Transition(pool, token, VirtqueueDescriptorOwnership.Reserved, VirtqueueDescriptorOwnership.Device);

    public static VirtqueueDescriptorStatus TryReclaim(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token) =>
        Transition(pool, token, VirtqueueDescriptorOwnership.Device, VirtqueueDescriptorOwnership.Reserved);

    public static VirtqueueDescriptorStatus TryRelease(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token)
    {
        VirtqueueDescriptorStatus status = Find(pool, token, out VirtqueueDescriptorSlot* slot);
        if (status != VirtqueueDescriptorStatus.Success) return status;
        if (slot->Owner != VirtqueueDescriptorOwnership.Reserved) return VirtqueueDescriptorStatus.InvalidOwnership;
        slot->Owner = VirtqueueDescriptorOwnership.Free; pool->Allocated--;
        return VirtqueueDescriptorStatus.Success; // Generation and slot binding survive release.
    }

    private static VirtqueueDescriptorStatus Transition(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token,
        VirtqueueDescriptorOwnership expected, VirtqueueDescriptorOwnership next)
    {
        VirtqueueDescriptorStatus status = Find(pool, token, out VirtqueueDescriptorSlot* slot);
        if (status != VirtqueueDescriptorStatus.Success) return status;
        if (slot->Owner != expected) return VirtqueueDescriptorStatus.InvalidOwnership;
        slot->Owner = next;
        return VirtqueueDescriptorStatus.Success;
    }

    private static VirtqueueDescriptorStatus Find(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token,
        out VirtqueueDescriptorSlot* slot)
    {
        slot = null;
        if (!Valid(pool)) return VirtqueueDescriptorStatus.InvalidPool;
        if (!token.IsInitialized || token.Pool != pool->Self || token.Epoch != pool->Epoch || token.Index >= pool->Count)
            return VirtqueueDescriptorStatus.InvalidToken;
        VirtqueueDescriptorSlot* found = pool->Slots + token.Index;
        if (found->Pool != pool->Self || found->Owner > VirtqueueDescriptorOwnership.Device)
            return VirtqueueDescriptorStatus.InvalidPool;
        if (found->Generation != token.Generation || found->Owner == VirtqueueDescriptorOwnership.Free)
            return VirtqueueDescriptorStatus.StaleToken;
        if (pool->Allocated == 0) return VirtqueueDescriptorStatus.InvalidPool;
        slot = found;
        return VirtqueueDescriptorStatus.Success;
    }

    private static bool Valid(VirtqueueDescriptorPoolState* pool)
    {
        if (!Storage(pool, (ulong)sizeof(VirtqueueDescriptorPoolState)) || pool->Self != (ulong)pool || pool->Epoch == 0 ||
            BoundedVirtqueueSize.Validate(pool->Count, BoundedVirtqueueSize.Maximum, out _) != VirtqueueSizeStatus.Success ||
            pool->Allocated > pool->Count) return false;
        ulong bytes = (ulong)pool->Count * (ulong)sizeof(VirtqueueDescriptorSlot);
        return Storage(pool->Slots, bytes) &&
            !Overlaps((ulong)pool, (ulong)sizeof(VirtqueueDescriptorPoolState), (ulong)pool->Slots, bytes);
    }

    private static bool Storage(void* pointer, ulong bytes) => pointer != null && ((ulong)pointer & 7) == 0 &&
        bytes != 0 && bytes <= ~0UL - (ulong)pointer;
    private static bool Overlaps(ulong first, ulong bytes, ulong second, ulong otherBytes) =>
        first < second + otherBytes && second < first + bytes;
}
