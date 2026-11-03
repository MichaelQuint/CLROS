namespace OS.Kernel.Diagnostics;

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

    public static IdtTablePlanResult CreateRequiredFaultTable(ReadOnlySpan<ulong> handlerAddresses, ushort codeSelector, byte defaultIstIndex, byte doubleFaultIstIndex, Span<IdtGateDescriptor> destination)
    {
        if (destination.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.TableTooSmall, descriptorCount: destination.Length, requiredBytes: EntryCount);
        }

        if (handlerAddresses.Length < RequiredFaultVectorCount)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.HandlerTableTooSmall, descriptorCount: handlerAddresses.Length, requiredBytes: RequiredFaultVectorCount);
        }

        if (doubleFaultIstIndex == 0)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.DoubleFaultIstRequired, FaultVectorCatalog.DoubleFault);
        }

        destination.Clear();
        for (int index = 0; index < RequiredFaultVectorCount; index++)
        {
            byte vector = GetRequiredFaultVector(index);
            byte istIndex = vector == FaultVectorCatalog.DoubleFault ? doubleFaultIstIndex : defaultIstIndex;
            IdtGateDescriptorResult descriptorResult = IdtGateDescriptor.Create(vector, handlerAddresses[index], codeSelector, istIndex, IdtGateType.InterruptGate, descriptorPrivilegeLevel: 0, present: true);
            if (!descriptorResult.Succeeded)
            {
                return IdtTablePlanResult.Fail(IdtTablePlanStatus.InvalidDescriptor, vector, descriptorResult.Status);
            }

            destination[vector] = descriptorResult.Descriptor;
        }

        return IdtTablePlanResult.Success();
    }

    public static IdtTablePlanResult ValidateRequiredFaultTable(ReadOnlySpan<IdtGateDescriptor> table)
    {
        if (table.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.TableTooSmall, descriptorCount: table.Length, requiredBytes: EntryCount);
        }

        for (int index = 0; index < RequiredFaultVectorCount; index++)
        {
            byte vector = GetRequiredFaultVector(index);
            IdtGateDescriptor descriptor = table[vector];
            if (!descriptor.Present || descriptor.Vector != vector)
            {
                return IdtTablePlanResult.Fail(IdtTablePlanStatus.MissingRequiredVector, vector);
            }

            if (vector == FaultVectorCatalog.DoubleFault && descriptor.IstIndex == 0)
            {
                return IdtTablePlanResult.Fail(IdtTablePlanStatus.DoubleFaultIstRequired, vector);
            }
        }

        return IdtTablePlanResult.Success();
    }

    public static IdtTablePlanResult WriteDescriptorBytes(ReadOnlySpan<IdtGateDescriptor> table, Span<byte> destination)
    {
        if (table.Length < EntryCount)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.TableTooSmall, descriptorCount: table.Length, requiredBytes: EntryCount);
        }

        if (destination.Length < EncodedByteLength)
        {
            return IdtTablePlanResult.Fail(IdtTablePlanStatus.DescriptorBufferTooSmall, requiredBytes: EncodedByteLength);
        }

        for (int index = 0; index < EntryCount; index++)
        {
            Span<byte> descriptorBytes = destination.Slice(index * IdtGateDescriptor.EncodedLength, IdtGateDescriptor.EncodedLength);
            if (!table[index].TryWrite(descriptorBytes))
            {
                return IdtTablePlanResult.Fail(IdtTablePlanStatus.DescriptorBufferTooSmall, (byte)index, requiredBytes: EncodedByteLength);
            }
        }

        return IdtTablePlanResult.Success();
    }
}
