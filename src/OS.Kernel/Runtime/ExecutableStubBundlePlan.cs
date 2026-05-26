namespace OS.Kernel.Runtime;

public enum ExecutableStubSlotKind : byte
{
    Unknown = 0,
    Com1Out8 = 1,
    Com1In8 = 2,
    InstallIdt = 3,
    InstallGdt = 4,
    LoadTaskRegister = 5,
    InvalidOpcodeProbe = 6,
    NullWriteProbe = 7
}

public enum ExecutableStubBundlePlanStatus
{
    Success = 0,
    InvalidMemoryPlan = 1,
    InsufficientMemoryPlan = 2,
    DuplicateSlot = 3,
    ZeroLengthSlot = 4,
    UnknownSlotKind = 5,
    PayloadLengthOverflow = 6,
    RequiredMemoryLengthOverflow = 7
}

public readonly struct ExecutableStubBundleSlot
{
    public ExecutableStubBundleSlot(ExecutableStubSlotKind kind, nuint byteLength)
    {
        Kind = kind;
        ByteLength = byteLength;
    }

    public ExecutableStubSlotKind Kind { get; }

    public nuint ByteLength { get; }
}

public readonly struct ExecutableStubBundlePlanResult
{
    public static readonly nuint MissingSlotOffset = nuint.MaxValue;

    private ExecutableStubBundlePlanResult(
        bool isInitialized,
        ExecutableStubBundlePlanStatus status,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        int slotCount,
        nuint totalPayloadLength,
        nuint requiredMemoryBytes,
        nuint com1Out8Offset,
        nuint com1In8Offset,
        nuint installIdtOffset,
        nuint installGdtOffset,
        nuint loadTaskRegisterOffset,
        nuint invalidOpcodeProbeOffset,
        nuint nullWriteProbeOffset)
    {
        IsInitialized = isInitialized;
        Status = status;
        MemoryPlanStatus = memoryPlanStatus;
        SlotCount = slotCount;
        TotalPayloadLength = totalPayloadLength;
        RequiredMemoryBytes = requiredMemoryBytes;
        Com1Out8Offset = com1Out8Offset;
        Com1In8Offset = com1In8Offset;
        InstallIdtOffset = installIdtOffset;
        InstallGdtOffset = installGdtOffset;
        LoadTaskRegisterOffset = loadTaskRegisterOffset;
        InvalidOpcodeProbeOffset = invalidOpcodeProbeOffset;
        NullWriteProbeOffset = nullWriteProbeOffset;
    }

    public bool IsInitialized { get; }

    public ExecutableStubBundlePlanStatus Status { get; }

    public ExecutableStubMemoryPlanStatus MemoryPlanStatus { get; }

    public bool Succeeded => IsInitialized && Status == ExecutableStubBundlePlanStatus.Success;

    public int SlotCount { get; }

    public nuint TotalPayloadLength { get; }

    public nuint RequiredMemoryBytes { get; }

    public nuint Com1Out8Offset { get; }

    public nuint Com1In8Offset { get; }

    public nuint InstallIdtOffset { get; }

    public nuint InstallGdtOffset { get; }

    public nuint LoadTaskRegisterOffset { get; }

    public nuint InvalidOpcodeProbeOffset { get; }

    public nuint NullWriteProbeOffset { get; }

    public bool TryGetSlotOffset(ExecutableStubSlotKind kind, out nuint offset)
    {
        offset = kind switch
        {
            ExecutableStubSlotKind.Com1Out8 => Com1Out8Offset,
            ExecutableStubSlotKind.Com1In8 => Com1In8Offset,
            ExecutableStubSlotKind.InstallIdt => InstallIdtOffset,
            ExecutableStubSlotKind.InstallGdt => InstallGdtOffset,
            ExecutableStubSlotKind.LoadTaskRegister => LoadTaskRegisterOffset,
            ExecutableStubSlotKind.InvalidOpcodeProbe => InvalidOpcodeProbeOffset,
            ExecutableStubSlotKind.NullWriteProbe => NullWriteProbeOffset,
            _ => 0
        };

        return Succeeded
            && kind != ExecutableStubSlotKind.Unknown
            && kind >= ExecutableStubSlotKind.Com1Out8
            && kind <= ExecutableStubSlotKind.NullWriteProbe
            && offset != MissingSlotOffset;
    }

    public static ExecutableStubBundlePlanResult Create(
        ExecutableStubBundlePlanStatus status,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        int slotCount,
        nuint totalPayloadLength,
        nuint requiredMemoryBytes,
        nuint com1Out8Offset,
        nuint com1In8Offset,
        nuint installIdtOffset,
        nuint installGdtOffset,
        nuint loadTaskRegisterOffset,
        nuint invalidOpcodeProbeOffset,
        nuint nullWriteProbeOffset)
    {
        return new ExecutableStubBundlePlanResult(
            true,
            status,
            memoryPlanStatus,
            slotCount,
            totalPayloadLength,
            requiredMemoryBytes,
            com1Out8Offset,
            com1In8Offset,
            installIdtOffset,
            installGdtOffset,
            loadTaskRegisterOffset,
            invalidOpcodeProbeOffset,
            nullWriteProbeOffset);
    }

    public static ExecutableStubBundlePlanResult Fail(
        ExecutableStubBundlePlanStatus status,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        int slotCount,
        nuint totalPayloadLength,
        nuint requiredMemoryBytes)
    {
        return new ExecutableStubBundlePlanResult(
            true,
            status,
            memoryPlanStatus,
            slotCount,
            totalPayloadLength,
            requiredMemoryBytes,
            MissingSlotOffset,
            MissingSlotOffset,
            MissingSlotOffset,
            MissingSlotOffset,
            MissingSlotOffset,
            MissingSlotOffset,
            MissingSlotOffset);
    }

    public static ExecutableStubBundlePlanResult Success(
        int slotCount,
        nuint totalPayloadLength,
        nuint requiredMemoryBytes,
        nuint com1Out8Offset,
        nuint com1In8Offset,
        nuint installIdtOffset,
        nuint installGdtOffset,
        nuint loadTaskRegisterOffset,
        nuint invalidOpcodeProbeOffset,
        nuint nullWriteProbeOffset)
    {
        return new ExecutableStubBundlePlanResult(
            true,
            ExecutableStubBundlePlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            slotCount,
            totalPayloadLength,
            requiredMemoryBytes,
            com1Out8Offset,
            com1In8Offset,
            installIdtOffset,
            installGdtOffset,
            loadTaskRegisterOffset,
            invalidOpcodeProbeOffset,
            nullWriteProbeOffset);
    }
}

public static class ExecutableStubBundlePlan
{
    public const int StableSlotCount = 7;
    public static readonly nuint MissingSlotOffset = ExecutableStubBundlePlanResult.MissingSlotOffset;

    public static ExecutableStubBundlePlanResult Create(
        ReadOnlySpan<ExecutableStubBundleSlot> slots,
        ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        if (!IsValidMemoryPlan(executableStubMemoryPlan))
        {
            return ExecutableStubBundlePlanResult.Fail(
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                executableStubMemoryPlan.Status,
                0,
                0,
                0);
        }

        byte seenSlots = 0;
        int slotCount = 0;
        nuint nextOffset = 0;
        nuint com1Out8Offset = MissingSlotOffset;
        nuint com1In8Offset = MissingSlotOffset;
        nuint installIdtOffset = MissingSlotOffset;
        nuint installGdtOffset = MissingSlotOffset;
        nuint loadTaskRegisterOffset = MissingSlotOffset;
        nuint invalidOpcodeProbeOffset = MissingSlotOffset;
        nuint nullWriteProbeOffset = MissingSlotOffset;

        for (int i = 0; i < slots.Length; i++)
        {
            ExecutableStubBundleSlot slot = slots[i];
            if (!IsKnownSlotKind(slot.Kind))
            {
                return ExecutableStubBundlePlanResult.Fail(
                    ExecutableStubBundlePlanStatus.UnknownSlotKind,
                    executableStubMemoryPlan.Status,
                    slotCount,
                    nextOffset,
                    0);
            }

            if (slot.ByteLength == 0)
            {
                return ExecutableStubBundlePlanResult.Fail(
                    ExecutableStubBundlePlanStatus.ZeroLengthSlot,
                    executableStubMemoryPlan.Status,
                    slotCount,
                    nextOffset,
                    0);
            }

            byte slotMask = GetSlotMask(slot.Kind);
            if ((seenSlots & slotMask) != 0)
            {
                return ExecutableStubBundlePlanResult.Fail(
                    ExecutableStubBundlePlanStatus.DuplicateSlot,
                    executableStubMemoryPlan.Status,
                    slotCount,
                    nextOffset,
                    0);
            }

            seenSlots |= slotMask;

            nuint alignedOffset = ExecutableStubMemoryPlan.AlignUp(nextOffset, executableStubMemoryPlan.Alignment);
            if (alignedOffset < nextOffset)
            {
                return ExecutableStubBundlePlanResult.Fail(
                    ExecutableStubBundlePlanStatus.PayloadLengthOverflow,
                    executableStubMemoryPlan.Status,
                    slotCount,
                    nextOffset,
                    0);
            }

            nuint endOffset = alignedOffset + slot.ByteLength;
            if (endOffset < alignedOffset)
            {
                return ExecutableStubBundlePlanResult.Fail(
                    ExecutableStubBundlePlanStatus.PayloadLengthOverflow,
                    executableStubMemoryPlan.Status,
                    slotCount,
                    nextOffset,
                    0);
            }

            SetSlotOffset(
                slot.Kind,
                alignedOffset,
                ref com1Out8Offset,
                ref com1In8Offset,
                ref installIdtOffset,
                ref installGdtOffset,
                ref loadTaskRegisterOffset,
                ref invalidOpcodeProbeOffset,
                ref nullWriteProbeOffset);

            nextOffset = endOffset;
            slotCount++;
        }

        nuint requiredMemoryBytes = executableStubMemoryPlan.PayloadOffset + nextOffset;
        if (requiredMemoryBytes < executableStubMemoryPlan.PayloadOffset)
        {
            return ExecutableStubBundlePlanResult.Fail(
                ExecutableStubBundlePlanStatus.RequiredMemoryLengthOverflow,
                executableStubMemoryPlan.Status,
                slotCount,
                nextOffset,
                0);
        }

        requiredMemoryBytes = ExecutableStubMemoryPlan.AlignUp(
            requiredMemoryBytes,
            executableStubMemoryPlan.PageSize);
        if (requiredMemoryBytes < executableStubMemoryPlan.PayloadOffset)
        {
            return ExecutableStubBundlePlanResult.Fail(
                ExecutableStubBundlePlanStatus.RequiredMemoryLengthOverflow,
                executableStubMemoryPlan.Status,
                slotCount,
                nextOffset,
                0);
        }

        if (requiredMemoryBytes > executableStubMemoryPlan.RequiredByteLength)
        {
            return ExecutableStubBundlePlanResult.Fail(
                ExecutableStubBundlePlanStatus.InsufficientMemoryPlan,
                executableStubMemoryPlan.Status,
                slotCount,
                nextOffset,
                requiredMemoryBytes);
        }

        return ExecutableStubBundlePlanResult.Success(
            slotCount,
            nextOffset,
            requiredMemoryBytes,
            com1Out8Offset,
            com1In8Offset,
            installIdtOffset,
            installGdtOffset,
            loadTaskRegisterOffset,
            invalidOpcodeProbeOffset,
            nullWriteProbeOffset);
    }

    public static bool IsKnownSlotKind(ExecutableStubSlotKind kind)
    {
        return kind >= ExecutableStubSlotKind.Com1Out8
            && kind <= ExecutableStubSlotKind.NullWriteProbe;
    }

    private static byte GetSlotMask(ExecutableStubSlotKind kind)
    {
        return (byte)(1 << ((int)kind - 1));
    }

    private static bool IsValidMemoryPlan(ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        return executableStubMemoryPlan.Succeeded
            && executableStubMemoryPlan.PageSize >= ExecutableStubMemoryPlan.MinimumPageSize
            && executableStubMemoryPlan.Alignment != 0
            && executableStubMemoryPlan.Alignment <= executableStubMemoryPlan.PageSize
            && executableStubMemoryPlan.PayloadOffset <= executableStubMemoryPlan.RequiredByteLength;
    }

    private static void SetSlotOffset(
        ExecutableStubSlotKind kind,
        nuint offset,
        ref nuint com1Out8Offset,
        ref nuint com1In8Offset,
        ref nuint installIdtOffset,
        ref nuint installGdtOffset,
        ref nuint loadTaskRegisterOffset,
        ref nuint invalidOpcodeProbeOffset,
        ref nuint nullWriteProbeOffset)
    {
        switch (kind)
        {
            case ExecutableStubSlotKind.Com1Out8:
                com1Out8Offset = offset;
                break;
            case ExecutableStubSlotKind.Com1In8:
                com1In8Offset = offset;
                break;
            case ExecutableStubSlotKind.InstallIdt:
                installIdtOffset = offset;
                break;
            case ExecutableStubSlotKind.InstallGdt:
                installGdtOffset = offset;
                break;
            case ExecutableStubSlotKind.LoadTaskRegister:
                loadTaskRegisterOffset = offset;
                break;
            case ExecutableStubSlotKind.InvalidOpcodeProbe:
                invalidOpcodeProbeOffset = offset;
                break;
            case ExecutableStubSlotKind.NullWriteProbe:
                nullWriteProbeOffset = offset;
                break;
        }
    }
}
