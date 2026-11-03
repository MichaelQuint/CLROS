using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public readonly struct IdtrDescriptor
{
    public const int EncodedLength = 10;
    public const int RequiredTableByteLength = IdtTablePlan.EncodedByteLength;
    public const ushort RequiredLimit = RequiredTableByteLength - 1;
    private IdtrDescriptor(ushort limit, ulong baseAddress)
    {
        Limit = limit;
        BaseAddress = baseAddress;
    }

    public ushort Limit
    {
        get;
    }
    public ulong BaseAddress
    {
        get;
    }

    public static IdtrDescriptorResult Create(ulong baseAddress, int tableByteLength)
    {
        IdtrDescriptorStatus status = Validate(baseAddress, tableByteLength);
        if (status != IdtrDescriptorStatus.Ok)
        {
            return IdtrDescriptorResult.Fail(status, tableByteLength);
        }

        return IdtrDescriptorResult.Success(new IdtrDescriptor((ushort)(tableByteLength - 1), baseAddress), tableByteLength);
    }

    public static IdtrDescriptorStatus Validate(ulong baseAddress, int tableByteLength)
    {
        if (baseAddress == 0)
        {
            return IdtrDescriptorStatus.NullBaseAddress;
        }

        if (tableByteLength < RequiredTableByteLength)
        {
            return IdtrDescriptorStatus.TableTooSmall;
        }

        if (tableByteLength > RequiredTableByteLength)
        {
            return IdtrDescriptorStatus.TableTooLarge;
        }

        return IdtrDescriptorStatus.Ok;
    }

    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < EncodedLength)
        {
            return false;
        }

        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(0, sizeof(ushort)), Limit);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(2, sizeof(ulong)), BaseAddress);
        return true;
    }

    public IdtrDescriptorResult Write(Span<byte> destination)
    {
        if (!TryWrite(destination))
        {
            return IdtrDescriptorResult.Fail(IdtrDescriptorStatus.OutputBufferTooSmall, destination.Length, EncodedLength);
        }

        return IdtrDescriptorResult.Success(this, EncodedLength, EncodedLength);
    }
}
