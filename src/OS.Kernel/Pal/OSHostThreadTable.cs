namespace OS.Pal;

public enum OSHostThreadLifecycleState : uint
{
    None = 0,
    Created = 1,
    Exited = 2
}

public static class OSHostThreadConstants
{
    public const uint CreateOptionsVersion = 1;

    public const ulong CurrentThreadId = 1;

    public const ulong FirstCreatedThreadId = 2;

    public const ulong CapabilityCurrentThreadId = 1UL << 0;

    public const ulong CapabilityYield = 1UL << 1;

    public const ulong CapabilityMockThreadLifecycle = 1UL << 2;

    public const ulong CapabilityImmediateSleep = 1UL << 3;

    public const ulong CapabilitySingleThreadEarlyMode = 1UL << 4;
}

public readonly struct OSHostThreadCreateResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostHandle Thread;

    public readonly ulong ThreadId;

    public readonly bool IsInitialized;

    public OSHostThreadCreateResult(
        OSHostStatus status,
        OSHostHandle thread,
        ulong threadId,
        bool isInitialized = true)
    {
        Status = status;
        Thread = thread;
        ThreadId = threadId;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && Thread.IsValid
        && ThreadId != 0;

    public static OSHostThreadCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadCreateResult(status, default, threadId: 0);
    }
}

public readonly struct OSHostThreadJoinResult
{
    public readonly OSHostStatus Status;

    public readonly int ExitCode;

    public readonly bool IsInitialized;

    public OSHostThreadJoinResult(
        OSHostStatus status,
        int exitCode,
        bool isInitialized = true)
    {
        Status = status;
        ExitCode = exitCode;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostThreadJoinResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadJoinResult(status, exitCode: 0);
    }
}

public readonly struct OSHostThreadIdResult
{
    public readonly OSHostStatus Status;

    public readonly ulong ThreadId;

    public readonly bool IsInitialized;

    public OSHostThreadIdResult(
        OSHostStatus status,
        ulong threadId,
        bool isInitialized = true)
    {
        Status = status;
        ThreadId = threadId;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && ThreadId != 0;

    public static OSHostThreadIdResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadIdResult(status, threadId: 0);
    }
}

public readonly struct OSHostThreadCapabilityResult
{
    public readonly OSHostStatus Status;

    public readonly ulong CapabilityFlags;

    public readonly uint MaxThreadCount;

    public readonly uint ActiveThreadCount;

    public readonly bool ReadyForHostedRuntime;

    public readonly bool IsInitialized;

    public OSHostThreadCapabilityResult(
        OSHostStatus status,
        ulong capabilityFlags,
        uint maxThreadCount,
        uint activeThreadCount,
        bool readyForHostedRuntime,
        bool isInitialized = true)
    {
        Status = status;
        CapabilityFlags = capabilityFlags;
        MaxThreadCount = maxThreadCount;
        ActiveThreadCount = activeThreadCount;
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

    public static OSHostThreadCapabilityResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadCapabilityResult(
            status,
            capabilityFlags: 0,
            maxThreadCount: 0,
            activeThreadCount: 0,
            readyForHostedRuntime: false);
    }
}

public readonly struct OSHostThreadSleepResult
{
    public readonly OSHostStatus Status;

    public readonly uint TimeoutKind;

    public readonly bool CompletedSynchronously;

    public readonly bool IsInitialized;

    public OSHostThreadSleepResult(
        OSHostStatus status,
        uint timeoutKind,
        bool completedSynchronously,
        bool isInitialized = true)
    {
        Status = status;
        TimeoutKind = timeoutKind;
        CompletedSynchronously = completedSynchronously;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && CompletedSynchronously;

    public static OSHostThreadSleepResult FromStatus(
        OSHostStatus status,
        uint timeoutKind)
    {
        return new OSHostThreadSleepResult(
            status,
            timeoutKind,
            completedSynchronously: false);
    }
}

public readonly struct OSHostThreadTableEntry
{
    public readonly OSHostHandle Thread;

    public readonly ulong ThreadId;

    public readonly OSHostThreadLifecycleState State;

    public readonly OSHostThreadCreateOptions Options;

    public readonly int ExitCode;

    public OSHostThreadTableEntry(
        OSHostHandle thread,
        ulong threadId,
        OSHostThreadLifecycleState state,
        OSHostThreadCreateOptions options,
        int exitCode)
    {
        Thread = thread;
        ThreadId = threadId;
        State = state;
        Options = options;
        ExitCode = exitCode;
    }
}

public sealed class OSHostThreadTable
{
    private readonly ThreadSlot[] slots;

    private ulong nextThreadId = OSHostThreadConstants.FirstCreatedThreadId;

    public OSHostThreadTable(int capacity)
    {
        slots = capacity > 0
            ? new ThreadSlot[capacity]
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

    public OSHostThreadCreateResult Create(OSHostThreadCreateOptions options)
    {
        OSHostStatus optionsStatus = ValidateCreateOptions(options);
        if (optionsStatus != OSHostStatus.Ok)
        {
            return OSHostThreadCreateResult.FromStatus(optionsStatus);
        }

        int slotIndex = FindFreeSlot();
        if (slotIndex < 0)
        {
            return OSHostThreadCreateResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        ulong threadId = NextThreadId();
        if (threadId == 0)
        {
            return OSHostThreadCreateResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        uint generation = NextGeneration(slots[slotIndex].Generation);
        OSHostHandle thread = CreateHandle(slotIndex, generation);
        slots[slotIndex] = ThreadSlot.Create(thread, generation, threadId, options);
        return new OSHostThreadCreateResult(OSHostStatus.Ok, thread, threadId);
    }

    public OSHostStatus MarkExited(OSHostHandle thread, int exitCode)
    {
        OSHostStatus status = TryFindSlot(thread, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        if (slots[slotIndex].State == OSHostThreadLifecycleState.Exited)
        {
            return OSHostStatus.Closed;
        }

        slots[slotIndex].State = OSHostThreadLifecycleState.Exited;
        slots[slotIndex].ExitCode = exitCode;
        return OSHostStatus.Ok;
    }

    public OSHostThreadJoinResult Join(OSHostHandle thread)
    {
        OSHostStatus status = TryFindSlot(thread, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return OSHostThreadJoinResult.FromStatus(status);
        }

        if (slots[slotIndex].State != OSHostThreadLifecycleState.Exited)
        {
            return OSHostThreadJoinResult.FromStatus(OSHostStatus.WouldBlock);
        }

        int exitCode = slots[slotIndex].ExitCode;
        uint generation = slots[slotIndex].Generation;
        slots[slotIndex] = new ThreadSlot
        {
            Generation = generation
        };
        return new OSHostThreadJoinResult(OSHostStatus.Ok, exitCode);
    }

    public OSHostThreadIdResult GetCurrentThreadId()
    {
        return new OSHostThreadIdResult(
            OSHostStatus.Ok,
            OSHostThreadConstants.CurrentThreadId);
    }

    public OSHostThreadCapabilityResult GetCapabilities()
    {
        return new OSHostThreadCapabilityResult(
            OSHostStatus.Ok,
            OSHostThreadConstants.CapabilityCurrentThreadId
                | OSHostThreadConstants.CapabilityYield
                | OSHostThreadConstants.CapabilityMockThreadLifecycle
                | OSHostThreadConstants.CapabilityImmediateSleep
                | OSHostThreadConstants.CapabilitySingleThreadEarlyMode,
            (uint)Capacity,
            (uint)ActiveCount,
            readyForHostedRuntime: false);
    }

    public OSHostStatus Yield()
    {
        return OSHostStatus.Ok;
    }

    public OSHostThreadSleepResult Sleep(OSHostTimeout timeout)
    {
        if (timeout.Flags != 0 || !OSHostSocketTimeouts.IsValid(timeout))
        {
            return OSHostThreadSleepResult.FromStatus(
                OSHostStatus.InvalidArgument,
                timeout.Kind);
        }

        if (timeout.Kind != OSHostSocketConstants.TimeoutKindImmediate)
        {
            return OSHostThreadSleepResult.FromStatus(
                OSHostStatus.NotSupported,
                timeout.Kind);
        }

        return new OSHostThreadSleepResult(
            OSHostStatus.Ok,
            timeout.Kind,
            completedSynchronously: true);
    }

    public OSHostStatus TryGetEntry(
        OSHostHandle thread,
        out OSHostThreadTableEntry entry)
    {
        OSHostStatus status = TryFindSlot(thread, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            entry = default;
            return status;
        }

        ThreadSlot slot = slots[slotIndex];
        entry = new OSHostThreadTableEntry(
            slot.Thread,
            slot.ThreadId,
            slot.State,
            slot.Options,
            slot.ExitCode);
        return OSHostStatus.Ok;
    }

    private static OSHostStatus ValidateCreateOptions(
        OSHostThreadCreateOptions options)
    {
        if (options.Version != OSHostThreadConstants.CreateOptionsVersion ||
            options.Flags != 0 ||
            options.StackSizeBytes == 0 ||
            options.Start.EntryPoint == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (options.GuardSizeBytes > options.StackSizeBytes)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (options.NameUtf8.Length != 0 && options.NameUtf8.Address == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (options.StackSizeBytes > ulong.MaxValue - options.GuardSizeBytes)
        {
            return OSHostStatus.InvalidArgument;
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

    private OSHostStatus TryFindSlot(OSHostHandle thread, out int slotIndex)
    {
        if (!thread.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        uint indexPart = (uint)(thread.Value & 0xFFFF_FFFFUL);
        uint generation = (uint)(thread.Value >> 32);
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

    private ulong NextThreadId()
    {
        ulong id = nextThreadId;
        nextThreadId++;
        if (nextThreadId == 0)
        {
            nextThreadId = OSHostThreadConstants.FirstCreatedThreadId;
        }

        return id;
    }

    private struct ThreadSlot
    {
        public bool InUse;

        public uint Generation;

        public OSHostHandle Thread;

        public ulong ThreadId;

        public OSHostThreadLifecycleState State;

        public OSHostThreadCreateOptions Options;

        public int ExitCode;

        public static ThreadSlot Create(
            OSHostHandle thread,
            uint generation,
            ulong threadId,
            OSHostThreadCreateOptions options)
        {
            return new ThreadSlot
            {
                InUse = true,
                Generation = generation,
                Thread = thread,
                ThreadId = threadId,
                State = OSHostThreadLifecycleState.Created,
                Options = options,
                ExitCode = 0
            };
        }
    }
}
