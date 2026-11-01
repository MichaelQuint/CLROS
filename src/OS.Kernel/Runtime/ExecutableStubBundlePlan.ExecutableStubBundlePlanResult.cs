namespace OS.Kernel.Runtime;
public readonly struct ExecutableStubBundlePlanResult
{
    public static readonly nuint MissingSlotOffset = nuint.MaxValue;
    private ExecutableStubBundlePlanResult(bool isInitialized, ExecutableStubBundlePlanStatus status, ExecutableStubMemoryPlanStatus memoryPlanStatus, int slotCount, nuint totalPayloadLength, nuint requiredMemoryBytes, nuint com1Out8Offset, nuint com1In8Offset, nuint installIdtOffset, nuint installGdtOffset, nuint loadTaskRegisterOffset, nuint invalidOpcodeProbeOffset, nuint nullWriteProbeOffset)
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
        return Succeeded && kind != ExecutableStubSlotKind.Unknown && kind >= ExecutableStubSlotKind.Com1Out8 && kind <= ExecutableStubSlotKind.NullWriteProbe && offset != MissingSlotOffset;
    }

    public static ExecutableStubBundlePlanResult Create(ExecutableStubBundlePlanStatus status, ExecutableStubMemoryPlanStatus memoryPlanStatus, int slotCount, nuint totalPayloadLength, nuint requiredMemoryBytes, nuint com1Out8Offset, nuint com1In8Offset, nuint installIdtOffset, nuint installGdtOffset, nuint loadTaskRegisterOffset, nuint invalidOpcodeProbeOffset, nuint nullWriteProbeOffset)
    {
        return new ExecutableStubBundlePlanResult(true, status, memoryPlanStatus, slotCount, totalPayloadLength, requiredMemoryBytes, com1Out8Offset, com1In8Offset, installIdtOffset, installGdtOffset, loadTaskRegisterOffset, invalidOpcodeProbeOffset, nullWriteProbeOffset);
    }

    public static ExecutableStubBundlePlanResult Fail(ExecutableStubBundlePlanStatus status, ExecutableStubMemoryPlanStatus memoryPlanStatus, int slotCount, nuint totalPayloadLength, nuint requiredMemoryBytes)
    {
        return new ExecutableStubBundlePlanResult(true, status, memoryPlanStatus, slotCount, totalPayloadLength, requiredMemoryBytes, MissingSlotOffset, MissingSlotOffset, MissingSlotOffset, MissingSlotOffset, MissingSlotOffset, MissingSlotOffset, MissingSlotOffset);
    }

    public static ExecutableStubBundlePlanResult Success(int slotCount, nuint totalPayloadLength, nuint requiredMemoryBytes, nuint com1Out8Offset, nuint com1In8Offset, nuint installIdtOffset, nuint installGdtOffset, nuint loadTaskRegisterOffset, nuint invalidOpcodeProbeOffset, nuint nullWriteProbeOffset)
    {
        return new ExecutableStubBundlePlanResult(true, ExecutableStubBundlePlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, slotCount, totalPayloadLength, requiredMemoryBytes, com1Out8Offset, com1In8Offset, installIdtOffset, installGdtOffset, loadTaskRegisterOffset, invalidOpcodeProbeOffset, nullWriteProbeOffset);
    }
}
