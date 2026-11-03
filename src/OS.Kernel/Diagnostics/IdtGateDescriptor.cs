using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public readonly struct IdtGateDescriptor
{
    public const int EncodedLength = 16;
    public const int MinimumVector = 0;
    public const int MaximumVector = 255;
    public const int MinimumIstIndex = 0;
    public const int MaximumIstIndex = 7;
    public const int MinimumDescriptorPrivilegeLevel = 0;
    public const int MaximumDescriptorPrivilegeLevel = 3;
    private IdtGateDescriptor(int vector, ushort offsetLow, ushort codeSelector, byte istIndex, IdtGateType gateType, byte descriptorPrivilegeLevel, bool present, ushort offsetMiddle, uint offsetHigh)
    {
        Vector = vector;
        OffsetLow = offsetLow;
        CodeSelector = codeSelector;
        IstIndex = istIndex;
        GateType = gateType;
        DescriptorPrivilegeLevel = descriptorPrivilegeLevel;
        Present = present;
        OffsetMiddle = offsetMiddle;
        OffsetHigh = offsetHigh;
    }

    public int Vector
    {
        get;
    }
    public ushort OffsetLow
    {
        get;
    }
    public ushort CodeSelector
    {
        get;
    }
    public byte IstIndex
    {
        get;
    }
    public IdtGateType GateType
    {
        get;
    }
    public byte DescriptorPrivilegeLevel
    {
        get;
    }
    public bool Present
    {
        get;
    }
    public ushort OffsetMiddle
    {
        get;
    }
    public uint OffsetHigh
    {
        get;
    }

    public uint Reserved
    {
        get
        {
            return 0;
        }
    }

    public byte IstByte
    {
        get
        {
            return IstIndex;
        }
    }

    public byte TypeAttributes
    {
        get
        {
            byte attributes = (byte)((DescriptorPrivilegeLevel & 0x03) << 5);
            attributes |= (byte)GateType;
            if (Present)
            {
                attributes |= 0x80;
            }

            return attributes;
        }
    }

    public ulong HandlerAddress
    {
        get
        {
            return OffsetLow | ((ulong)OffsetMiddle << 16) | ((ulong)OffsetHigh << 32);
        }
    }

    public static IdtGateDescriptorResult Create(int vector, ulong handlerAddress, ushort codeSelector, byte istIndex, IdtGateType gateType, byte descriptorPrivilegeLevel, bool present)
    {
        IdtGateDescriptorStatus status = Validate(vector, handlerAddress, codeSelector, istIndex, gateType, descriptorPrivilegeLevel);
        if (status != IdtGateDescriptorStatus.Ok)
        {
            return IdtGateDescriptorResult.Fail(status);
        }

        IdtGateDescriptor descriptor = new IdtGateDescriptor(vector, (ushort)handlerAddress, codeSelector, istIndex, gateType, descriptorPrivilegeLevel, present, (ushort)(handlerAddress >> 16), (uint)(handlerAddress >> 32));
        return IdtGateDescriptorResult.Success(descriptor);
    }

    public static IdtGateDescriptorStatus Validate(int vector, ulong handlerAddress, ushort codeSelector, byte istIndex, IdtGateType gateType, byte descriptorPrivilegeLevel)
    {
        if (vector < MinimumVector || vector > MaximumVector)
        {
            return IdtGateDescriptorStatus.InvalidVector;
        }

        if (handlerAddress == 0)
        {
            return IdtGateDescriptorStatus.NullHandlerAddress;
        }

        if (istIndex > MaximumIstIndex)
        {
            return IdtGateDescriptorStatus.InvalidIstIndex;
        }

        if (codeSelector == 0)
        {
            return IdtGateDescriptorStatus.InvalidSelector;
        }

        if (!IsValidGateType(gateType))
        {
            return IdtGateDescriptorStatus.InvalidGateType;
        }

        if (descriptorPrivilegeLevel > MaximumDescriptorPrivilegeLevel)
        {
            return IdtGateDescriptorStatus.InvalidDescriptorPrivilegeLevel;
        }

        return IdtGateDescriptorStatus.Ok;
    }

    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < EncodedLength)
        {
            return false;
        }

        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(0, sizeof(ushort)), OffsetLow);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(2, sizeof(ushort)), CodeSelector);
        destination[4] = IstByte;
        destination[5] = TypeAttributes;
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(6, sizeof(ushort)), OffsetMiddle);
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(8, sizeof(uint)), OffsetHigh);
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(12, sizeof(uint)), Reserved);
        return true;
    }

    private static bool IsValidGateType(IdtGateType gateType)
    {
        return gateType == IdtGateType.InterruptGate || gateType == IdtGateType.TrapGate;
    }
}
