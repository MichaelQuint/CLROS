namespace OS.Pal;
public sealed partial class OSHostThreadTable
{
    private readonly ThreadSlot[] slots;
    private ulong nextThreadId = OSHostThreadConstants.FirstCreatedThreadId;
    public OSHostThreadTable(int capacity)
    {
        slots = capacity > 0 ? new ThreadSlot[capacity] : [];
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
        return new OSHostThreadIdResult(OSHostStatus.Ok, OSHostThreadConstants.CurrentThreadId);
    }

    public OSHostThreadCapabilityResult GetCapabilities()
    {
        return new OSHostThreadCapabilityResult(OSHostStatus.Ok, OSHostThreadConstants.CapabilityCurrentThreadId | OSHostThreadConstants.CapabilityYield | OSHostThreadConstants.CapabilityMockThreadLifecycle | OSHostThreadConstants.CapabilityImmediateSleep | OSHostThreadConstants.CapabilitySingleThreadEarlyMode, (uint)Capacity, (uint)ActiveCount, readyForHostedRuntime: false);
    }

    public OSHostStatus Yield()
    {
        return OSHostStatus.Ok;
    }

    public OSHostThreadSleepResult Sleep(OSHostTimeout timeout)
    {
        if (timeout.Flags != 0 || !OSHostSocketTimeouts.IsValid(timeout))
        {
            return OSHostThreadSleepResult.FromStatus(OSHostStatus.InvalidArgument, timeout.Kind);
        }

        if (timeout.Kind != OSHostSocketConstants.TimeoutKindImmediate)
        {
            return OSHostThreadSleepResult.FromStatus(OSHostStatus.NotSupported, timeout.Kind);
        }

        return new OSHostThreadSleepResult(OSHostStatus.Ok, timeout.Kind, completedSynchronously: true);
    }

    public OSHostStatus TryGetEntry(OSHostHandle thread, out OSHostThreadTableEntry entry)
    {
        OSHostStatus status = TryFindSlot(thread, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            entry = default;
            return status;
        }

        ThreadSlot slot = slots[slotIndex];
        entry = new OSHostThreadTableEntry(slot.Thread, slot.ThreadId, slot.State, slot.Options, slot.ExitCode);
        return OSHostStatus.Ok;
    }

    private static OSHostStatus ValidateCreateOptions(OSHostThreadCreateOptions options)
    {
        if (options.Version != OSHostThreadConstants.CreateOptionsVersion || options.Flags != 0 || options.StackSizeBytes == 0 || options.Start.EntryPoint == 0)
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
        return slots[slotIndex].InUse && slots[slotIndex].Generation == generation ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
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
}
