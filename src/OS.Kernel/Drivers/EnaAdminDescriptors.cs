namespace OS.Drivers;

using OS.Std.Binary;

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
    public static EnaAdminDescriptorStatus TryWriteGetFeatureCommand(Span<byte> destination, EnaAdminGetFeatureCommand command, out int bytesWritten)
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
        EndianPrimitives.WriteUInt16LittleEndian(descriptor.Slice(AqCommandIdOffset, sizeof(ushort)), command.CommonDescriptor.CommandId);
        descriptor[AqOpcodeOffset] = command.CommonDescriptor.Opcode;
        descriptor[AqFlagsOffset] = command.CommonDescriptor.Flags;
        EndianPrimitives.WriteUInt32LittleEndian(descriptor.Slice(ControlBufferLengthOffset, sizeof(uint)), command.ControlBuffer.Length);
        EndianPrimitives.WriteUInt32LittleEndian(descriptor.Slice(ControlBufferAddressLowOffset, sizeof(uint)), command.ControlBuffer.AddressLow);
        EndianPrimitives.WriteUInt16LittleEndian(descriptor.Slice(ControlBufferAddressHighOffset, sizeof(ushort)), command.ControlBuffer.AddressHigh);
        EndianPrimitives.WriteUInt16LittleEndian(descriptor.Slice(ControlBufferReservedOffset, sizeof(ushort)), command.ControlBuffer.Reserved);
        descriptor[FeatureFlagsOffset] = (byte)(command.FeatureFlags & FeatureSelectMask);
        descriptor[FeatureIdOffset] = command.FeatureId;
        descriptor[FeatureVersionOffset] = command.FeatureVersion;
        bytesWritten = GetFeatureCommandByteLength;
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus TryReadGetFeatureCommand(ReadOnlySpan<byte> source, out EnaAdminGetFeatureCommand command)
    {
        command = default;
        if (source.Length < GetFeatureCommandByteLength)
        {
            return EnaAdminDescriptorStatus.BufferTooSmall;
        }

        ushort commandId = (ushort)(EndianPrimitives.ReadUInt16LittleEndian(source.Slice(AqCommandIdOffset, sizeof(ushort))) & CommandIdMask);
        byte opcode = source[AqOpcodeOffset];
        byte flags = source[AqFlagsOffset];
        uint controlBufferLength = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(ControlBufferLengthOffset, sizeof(uint)));
        uint addressLow = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(ControlBufferAddressLowOffset, sizeof(uint)));
        ushort addressHigh = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(ControlBufferAddressHighOffset, sizeof(ushort)));
        ushort reserved = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(ControlBufferReservedOffset, sizeof(ushort)));
        ulong physicalAddress = (ulong)addressLow | ((ulong)addressHigh << 32);
        command = new EnaAdminGetFeatureCommand(new EnaAdminAqCommonDescriptor(commandId, opcode, flags), new EnaAdminControlBuffer(controlBufferLength, physicalAddress, reserved), source[FeatureFlagsOffset], source[FeatureIdOffset], source[FeatureVersionOffset]);
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus TryReadCompletionEntry(ReadOnlySpan<byte> source, out EnaAdminAcqEntry entry)
    {
        entry = default;
        if (source.Length < AdminCompletionDescriptorByteLength)
        {
            return EnaAdminDescriptorStatus.BufferTooSmall;
        }

        EnaAdminAcqCommonDescriptor commonDescriptor = new EnaAdminAcqCommonDescriptor(EndianPrimitives.ReadUInt16LittleEndian(source.Slice(AcqCommandOffset, sizeof(ushort))), source[AcqStatusOffset], source[AcqFlagsOffset], EndianPrimitives.ReadUInt16LittleEndian(source.Slice(AcqExtendedStatusOffset, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(AcqSubmissionQueueHeadIndexOffset, sizeof(ushort))));
        entry = new EnaAdminAcqEntry(commonDescriptor);
        return EnaAdminDescriptorStatus.Success;
    }

    public static EnaAdminDescriptorStatus ValidateSuccessfulCompletion(EnaAdminAcqEntry entry, ushort expectedCommandId, bool expectedPhase)
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
