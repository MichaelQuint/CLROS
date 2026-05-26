namespace OS.Drivers;

public enum EnaAdminQueueLayoutPlanStatus
{
    Success = 0,
    InvalidDepth = 1,
    InvalidEntrySize = 2,
    InvalidAlignment = 3,
    InsufficientMemory = 4,
    LayoutOverflow = 5
}

public readonly struct EnaAdminQueueRegionPlan
{
    internal EnaAdminQueueRegionPlan(
        uint offset,
        uint length,
        uint depth,
        uint entrySize,
        uint alignment)
    {
        Offset = offset;
        Length = length;
        Depth = depth;
        EntrySize = entrySize;
        Alignment = alignment;
        EndOffset = offset + length;
    }

    public uint Offset { get; }

    public uint Length { get; }

    public uint Depth { get; }

    public uint EntrySize { get; }

    public uint Alignment { get; }

    public uint EndOffset { get; }

    public bool IsAligned => Alignment != 0 && (Offset % Alignment) == 0;
}

public readonly struct EnaAdminQueueLayoutPlan
{
    internal EnaAdminQueueLayoutPlan(
        EnaAdminQueueRegionPlan adminQueue,
        EnaAdminQueueRegionPlan adminCompletionQueue,
        uint totalRequiredBytes,
        uint memoryBudgetBytes,
        uint baseAlignment,
        bool requiresContiguousMemory)
    {
        AdminQueue = adminQueue;
        AdminCompletionQueue = adminCompletionQueue;
        TotalRequiredBytes = totalRequiredBytes;
        MemoryBudgetBytes = memoryBudgetBytes;
        BaseAlignment = baseAlignment;
        RequiresContiguousMemory = requiresContiguousMemory;
    }

    public const uint MinimumDepth = 1;
    public const uint MaximumDepth = 1U << 20;
    public const uint MinimumEntrySize = 4;
    public const uint MaximumEntrySize = 1U << 20;
    public const uint DefaultBaseAlignment = 4096;

    public const ushort InitialAdminQueueProducerIndex = 0;
    public const ushort InitialAdminCompletionQueueConsumerIndex = 0;
    public const byte InitialAdminCompletionPhase = 1;

    public EnaAdminQueueRegionPlan AdminQueue { get; }

    public EnaAdminQueueRegionPlan AdminCompletionQueue { get; }

    public uint TotalRequiredBytes { get; }

    public uint MemoryBudgetBytes { get; }

    public uint BaseAlignment { get; }

    public bool RequiresContiguousMemory { get; }

    public bool FitsBudget =>
        TotalRequiredBytes != 0 && TotalRequiredBytes <= MemoryBudgetBytes;

    public static EnaAdminQueueLayoutPlanResult Create(
        uint adminQueueDepth,
        uint adminQueueEntrySize,
        uint adminCompletionQueueDepth,
        uint adminCompletionQueueEntrySize,
        uint baseAlignment,
        uint memoryBudgetBytes,
        bool requiresContiguousMemory)
    {
        if (!IsValidDepth(adminQueueDepth) || !IsValidDepth(adminCompletionQueueDepth))
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.InvalidDepth,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory);
        }

        if (!IsValidEntrySize(adminQueueEntrySize) ||
            !IsValidEntrySize(adminCompletionQueueEntrySize))
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.InvalidEntrySize,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory);
        }

        if (!IsPowerOfTwo(baseAlignment))
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.InvalidAlignment,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory);
        }

        if (!TryMultiply(adminQueueDepth, adminQueueEntrySize, out uint adminQueueBytes) ||
            !TryMultiply(
                adminCompletionQueueDepth,
                adminCompletionQueueEntrySize,
                out uint adminCompletionQueueBytes))
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.LayoutOverflow,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory);
        }

        uint adminQueueOffset = 0;
        if (!TryAdd(adminQueueOffset, adminQueueBytes, out uint adminQueueEnd) ||
            !TryAlignUp(adminQueueEnd, baseAlignment, out uint adminCompletionQueueOffset) ||
            !TryAdd(
                adminCompletionQueueOffset,
                adminCompletionQueueBytes,
                out uint totalRequiredBytes))
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.LayoutOverflow,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory);
        }

        if (totalRequiredBytes > memoryBudgetBytes)
        {
            return EnaAdminQueueLayoutPlanResult.Fail(
                EnaAdminQueueLayoutPlanStatus.InsufficientMemory,
                adminQueueDepth,
                adminCompletionQueueDepth,
                adminQueueEntrySize,
                adminCompletionQueueEntrySize,
                baseAlignment,
                memoryBudgetBytes,
                requiresContiguousMemory,
                totalRequiredBytes);
        }

        EnaAdminQueueRegionPlan adminQueue = new EnaAdminQueueRegionPlan(
            adminQueueOffset,
            adminQueueBytes,
            adminQueueDepth,
            adminQueueEntrySize,
            baseAlignment);
        EnaAdminQueueRegionPlan adminCompletionQueue = new EnaAdminQueueRegionPlan(
            adminCompletionQueueOffset,
            adminCompletionQueueBytes,
            adminCompletionQueueDepth,
            adminCompletionQueueEntrySize,
            baseAlignment);

        return EnaAdminQueueLayoutPlanResult.Success(new EnaAdminQueueLayoutPlan(
            adminQueue,
            adminCompletionQueue,
            totalRequiredBytes,
            memoryBudgetBytes,
            baseAlignment,
            requiresContiguousMemory));
    }

    private static bool IsValidDepth(uint depth)
    {
        return depth >= MinimumDepth &&
            depth <= MaximumDepth &&
            IsPowerOfTwo(depth);
    }

    private static bool IsValidEntrySize(uint entrySize)
    {
        return entrySize >= MinimumEntrySize &&
            entrySize <= MaximumEntrySize &&
            (entrySize % MinimumEntrySize) == 0;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAlignUp(
        uint value,
        uint alignment,
        out uint aligned)
    {
        uint remainder = value % alignment;
        if (remainder == 0)
        {
            aligned = value;
            return true;
        }

        return TryAdd(value, alignment - remainder, out aligned);
    }

    private static bool TryAdd(
        uint left,
        uint right,
        out uint result)
    {
        if (left > uint.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }

    private static bool TryMultiply(
        uint left,
        uint right,
        out uint result)
    {
        ulong value = (ulong)left * right;
        if (value > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }
}

public readonly struct EnaAdminQueueLayoutPlanResult
{
    private EnaAdminQueueLayoutPlanResult(
        bool isInitialized,
        EnaAdminQueueLayoutPlanStatus status,
        uint adminQueueDepth,
        uint adminCompletionQueueDepth,
        uint adminQueueEntrySize,
        uint adminCompletionQueueEntrySize,
        uint baseAlignment,
        uint memoryBudgetBytes,
        bool requiresContiguousMemory,
        uint totalRequiredBytes,
        EnaAdminQueueLayoutPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        AdminQueueDepth = adminQueueDepth;
        AdminCompletionQueueDepth = adminCompletionQueueDepth;
        AdminQueueEntrySize = adminQueueEntrySize;
        AdminCompletionQueueEntrySize = adminCompletionQueueEntrySize;
        BaseAlignment = baseAlignment;
        MemoryBudgetBytes = memoryBudgetBytes;
        RequiresContiguousMemory = requiresContiguousMemory;
        TotalRequiredBytes = totalRequiredBytes;
        Plan = plan;
    }

    public EnaAdminQueueLayoutPlanStatus Status { get; }

    public uint AdminQueueDepth { get; }

    public uint AdminCompletionQueueDepth { get; }

    public uint AdminQueueEntrySize { get; }

    public uint AdminCompletionQueueEntrySize { get; }

    public uint BaseAlignment { get; }

    public uint MemoryBudgetBytes { get; }

    public bool RequiresContiguousMemory { get; }

    public uint TotalRequiredBytes { get; }

    public EnaAdminQueueLayoutPlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaAdminQueueLayoutPlanStatus.Success;

    internal static EnaAdminQueueLayoutPlanResult Success(EnaAdminQueueLayoutPlan plan)
    {
        return new EnaAdminQueueLayoutPlanResult(
            isInitialized: true,
            EnaAdminQueueLayoutPlanStatus.Success,
            plan.AdminQueue.Depth,
            plan.AdminCompletionQueue.Depth,
            plan.AdminQueue.EntrySize,
            plan.AdminCompletionQueue.EntrySize,
            plan.BaseAlignment,
            plan.MemoryBudgetBytes,
            plan.RequiresContiguousMemory,
            plan.TotalRequiredBytes,
            plan);
    }

    internal static EnaAdminQueueLayoutPlanResult Fail(
        EnaAdminQueueLayoutPlanStatus status,
        uint adminQueueDepth,
        uint adminCompletionQueueDepth,
        uint adminQueueEntrySize,
        uint adminCompletionQueueEntrySize,
        uint baseAlignment,
        uint memoryBudgetBytes,
        bool requiresContiguousMemory,
        uint totalRequiredBytes = 0)
    {
        return new EnaAdminQueueLayoutPlanResult(
            isInitialized: true,
            status,
            adminQueueDepth,
            adminCompletionQueueDepth,
            adminQueueEntrySize,
            adminCompletionQueueEntrySize,
            baseAlignment,
            memoryBudgetBytes,
            requiresContiguousMemory,
            totalRequiredBytes,
            default);
    }
}
