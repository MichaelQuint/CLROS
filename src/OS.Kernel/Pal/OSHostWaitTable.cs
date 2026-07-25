namespace OS.Pal;

public enum OSHostWaitableKind : uint
{
    None = 0,
    ManualResetEvent = 1,
    AutoResetEvent = 2
}

public static class OSHostWaitConstants
{
    public const uint TimeoutKindImmediate = OSHostSocketConstants.TimeoutKindImmediate;

    public const uint WaitFlagWaitAll = 1;

    public const ulong CapabilityManualResetEvent = 1UL << 0;

    public const ulong CapabilityAutoResetEvent = 1UL << 1;

    public const ulong CapabilityImmediateWaitOne = 1UL << 2;

    public const ulong CapabilityImmediateWaitMany = 1UL << 3;

    public const ulong CapabilityMockEventLifecycle = 1UL << 4;

    public const ulong CapabilitySingleThreadEarlyMode = 1UL << 5;

    public const uint ResultKindNone = 0;

    public const uint ResultKindSignaled = 1;

    public const uint ResultKindWouldBlock = 2;

    public const uint ResultKindInterrupted = 3;
}

public readonly struct OSHostEventCreateResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostHandle Waitable;

    public OSHostEventCreateResult(OSHostStatus status, OSHostHandle waitable)
    {
        Status = status;
        Waitable = waitable;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Waitable.IsValid;

    public static OSHostEventCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostEventCreateResult(status, default);
    }
}

public readonly struct OSHostWaitCapabilityResult
{
    public readonly OSHostStatus Status;

    public readonly ulong CapabilityFlags;

    public readonly uint MaxWaitableCount;

    public readonly uint ActiveWaitableCount;

    public readonly bool ReadyForHostedRuntime;

    public readonly bool IsInitialized;

    public OSHostWaitCapabilityResult(
        OSHostStatus status,
        ulong capabilityFlags,
        uint maxWaitableCount,
        uint activeWaitableCount,
        bool readyForHostedRuntime,
        bool isInitialized = true)
    {
        Status = status;
        CapabilityFlags = capabilityFlags;
        MaxWaitableCount = maxWaitableCount;
        ActiveWaitableCount = activeWaitableCount;
        ReadyForHostedRuntime = readyForHostedRuntime;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && CapabilityFlags != 0;

    public bool HasCapability(ulong capability)
    {
        return capability != 0 && (CapabilityFlags & capability) == capability;
    }

    public static OSHostWaitCapabilityResult FromStatus(OSHostStatus status)
    {
        return new OSHostWaitCapabilityResult(
            status,
            capabilityFlags: 0,
            maxWaitableCount: 0,
            activeWaitableCount: 0,
            readyForHostedRuntime: false);
    }
}

public readonly struct OSHostWaitTableEntry
{
    public readonly OSHostHandle Waitable;

    public readonly OSHostWaitableKind Kind;

    public readonly bool Signaled;

    public readonly bool Canceled;

    public OSHostWaitTableEntry(
        OSHostHandle waitable,
        OSHostWaitableKind kind,
        bool signaled,
        bool canceled)
    {
        Waitable = waitable;
        Kind = kind;
        Signaled = signaled;
        Canceled = canceled;
    }
}

public sealed class OSHostWaitTable
{
    private readonly WaitSlot[] slots;

    public OSHostWaitTable(int capacity)
    {
        slots = capacity > 0
            ? new WaitSlot[capacity]
            : [];
    }

    public int Capacity => slots.Length;

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

    public OSHostWaitCapabilityResult GetCapabilities()
    {
        return new OSHostWaitCapabilityResult(
            OSHostStatus.Ok,
            OSHostWaitConstants.CapabilityManualResetEvent
                | OSHostWaitConstants.CapabilityAutoResetEvent
                | OSHostWaitConstants.CapabilityImmediateWaitOne
                | OSHostWaitConstants.CapabilityImmediateWaitMany
                | OSHostWaitConstants.CapabilityMockEventLifecycle
                | OSHostWaitConstants.CapabilitySingleThreadEarlyMode,
            (uint)Capacity,
            (uint)ActiveCount,
            readyForHostedRuntime: false);
    }

    public OSHostEventCreateResult CreateEvent(
        OSHostWaitableKind kind,
        bool initiallySignaled)
    {
        if (kind is not (OSHostWaitableKind.ManualResetEvent or OSHostWaitableKind.AutoResetEvent))
        {
            return OSHostEventCreateResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        int slotIndex = FindFreeSlot();
        if (slotIndex < 0)
        {
            return OSHostEventCreateResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        uint generation = NextGeneration(slots[slotIndex].Generation);
        OSHostHandle waitable = CreateHandle(slotIndex, generation);
        slots[slotIndex] = WaitSlot.Create(waitable, generation, kind, initiallySignaled);
        return new OSHostEventCreateResult(OSHostStatus.Ok, waitable);
    }

    public OSHostStatus Set(OSHostHandle waitable)
    {
        OSHostStatus status = TryFindSlot(waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        if (slots[slotIndex].Canceled)
        {
            return OSHostStatus.Interrupted;
        }

        slots[slotIndex].Signaled = true;
        return OSHostStatus.Ok;
    }

    public OSHostStatus Reset(OSHostHandle waitable)
    {
        OSHostStatus status = TryFindSlot(waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        if (slots[slotIndex].Canceled)
        {
            return OSHostStatus.Interrupted;
        }

        slots[slotIndex].Signaled = false;
        return OSHostStatus.Ok;
    }

    public OSHostStatus Cancel(OSHostHandle waitable)
    {
        OSHostStatus status = TryFindSlot(waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        slots[slotIndex].Canceled = true;
        slots[slotIndex].Signaled = false;
        return OSHostStatus.Ok;
    }

    public OSHostWaitResult WaitOne(OSHostHandle waitable)
    {
        return WaitOne(new OSHostWaitOneRequest(
            waitable,
            ImmediateTimeout(),
            flags: 0,
            reserved: 0));
    }

    public OSHostWaitResult WaitOne(OSHostWaitOneRequest request)
    {
        OSHostStatus requestStatus = ValidateTimeoutAndFlags(request.Timeout, request.Flags, request.Reserved);
        if (requestStatus != OSHostStatus.Ok)
        {
            return Result(requestStatus);
        }

        OSHostStatus status = TryFindSlot(request.Waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return Result(status);
        }

        return WaitSlotAt(slotIndex, signaledIndex: 0);
    }

    public OSHostWaitResult WaitMany(
        ReadOnlySpan<OSHostHandle> waitables,
        OSHostTimeout timeout,
        uint flags = 0)
    {
        OSHostStatus requestStatus = ValidateTimeoutAndFlags(timeout, flags, reserved: 0);
        if (requestStatus != OSHostStatus.Ok)
        {
            return Result(requestStatus);
        }

        if (waitables.Length == 0)
        {
            return Result(OSHostStatus.InvalidArgument);
        }

        for (int index = 0; index < waitables.Length; index++)
        {
            OSHostStatus status = TryFindSlot(waitables[index], out int slotIndex);
            if (status != OSHostStatus.Ok)
            {
                return Result(status);
            }

            if (slots[slotIndex].Canceled)
            {
                return Result(OSHostStatus.Interrupted, OSHostWaitConstants.ResultKindInterrupted);
            }

            if (slots[slotIndex].Signaled)
            {
                return WaitSlotAt(slotIndex, (uint)index);
            }
        }

        return Result(OSHostStatus.WouldBlock, OSHostWaitConstants.ResultKindWouldBlock);
    }

    public OSHostStatus Close(OSHostHandle waitable)
    {
        OSHostStatus status = TryFindSlot(waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        uint generation = slots[slotIndex].Generation;
        slots[slotIndex] = new WaitSlot
        {
            Generation = generation
        };
        return OSHostStatus.Ok;
    }

    public OSHostStatus TryGetEntry(OSHostHandle waitable, out OSHostWaitTableEntry entry)
    {
        OSHostStatus status = TryFindSlot(waitable, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            entry = default;
            return status;
        }

        WaitSlot slot = slots[slotIndex];
        entry = new OSHostWaitTableEntry(
            slot.Waitable,
            slot.Kind,
            slot.Signaled,
            slot.Canceled);
        return OSHostStatus.Ok;
    }

    public static OSHostTimeout ImmediateTimeout()
    {
        return new OSHostTimeout(
            OSHostWaitConstants.TimeoutKindImmediate,
            flags: 0,
            ticks: 0,
            tickFrequency: 0);
    }

    private OSHostWaitResult WaitSlotAt(int slotIndex, uint signaledIndex)
    {
        if (slots[slotIndex].Canceled)
        {
            return Result(OSHostStatus.Interrupted, OSHostWaitConstants.ResultKindInterrupted);
        }

        if (!slots[slotIndex].Signaled)
        {
            return Result(OSHostStatus.WouldBlock, OSHostWaitConstants.ResultKindWouldBlock);
        }

        OSHostHandle waitable = slots[slotIndex].Waitable;
        if (slots[slotIndex].Kind == OSHostWaitableKind.AutoResetEvent)
        {
            slots[slotIndex].Signaled = false;
        }

        return new OSHostWaitResult(
            OSHostWaitConstants.ResultKindSignaled,
            signaledIndex,
            (int)OSHostStatus.Ok,
            flags: 0,
            waitable);
    }

    private static OSHostWaitResult Result(
        OSHostStatus status,
        uint kind = OSHostWaitConstants.ResultKindNone)
    {
        return new OSHostWaitResult(
            kind,
            signaledIndex: 0,
            (int)status,
            flags: 0,
            signaledHandle: default);
    }

    private static OSHostStatus ValidateTimeoutAndFlags(
        OSHostTimeout timeout,
        uint flags,
        uint reserved)
    {
        if (reserved != 0 || (flags & ~OSHostWaitConstants.WaitFlagWaitAll) != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        if ((flags & OSHostWaitConstants.WaitFlagWaitAll) != 0)
        {
            return OSHostStatus.NotSupported;
        }

        if (timeout.Flags != 0 || !OSHostSocketTimeouts.IsValid(timeout))
        {
            return OSHostStatus.InvalidArgument;
        }

        if (timeout.Kind != OSHostWaitConstants.TimeoutKindImmediate)
        {
            return OSHostStatus.NotSupported;
        }

        return OSHostStatus.Ok;
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

    private OSHostStatus TryFindSlot(OSHostHandle waitable, out int slotIndex)
    {
        if (!waitable.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        uint indexPart = (uint)(waitable.Value & 0xFFFF_FFFFUL);
        uint generation = (uint)(waitable.Value >> 32);
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
        return slots[slotIndex].InUse && slots[slotIndex].Generation == generation
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
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

    private struct WaitSlot
    {
        public bool InUse;

        public uint Generation;

        public OSHostHandle Waitable;

        public OSHostWaitableKind Kind;

        public bool Signaled;

        public bool Canceled;

        public static WaitSlot Create(
            OSHostHandle waitable,
            uint generation,
            OSHostWaitableKind kind,
            bool signaled)
        {
            return new WaitSlot
            {
                InUse = true,
                Generation = generation,
                Waitable = waitable,
                Kind = kind,
                Signaled = signaled,
                Canceled = false
            };
        }
    }
}
