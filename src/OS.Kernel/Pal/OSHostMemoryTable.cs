using System.Runtime.InteropServices;

namespace OS.Pal;
public sealed partial class OSHostMemoryTable
{
    public const ulong DefaultBaseAddress = 0x400000;
    private readonly MemorySlot[] slots;
    private readonly ulong capacityBytes;
    public OSHostMemoryTable(ulong pageSizeBytes, ulong baseAddress, ulong pageCapacity, int handleCapacity)
    {
        PageSizeBytes = pageSizeBytes;
        BaseAddress = baseAddress;
        PageCapacity = pageCapacity;
        capacityBytes = TryMultiply(pageCapacity, pageSizeBytes, out ulong bytes) ? bytes : 0;
        slots = handleCapacity > 0 ? new MemorySlot[handleCapacity] : [];
    }

    public ulong PageSizeBytes { get; }
    public ulong BaseAddress { get; }
    public ulong PageCapacity { get; }
    public ulong ByteCapacity => capacityBytes;
    public int HandleCapacity => slots.Length;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public OSHostMemoryAllocationResult AllocPages(OSHostMemoryRequest request)
    {
        OSHostStatus tableStatus = ValidateTable();
        if (tableStatus != OSHostStatus.Ok)
        {
            return OSHostMemoryAllocationResult.FromStatus(tableStatus);
        }

        OSHostStatus requestStatus = ValidateRequest(request, out ulong byteLength);
        if (requestStatus != OSHostStatus.Ok)
        {
            return OSHostMemoryAllocationResult.FromStatus(requestStatus);
        }

        int slotIndex = FindFreeSlot();
        if (slotIndex < 0)
        {
            return OSHostMemoryAllocationResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        if (!TryFindFreeRange(byteLength, request.AlignmentBytes, out ulong baseAddress))
        {
            return OSHostMemoryAllocationResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        OSHostPageProtection requestedProtection = (OSHostPageProtection)request.ProtectionFlags;
        OSHostMemoryProtectionPlanResult protection = OSHostMemoryProtectionPlan.Create(baseAddress, byteLength, OSHostPageProtection.None, requestedProtection, PageSizeBytes);
        if (!protection.Succeeded)
        {
            return OSHostMemoryAllocationResult.FromStatus(protection.HostStatus);
        }

        uint generation = NextGeneration(slots[slotIndex].Generation);
        OSHostHandle handle = CreateHandle(slotIndex, generation);
        OSHostMemoryRange range = new(baseAddress, byteLength, request.ProtectionFlags);
        slots[slotIndex] = MemorySlot.Create(handle, generation, range, requestedProtection);
        return new OSHostMemoryAllocationResult(OSHostStatus.Ok, handle, range);
    }

    public OSHostStatus FreePages(OSHostHandle allocation)
    {
        OSHostStatus status = TryFindSlot(allocation, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        uint generation = slots[slotIndex].Generation;
        slots[slotIndex] = new MemorySlot
        {
            Generation = generation
        };
        return OSHostStatus.Ok;
    }

    public OSHostStatus ProtectPages(OSHostMemoryRange range)
    {
        OSHostStatus tableStatus = ValidateTable();
        if (tableStatus != OSHostStatus.Ok)
        {
            return tableStatus;
        }

        OSHostStatus status = TryFindSlotByExactRange(range.BaseAddress, range.ByteLength, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        MemorySlot slot = slots[slotIndex];
        OSHostPageProtection requestedProtection = (OSHostPageProtection)range.ProtectionFlags;
        OSHostMemoryProtectionPlanResult protection = OSHostMemoryProtectionPlan.Create(range.BaseAddress, range.ByteLength, slot.Protection, requestedProtection, PageSizeBytes);
        if (!protection.Succeeded)
        {
            return protection.HostStatus;
        }

        slots[slotIndex].Range = new OSHostMemoryRange(range.BaseAddress, range.ByteLength, range.ProtectionFlags);
        slots[slotIndex].Protection = requestedProtection;
        return OSHostStatus.Ok;
    }

    public OSHostStatus QueryMemory(ulong address, out OSHostMemoryQueryResult result)
    {
        result = default;
        OSHostStatus tableStatus = ValidateTable();
        if (tableStatus != OSHostStatus.Ok)
        {
            return tableStatus;
        }

        if (address == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        for (int index = 0; index < slots.Length; index++)
        {
            MemorySlot slot = slots[index];
            if (!slot.InUse)
            {
                continue;
            }

            if (Contains(slot.Range.BaseAddress, slot.Range.ByteLength, address))
            {
                result = new OSHostMemoryQueryResult(slot.Range, PageSizeBytes, OSHostMemoryStateFlags.Allocated);
                return OSHostStatus.Ok;
            }
        }

        return OSHostStatus.NotFound;
    }

    public OSHostStatus TryGetEntry(OSHostHandle allocation, out OSHostMemoryTableEntry entry)
    {
        OSHostStatus status = TryFindSlot(allocation, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            entry = default;
            return status;
        }

        MemorySlot slot = slots[slotIndex];
        entry = new OSHostMemoryTableEntry(slot.Allocation, slot.Range, OSHostMemoryStateFlags.Allocated);
        return OSHostStatus.Ok;
    }

    private OSHostStatus ValidateTable()
    {
        if (!IsValidPageSize(PageSizeBytes) || BaseAddress == 0 || !IsAligned(BaseAddress, PageSizeBytes) || PageCapacity == 0 || capacityBytes == 0 || !TryAdd(BaseAddress, capacityBytes, out _))
        {
            return OSHostStatus.InvalidArgument;
        }

        return OSHostStatus.Ok;
    }

    private OSHostStatus ValidateRequest(OSHostMemoryRequest request, out ulong byteLength)
    {
        byteLength = 0;
        if (request.PageCount == 0 || request.AlignmentBytes < PageSizeBytes || !IsPowerOfTwo(request.AlignmentBytes) || !IsAligned(request.AlignmentBytes, PageSizeBytes))
        {
            return OSHostStatus.InvalidArgument;
        }

        if (!TryMultiply(request.PageCount, PageSizeBytes, out byteLength) || byteLength == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostPageProtection requestedProtection = (OSHostPageProtection)request.ProtectionFlags;
        OSHostMemoryProtectionPlanStatus protectionStatus = OSHostMemoryProtectionPlan.ValidateProtectionTransition(OSHostPageProtection.None, requestedProtection);
        return OSHostMemoryProtectionPlan.ToHostStatus(protectionStatus);
    }

    private bool TryFindFreeRange(ulong byteLength, ulong alignmentBytes, out ulong baseAddress)
    {
        baseAddress = 0;
        if (!TryAdd(BaseAddress, capacityBytes, out ulong windowEnd) || !TryAlignUp(BaseAddress, alignmentBytes, out ulong candidate))
        {
            return false;
        }

        while (TryAdd(candidate, byteLength, out ulong candidateEnd) && candidateEnd <= windowEnd)
        {
            bool overlapped = false;
            ulong nextCandidate = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                MemorySlot slot = slots[index];
                if (!slot.InUse)
                {
                    continue;
                }

                ulong slotEnd = slot.EndAddress;
                if (!RangesOverlap(candidate, candidateEnd, slot.Range.BaseAddress, slotEnd))
                {
                    continue;
                }

                overlapped = true;
                if (!TryAlignUp(slotEnd, alignmentBytes, out nextCandidate) || nextCandidate <= candidate)
                {
                    return false;
                }

                break;
            }

            if (!overlapped)
            {
                baseAddress = candidate;
                return true;
            }

            candidate = nextCandidate;
        }

        return false;
    }

    private int FindFreeSlot()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            if (!slots[index].InUse)
            {
                return index;
            }
        }

        return -1;
    }

    private OSHostStatus TryFindSlot(OSHostHandle allocation, out int slotIndex)
    {
        if (!allocation.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        uint indexPart = (uint)(allocation.Value & 0xFFFF_FFFFUL);
        uint generation = (uint)(allocation.Value >> 32);
        if (indexPart == 0 || generation == 0)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        ulong zeroBasedIndex = (ulong)indexPart - 1;
        if (zeroBasedIndex >= (ulong)slots.Length)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        slotIndex = (int)zeroBasedIndex;
        return slots[slotIndex].InUse && slots[slotIndex].Generation == generation ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    private OSHostStatus TryFindSlotByExactRange(ulong baseAddress, ulong byteLength, out int slotIndex)
    {
        if (baseAddress == 0 || byteLength == 0)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        for (int index = 0; index < slots.Length; index++)
        {
            MemorySlot slot = slots[index];
            if (slot.InUse && slot.Range.BaseAddress == baseAddress && slot.Range.ByteLength == byteLength)
            {
                slotIndex = index;
                return OSHostStatus.Ok;
            }
        }

        slotIndex = -1;
        return OSHostStatus.InvalidArgument;
    }

    private static OSHostHandle CreateHandle(int slotIndex, uint generation)
    {
        return new OSHostHandle(((ulong)generation << 32) | ((uint)slotIndex + 1));
    }

    private static uint NextGeneration(uint previous)
    {
        uint next = previous + 1;
        return next == 0 ? 1 : next;
    }

    private static bool Contains(ulong baseAddress, ulong byteLength, ulong address)
    {
        return TryAdd(baseAddress, byteLength, out ulong endAddress) && address >= baseAddress && address < endAddress;
    }

    private static bool RangesOverlap(ulong firstBase, ulong firstEnd, ulong secondBase, ulong secondEnd)
    {
        return firstBase < secondEnd && secondBase < firstEnd;
    }

    private static bool TryAlignUp(ulong value, ulong alignment, out ulong aligned)
    {
        aligned = 0;
        if (!IsPowerOfTwo(alignment))
        {
            return false;
        }

        ulong mask = alignment - 1;
        if (value > ulong.MaxValue - mask)
        {
            return false;
        }

        aligned = (value + mask) & ~mask;
        return true;
    }

    private static bool IsValidPageSize(ulong value)
    {
        return value >= OSHostMemoryProtectionPlan.MinimumPageSize && IsPowerOfTwo(value);
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return alignment != 0 && (value & (alignment - 1)) == 0;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static bool TryMultiply(ulong left, ulong right, out ulong value)
    {
        if (left != 0 && right > ulong.MaxValue / left)
        {
            value = 0;
            return false;
        }

        value = left * right;
        return true;
    }
}
