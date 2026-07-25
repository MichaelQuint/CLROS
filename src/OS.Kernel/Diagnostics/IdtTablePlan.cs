namespace OS.Kernel.Diagnostics;

public enum IdtTablePlanStatus
{
    Ok = 0,
    TableTooSmall = 1,
    HandlerTableTooSmall = 2,
    DescriptorBufferTooSmall = 3,
    InvalidDescriptor = 4,
    MissingRequiredVector = 5,
    DoubleFaultIstRequired = 6
}

public readonly struct IdtTablePlanResult
{
    private IdtTablePlanResult(
        bool succeeded,
        IdtTablePlanStatus status,
        byte vector,
        IdtGateDescriptorStatus descriptorStatus,
        int descriptorCount,
        int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Vector = vector;
        DescriptorStatus = descriptorStatus;
        DescriptorCount = descriptorCount;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }

    public IdtTablePlanStatus Status { get; }

    public byte Vector { get; }

    public IdtGateDescriptorStatus DescriptorStatus { get; }

    public int DescriptorCount { get; }

    public int RequiredBytes { get; }

    public static IdtTablePlanResult Success()
    {
        return new IdtTablePlanResult(
            true,
            IdtTablePlanStatus.Ok,
            0,
            IdtGateDescriptorStatus.Ok,
            IdtTablePlan.EntryCount,
            IdtTablePlan.EncodedByteLength);
    }

    public static IdtTablePlanResult Fail(
        IdtTablePlanStatus status,
        byte vector = 0,
        IdtGateDescriptorStatus descriptorStatus = IdtGateDescriptorStatus.Ok,
        int descriptorCount = 0,
        int requiredBytes = 0)
    {
        return new IdtTablePlanResult(
            false,
            status,
            vector,
            descriptorStatus,
            descriptorCount,
            requiredBytes);
    }
}

public static class IdtTablePlan
{
    public const int EntryCount = 256;
    public const int RequiredFaultVectorCount = 9;
    public const int EncodedByteLength = EntryCount * IdtGateDescriptor.EncodedLength;

    public static byte GetRequiredFaultVector(int index)
    {
        return index switch
        {
            0 => FaultVectorCatalog.DivideError,
            1 => FaultVectorCatalog.InvalidOpcode,
            2 => FaultVectorCatalog.DoubleFault,
            3 => FaultVectorCatalog.DeviceNotAvailable,
            4 => FaultVectorCatalog.InvalidTss,
            5 => FaultVectorCatalog.SegmentNotPresent,
            6 => FaultVectorCatalog.StackSegmentFault,
            7 => FaultVectorCatalog.GeneralProtectionFault,
            8 => FaultVectorCatalog.PageFault,
            _ => 0xff
        };
    }

    public static bool IsRequiredFaultVector(byte vector)
    {
        for (int index = 0; index < RequiredFaultVectorCount; index++)
        {
            if (GetRequiredFaultVector(index) == vector)
            {
                return true;
            }
        }

        return false;
    }

    public static IdtTablePlanResult CreateRequiredFaultTable(
        ReadOnlySpan<ulong> handlerAddresses,
        ushort codeSelector,
        byte defaultIstIndex,
        byte doubleFaultIstIndex,
        Span<IdtGateDescriptor> destination)
    {
        if (destination.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.TableTooSmall,
                descriptorCount: destination.Length,
                requiredBytes: EntryCount);
        }

        if (handlerAddresses.Length < RequiredFaultVectorCount)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.HandlerTableTooSmall,
                descriptorCount: handlerAddresses.Length,
                requiredBytes: RequiredFaultVectorCount);
        }

        if (doubleFaultIstIndex == 0)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.DoubleFaultIstRequired,
                FaultVectorCatalog.DoubleFault);
        }

        destination.Clear();

        for (int index = 0; index < RequiredFaultVectorCount; index++)
        {
            byte vector = GetRequiredFaultVector(index);
            byte istIndex = vector == FaultVectorCatalog.DoubleFault
                ? doubleFaultIstIndex
                : defaultIstIndex;

            IdtGateDescriptorResult descriptorResult = IdtGateDescriptor.Create(
                vector,
                handlerAddresses[index],
                codeSelector,
                istIndex,
                IdtGateType.InterruptGate,
                descriptorPrivilegeLevel: 0,
                present: true);

            if (!descriptorResult.Succeeded)
            {
                return IdtTablePlanResult.Fail(
                    IdtTablePlanStatus.InvalidDescriptor,
                    vector,
                    descriptorResult.Status);
            }

            destination[vector] = descriptorResult.Descriptor;
        }

        return IdtTablePlanResult.Success();
    }

    public static IdtTablePlanResult ValidateRequiredFaultTable(ReadOnlySpan<IdtGateDescriptor> table)
    {
        if (table.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.TableTooSmall,
                descriptorCount: table.Length,
                requiredBytes: EntryCount);
        }

        for (int index = 0; index < RequiredFaultVectorCount; index++)
        {
            byte vector = GetRequiredFaultVector(index);
            IdtGateDescriptor descriptor = table[vector];

            if (!descriptor.Present || descriptor.Vector != vector)
            {
                return IdtTablePlanResult.Fail(
                    IdtTablePlanStatus.MissingRequiredVector,
                    vector);
            }

            if (vector == FaultVectorCatalog.DoubleFault && descriptor.IstIndex == 0)
            {
                return IdtTablePlanResult.Fail(
                    IdtTablePlanStatus.DoubleFaultIstRequired,
                    vector);
            }
        }

        return IdtTablePlanResult.Success();
    }

    public static IdtTablePlanResult WriteDescriptorBytes(
        ReadOnlySpan<IdtGateDescriptor> table,
        Span<byte> destination)
    {
        if (table.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.TableTooSmall,
                descriptorCount: table.Length,
                requiredBytes: EntryCount);
        }

        if (destination.Length < EncodedByteLength)
        {
            return IdtTablePlanResult.Fail(
                IdtTablePlanStatus.DescriptorBufferTooSmall,
                requiredBytes: EncodedByteLength);
        }

        for (int index = 0; index < EntryCount; index++)
        {
            Span<byte> descriptorBytes = destination.Slice(
                index * IdtGateDescriptor.EncodedLength,
                IdtGateDescriptor.EncodedLength);

            if (!table[index].TryWrite(descriptorBytes))
            {
                return IdtTablePlanResult.Fail(
                    IdtTablePlanStatus.DescriptorBufferTooSmall,
                    (byte)index,
                    requiredBytes: EncodedByteLength);
            }
        }

        return IdtTablePlanResult.Success();
    }
}
