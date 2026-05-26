namespace OS.Drivers;

using OS.Std.Binary;

public enum EnaAdminDescriptorStatus
{
    Success = 0,
    BufferTooSmall = 1,
    CommandIdOutOfRange = 2,
    PhysicalAddressOutOfRange = 3,
    CommandIdMismatch = 4,
    PhaseMismatch = 5,
    CompletionStatusNotSuccess = 6,
    CompletionEntryNotInitialized = 7
}

public enum EnaAdminOpcode : byte
{
    CreateSubmissionQueue = 1,
    DestroySubmissionQueue = 2,
    CreateCompletionQueue = 3,
    DestroyCompletionQueue = 4,
    GetFeature = 8,
    SetFeature = 9,
    GetStatistics = 11
}

public enum EnaAdminCompletionStatus : byte
{
    Success = 0,
    ResourceAllocationFailure = 1,
    BadOpcode = 2,
    UnsupportedOpcode = 3,
    MalformedRequest = 4,
    IllegalParameter = 5,
    UnknownError = 6,
    ResourceBusy = 7
}

public enum EnaAdminFeatureId : byte
{
    DeviceAttributes = 1,
    MaxQueuesNumber = 2,
    HardwareHints = 3,
    LowLatencyQueue = 4,
    ExtraPropertiesStrings = 5,
    ExtraPropertiesFlags = 6,
    MaxQueuesExtended = 7,
    RssHashFunction = 10,
    StatelessOffloadConfiguration = 11,
    RssIndirectionTableConfiguration = 12,
    Mtu = 14,
    RssHashInput = 18,
    InterruptModeration = 20,
    AenqConfiguration = 26,
    LinkConfiguration = 27,
    HostAttributesConfiguration = 28,
    PhcConfiguration = 29
}

public enum EnaAdminFeatureSelect : byte
{
    CurrentValue = 1,
    DefaultValue = 3
}

public readonly struct EnaAdminControlBuffer
{
    public EnaAdminControlBuffer(
        uint length,
        ulong physicalAddress,
        ushort reserved)
    {
        Length = length;
        PhysicalAddress = physicalAddress;
        Reserved = reserved;
    }

    public uint Length { get; }

    public ulong PhysicalAddress { get; }

    public ushort Reserved { get; }

    public uint AddressLow => (uint)PhysicalAddress;

    public ushort AddressHigh => (ushort)(PhysicalAddress >> 32);
}

public readonly struct EnaAdminAqCommonDescriptor
{
    public EnaAdminAqCommonDescriptor(
        ushort commandId,
        byte opcode,
        byte flags)
    {
        CommandId = commandId;
        Opcode = opcode;
        Flags = flags;
    }

    public ushort CommandId { get; }

    public byte Opcode { get; }

    public byte Flags { get; }

    public bool Phase => (Flags & EnaAdminDescriptors.PhaseFlag) != 0;

    public bool HasControlBuffer => (Flags & EnaAdminDescriptors.ControlDataFlag) != 0;

    public bool HasIndirectControlBuffer => (Flags & EnaAdminDescriptors.ControlDataIndirectFlag) != 0;
}

public readonly struct EnaAdminGetFeatureCommand
{
    public EnaAdminGetFeatureCommand(
        EnaAdminAqCommonDescriptor commonDescriptor,
        EnaAdminControlBuffer controlBuffer,
        byte featureFlags,
        byte featureId,
        byte featureVersion)
    {
        CommonDescriptor = commonDescriptor;
        ControlBuffer = controlBuffer;
        FeatureFlags = featureFlags;
        FeatureId = featureId;
        FeatureVersion = featureVersion;
    }

    public EnaAdminAqCommonDescriptor CommonDescriptor { get; }

    public EnaAdminControlBuffer ControlBuffer { get; }

    public byte FeatureFlags { get; }

    public byte FeatureSelect => (byte)(FeatureFlags & EnaAdminDescriptors.FeatureSelectMask);

    public byte FeatureId { get; }

    public byte FeatureVersion { get; }

    public static EnaAdminGetFeatureCommand Create(
        ushort commandId,
        bool phase,
        EnaAdminFeatureId featureId,
        byte featureVersion,
        EnaAdminFeatureSelect featureSelect,
        EnaAdminControlBuffer controlBuffer,
        bool controlBufferAddressValid,
        bool controlBufferIndirect)
    {
        byte flags = phase ? EnaAdminDescriptors.PhaseFlag : (byte)0;
        if (controlBufferAddressValid)
        {
            flags |= EnaAdminDescriptors.ControlDataFlag;
        }

        if (controlBufferIndirect)
        {
            flags |= EnaAdminDescriptors.ControlDataIndirectFlag;
        }

        return new EnaAdminGetFeatureCommand(
            new EnaAdminAqCommonDescriptor(
                commandId,
                (byte)EnaAdminOpcode.GetFeature,
                flags),
            controlBuffer,
            (byte)featureSelect,
            (byte)featureId,
            featureVersion);
    }

    public static EnaAdminGetFeatureCommand CreateDeviceAttributes(
        ushort commandId,
        bool phase)
    {
        return Create(
            commandId,
            phase,
            EnaAdminFeatureId.DeviceAttributes,
            featureVersion: 0,
            EnaAdminFeatureSelect.CurrentValue,
            default,
            controlBufferAddressValid: false,
            controlBufferIndirect: false);
    }
}

public readonly struct EnaAdminAcqCommonDescriptor
{
    public EnaAdminAcqCommonDescriptor(
        ushort rawCommand,
        byte status,
        byte flags,
        ushort extendedStatus,
        ushort submissionQueueHeadIndex)
    {
        RawCommand = rawCommand;
        Status = status;
        Flags = flags;
        ExtendedStatus = extendedStatus;
        SubmissionQueueHeadIndex = submissionQueueHeadIndex;
    }

    public ushort RawCommand { get; }

    public ushort CommandId => (ushort)(RawCommand & EnaAdminDescriptors.CommandIdMask);

    public byte Status { get; }

    public EnaAdminCompletionStatus CompletionStatus => (EnaAdminCompletionStatus)Status;

    public byte Flags { get; }

    public bool Phase => (Flags & EnaAdminDescriptors.PhaseFlag) != 0;

    public ushort ExtendedStatus { get; }

    public ushort SubmissionQueueHeadIndex { get; }
}

public readonly struct EnaAdminAcqEntry
{
    internal EnaAdminAcqEntry(EnaAdminAcqCommonDescriptor commonDescriptor)
    {
        IsInitialized = true;
        CommonDescriptor = commonDescriptor;
    }

    public bool IsInitialized { get; }

    public EnaAdminAcqCommonDescriptor CommonDescriptor { get; }
}

public static class EnaAdminDescriptors
{
    public const int AdminSubmissionDescriptorByteLength = 64;
    public const int AdminCompletionDescriptorByteLength = 64;
    public const int AqCommonDescriptorByteLength = 4;
    public const int AcqCommonDescriptorByteLength = 8;
    public const int ControlBufferInfoByteLength = 12;
    public const int GetSetFeatureCommonDescriptorByteLength = 4;
    public const int GetFeatureCommandByteLength = AdminSubmissionDescriptorByteLength;
    public const int GetFeatureResponseByteLength = AdminCompletionDescriptorByteLength;

    public const ushort CommandIdMask = 0x0FFF;
    public const byte PhaseFlag = 0x01;
    public const byte ControlDataFlag = 0x02;
    public const byte ControlDataIndirectFlag = 0x04;
    public const byte FeatureSelectMask = 0x03;
    public const ulong MaximumPhysicalAddress = 0x0000_FFFF_FFFF_FFFFUL;

    public const bool AllocatesDmaMemory = false;
    public const bool PerformsMmio = false;
    public const bool RingsDoorbell = false;
    public const bool ExecutesAdminCommand = false;

    private const int AqCommandIdOffset = 0;
    private const int AqOpcodeOffset = 2;
    private const int AqFlagsOffset = 3;
    private const int ControlBufferLengthOffset = 4;
    private const int ControlBufferAddressLowOffset = 8;
    private const int ControlBufferAddressHighOffset = 12;
    private const int ControlBufferReservedOffset = 14;
    private const int FeatureFlagsOffset = 16;
    private const int FeatureIdOffset = 17;
    private const int FeatureVersionOffset = 18;
    private const int AcqCommandOffset = 0;
    private const int AcqStatusOffset = 2;
    private const int AcqFlagsOffset = 3;
    private const int AcqExtendedStatusOffset = 4;
    private const int AcqSubmissionQueueHeadIndexOffset = 6;

    public static EnaAdminDescriptorStatus TryWriteGetFeatureCommand(
        Span<byte> destination,
        EnaAdminGetFeatureCommand command,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (destination.Length < GetFeatureCommandByteLength)
        {
            return EnaAdminDescriptorStatus.BufferTooSmall;
        }

        if (command.CommonDescriptor.CommandId > CommandIdMask)
        {
            return EnaAdminDescriptorStatus.CommandIdOutOfRange;
        }

        if (command.ControlBuffer.PhysicalAddress > MaximumPhysicalAddress)
        {
            return EnaAdminDescriptorStatus.PhysicalAddressOutOfRange;
        }

        Span<byte> descriptor = destination.Slice(0, GetFeatureCommandByteLength);
        descriptor.Clear();

        EndianPrimitives.WriteUInt16LittleEndian(
            descriptor.Slice(AqCommandIdOffset, sizeof(ushort)),
            command.CommonDescriptor.CommandId);
        descriptor[AqOpcodeOffset] = command.CommonDescriptor.Opcode;
        descriptor[AqFlagsOffset] = command.CommonDescriptor.Flags;

        EndianPrimitives.WriteUInt32LittleEndian(
            descriptor.Slice(ControlBufferLengthOffset, sizeof(uint)),
            command.ControlBuffer.Length);
        EndianPrimitives.WriteUInt32LittleEndian(
            descriptor.Slice(ControlBufferAddressLowOffset, sizeof(uint)),
            command.ControlBuffer.AddressLow);
        EndianPrimitives.WriteUInt16LittleEndian(
            descriptor.Slice(ControlBufferAddressHighOffset, sizeof(ushort)),
            command.ControlBuffer.AddressHigh);
        EndianPrimitives.WriteUInt16LittleEndian(
            descriptor.Slice(ControlBufferReservedOffset, sizeof(ushort)),
            command.ControlBuffer.Reserved);

        descriptor[FeatureFlagsOffset] =
            (byte)(command.FeatureFlags & FeatureSelectMask);
        descriptor[FeatureIdOffset] = command.FeatureId;
        descriptor[FeatureVersionOffset] = command.FeatureVersion;

        bytesWritten = GetFeatureCommandByteLength;
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus TryReadGetFeatureCommand(
        ReadOnlySpan<byte> source,
        out EnaAdminGetFeatureCommand command)
    {
        command = default;

        if (source.Length < GetFeatureCommandByteLength)
        {
            return EnaAdminDescriptorStatus.BufferTooSmall;
        }

        ushort commandId = (ushort)(EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice(AqCommandIdOffset, sizeof(ushort))) & CommandIdMask);
        byte opcode = source[AqOpcodeOffset];
        byte flags = source[AqFlagsOffset];
        uint controlBufferLength = EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice(ControlBufferLengthOffset, sizeof(uint)));
        uint addressLow = EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice(ControlBufferAddressLowOffset, sizeof(uint)));
        ushort addressHigh = EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice(ControlBufferAddressHighOffset, sizeof(ushort)));
        ushort reserved = EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice(ControlBufferReservedOffset, sizeof(ushort)));

        ulong physicalAddress = (ulong)addressLow | ((ulong)addressHigh << 32);
        command = new EnaAdminGetFeatureCommand(
            new EnaAdminAqCommonDescriptor(commandId, opcode, flags),
            new EnaAdminControlBuffer(
                controlBufferLength,
                physicalAddress,
                reserved),
            source[FeatureFlagsOffset],
            source[FeatureIdOffset],
            source[FeatureVersionOffset]);
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus TryReadCompletionEntry(
        ReadOnlySpan<byte> source,
        out EnaAdminAcqEntry entry)
    {
        entry = default;

        if (source.Length < AdminCompletionDescriptorByteLength)
        {
            return EnaAdminDescriptorStatus.BufferTooSmall;
        }

        EnaAdminAcqCommonDescriptor commonDescriptor =
            new EnaAdminAcqCommonDescriptor(
                EndianPrimitives.ReadUInt16LittleEndian(
                    source.Slice(AcqCommandOffset, sizeof(ushort))),
                source[AcqStatusOffset],
                source[AcqFlagsOffset],
                EndianPrimitives.ReadUInt16LittleEndian(
                    source.Slice(AcqExtendedStatusOffset, sizeof(ushort))),
                EndianPrimitives.ReadUInt16LittleEndian(
                    source.Slice(AcqSubmissionQueueHeadIndexOffset, sizeof(ushort))));

        entry = new EnaAdminAcqEntry(commonDescriptor);
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus ValidateSuccessfulCompletion(
        EnaAdminAcqEntry entry,
        ushort expectedCommandId,
        bool expectedPhase)
    {
        if (expectedCommandId > CommandIdMask)
        {
            return EnaAdminDescriptorStatus.CommandIdOutOfRange;
        }

        if (!entry.IsInitialized)
        {
            return EnaAdminDescriptorStatus.CompletionEntryNotInitialized;
        }

        EnaAdminAcqCommonDescriptor common = entry.CommonDescriptor;
        if (common.CommandId != expectedCommandId)
        {
            return EnaAdminDescriptorStatus.CommandIdMismatch;
        }

        if (common.Phase != expectedPhase)
        {
            return EnaAdminDescriptorStatus.PhaseMismatch;
        }

        if (common.Status != (byte)EnaAdminCompletionStatus.Success)
        {
            return EnaAdminDescriptorStatus.CompletionStatusNotSuccess;
        }

        return EnaAdminDescriptorStatus.Success;
    }
}
