namespace OS.Drivers;
using OS.Std.Binary;

public static class EnaDataQueueDescriptors
{
    public const int TransmitDescriptorByteLength = 16;
    public const int ReceiveDescriptorByteLength = 16;
    public const int TransmitCompletionDescriptorByteLength = 8;
    public const int ReceiveCompletionBaseDescriptorByteLength = 16;
    public const uint MaximumTransmitBufferLength = ushort.MaxValue;
    public const uint RxEncodedZeroLengthBytes = 64u * 1024u;
    public const uint MaximumReceiveBufferLength = RxEncodedZeroLengthBytes;
    public const ulong MaximumPhysicalAddress = 0x0000_FFFF_FFFF_FFFFUL;
    public const int TxRequestIdLowBitCount = 10;
    public const ushort TxRequestIdLowMask = 0x03FF;
    public const uint TxDescriptorLengthMask = 0x0000_FFFFU;
    public const uint TxDescriptorRequestIdHighMask = 0x003F_0000U;
    public const uint TxDescriptorMetaDescriptorFlag = 1U << 23;
    public const uint TxDescriptorPhaseFlag = 1U << 24;
    public const uint TxDescriptorFirstFlag = 1U << 26;
    public const uint TxDescriptorLastFlag = 1U << 27;
    public const uint TxDescriptorCompletionRequestedFlag = 1U << 28;
    public const uint TxDescriptorReservedControlMask = (1U << 22) | TxDescriptorMetaDescriptorFlag | (1U << 25) | (7U << 29);
    public const uint TxDescriptorRequestIdLowMask = 0xFFC0_0000U;
    public const uint TxDescriptorAddressHighMask = 0x0000_FFFFU;
    public const uint TxDescriptorReservedAddressHighMask = 0x00FF_0000U;
    public const int TxDescriptorHeaderLengthShift = 24;
    public const byte RxDescriptorPhaseFlag = 1 << 0;
    public const byte RxDescriptorFirstFlag = 1 << 2;
    public const byte RxDescriptorLastFlag = 1 << 3;
    public const byte RxDescriptorCompletionRequestedFlag = 1 << 4;
    public const byte RxDescriptorMustBeOneFlag = 1 << 5;
    public const byte RxDescriptorReservedControlMask = (1 << 1) | (3 << 6);
    public const byte CompletionPhaseFlag = 1 << 0;
    public const uint RxCompletionL3ProtocolMask = 0x0000_001FU;
    public const int RxCompletionSourceVlanCountShift = 5;
    public const uint RxCompletionSourceVlanCountMask = 0x0000_0060U;
    public const int RxCompletionL4ProtocolShift = 8;
    public const uint RxCompletionL4ProtocolMask = 0x0000_1F00U;
    public const uint RxCompletionL3ChecksumErrorFlag = 1U << 13;
    public const uint RxCompletionL4ChecksumErrorFlag = 1U << 14;
    public const uint RxCompletionIpv4FragmentFlag = 1U << 15;
    public const uint RxCompletionL4ChecksumCheckedFlag = 1U << 16;
    public const uint RxCompletionPhaseFlag = 1U << 24;
    public const uint RxCompletionL3Checksum2Flag = 1U << 25;
    public const uint RxCompletionFirstFlag = 1U << 26;
    public const uint RxCompletionLastFlag = 1U << 27;
    public const uint RxCompletionBufferFlag = 1U << 30;
    public const uint RxCompletionReservedStatusMask = (1U << 7) | (0x7FU << 17) | (3U << 28) | (1U << 31);
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsMmio = false;
    public const bool RingsDoorbell = false;
    public const bool ConfiguresInterrupts = false;
    public const bool PerformsPacketIo = false;
    private const int TxLengthControlOffset = 0;
    private const int TxMetaControlOffset = 4;
    private const int TxAddressLowOffset = 8;
    private const int TxAddressHighHeaderLengthOffset = 12;
    private const int RxLengthOffset = 0;
    private const int RxReserved2Offset = 2;
    private const int RxControlOffset = 3;
    private const int RxRequestIdOffset = 4;
    private const int RxReserved6Offset = 6;
    private const int RxAddressLowOffset = 8;
    private const int RxAddressHighOffset = 12;
    private const int RxReserved16Offset = 14;
    private const int TxCompletionRequestIdOffset = 0;
    private const int TxCompletionStatusOffset = 2;
    private const int TxCompletionFlagsOffset = 3;
    private const int TxCompletionSubmissionQueueIdOffset = 4;
    private const int TxCompletionSubmissionQueueHeadIndexOffset = 6;
    private const int RxCompletionStatusOffset = 0;
    private const int RxCompletionLengthOffset = 4;
    private const int RxCompletionRequestIdOffset = 6;
    private const int RxCompletionHashOffset = 8;
    private const int RxCompletionSubmissionQueueIdOffset = 12;
    private const int RxCompletionOffsetOffset = 14;
    private const int RxCompletionReservedOffset = 15;
    public static EnaDataQueueDescriptorStatus TryWriteTransmitDescriptor(Span<byte> destination, EnaDataQueueTransmitDescriptor descriptor, out int bytesWritten)
    {
        bytesWritten = 0;
        EnaDataQueueDescriptorStatus validationStatus = ValidateTransmitDescriptor(descriptor);
        if (validationStatus != EnaDataQueueDescriptorStatus.Success)
        {
            return validationStatus;
        }

        if (destination.Length < TransmitDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        Span<byte> bytes = destination.Slice(0, TransmitDescriptorByteLength);
        bytes.Clear();
        uint lengthControl = descriptor.Length | ((uint)descriptor.RequestIdHigh << 16);
        if (descriptor.Phase)
        {
            lengthControl |= TxDescriptorPhaseFlag;
        }

        if (descriptor.First)
        {
            lengthControl |= TxDescriptorFirstFlag;
        }

        if (descriptor.Last)
        {
            lengthControl |= TxDescriptorLastFlag;
        }

        if (descriptor.CompletionRequested)
        {
            lengthControl |= TxDescriptorCompletionRequestedFlag;
        }

        uint metaControl = (uint)descriptor.RequestIdLow << 22;
        uint addressHighHeaderLength = descriptor.AddressHigh | ((uint)descriptor.HeaderLength << TxDescriptorHeaderLengthShift);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.Slice(TxLengthControlOffset, sizeof(uint)), lengthControl);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.Slice(TxMetaControlOffset, sizeof(uint)), metaControl);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.Slice(TxAddressLowOffset, sizeof(uint)), descriptor.AddressLow);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.Slice(TxAddressHighHeaderLengthOffset, sizeof(uint)), addressHighHeaderLength);
        bytesWritten = TransmitDescriptorByteLength;
        return EnaDataQueueDescriptorStatus.Success;
    }

    public static EnaDataQueueDescriptorStatus TryReadTransmitDescriptor(ReadOnlySpan<byte> source, out EnaDataQueueTransmitDescriptor descriptor)
    {
        descriptor = default;
        if (source.Length < TransmitDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        uint lengthControl = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(TxLengthControlOffset, sizeof(uint)));
        uint metaControl = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(TxMetaControlOffset, sizeof(uint)));
        uint addressLow = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(TxAddressLowOffset, sizeof(uint)));
        uint addressHighHeaderLength = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(TxAddressHighHeaderLengthOffset, sizeof(uint)));
        if ((lengthControl & TxDescriptorReservedControlMask) != 0 || (addressHighHeaderLength & TxDescriptorReservedAddressHighMask) != 0)
        {
            return EnaDataQueueDescriptorStatus.ReservedBitsSet;
        }

        if ((metaControl & ~TxDescriptorRequestIdLowMask) != 0)
        {
            return EnaDataQueueDescriptorStatus.UnsupportedOffloadMetadata;
        }

        uint length = lengthControl & TxDescriptorLengthMask;
        ushort requestIdLow = (ushort)((metaControl & TxDescriptorRequestIdLowMask) >> 22);
        ushort requestIdHigh = (ushort)((lengthControl & TxDescriptorRequestIdHighMask) >> 16);
        ushort requestId = (ushort)(requestIdLow | (requestIdHigh << TxRequestIdLowBitCount));
        ulong physicalAddress = (ulong)addressLow | ((ulong)(addressHighHeaderLength & TxDescriptorAddressHighMask) << 32);
        descriptor = new EnaDataQueueTransmitDescriptor(requestId, length, physicalAddress, (lengthControl & TxDescriptorPhaseFlag) != 0, (lengthControl & TxDescriptorFirstFlag) != 0, (lengthControl & TxDescriptorLastFlag) != 0, (lengthControl & TxDescriptorCompletionRequestedFlag) != 0, (byte)(addressHighHeaderLength >> TxDescriptorHeaderLengthShift));
        return ValidateTransmitDescriptor(descriptor);
    }

    public static EnaDataQueueDescriptorStatus TryWriteReceiveDescriptor(Span<byte> destination, EnaDataQueueReceiveDescriptor descriptor, out int bytesWritten)
    {
        bytesWritten = 0;
        EnaDataQueueDescriptorStatus validationStatus = ValidateReceiveDescriptor(descriptor);
        if (validationStatus != EnaDataQueueDescriptorStatus.Success)
        {
            return validationStatus;
        }

        if (destination.Length < ReceiveDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        Span<byte> bytes = destination.Slice(0, ReceiveDescriptorByteLength);
        bytes.Clear();
        byte control = RxDescriptorMustBeOneFlag;
        if (descriptor.Phase)
        {
            control |= RxDescriptorPhaseFlag;
        }

        if (descriptor.First)
        {
            control |= RxDescriptorFirstFlag;
        }

        if (descriptor.Last)
        {
            control |= RxDescriptorLastFlag;
        }

        if (descriptor.CompletionRequested)
        {
            control |= RxDescriptorCompletionRequestedFlag;
        }

        EndianPrimitives.WriteUInt16LittleEndian(bytes.Slice(RxLengthOffset, sizeof(ushort)), descriptor.EncodedLength);
        bytes[RxControlOffset] = control;
        EndianPrimitives.WriteUInt16LittleEndian(bytes.Slice(RxRequestIdOffset, sizeof(ushort)), descriptor.RequestId);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.Slice(RxAddressLowOffset, sizeof(uint)), descriptor.AddressLow);
        EndianPrimitives.WriteUInt16LittleEndian(bytes.Slice(RxAddressHighOffset, sizeof(ushort)), descriptor.AddressHigh);
        bytesWritten = ReceiveDescriptorByteLength;
        return EnaDataQueueDescriptorStatus.Success;
    }

    public static EnaDataQueueDescriptorStatus TryReadReceiveDescriptor(ReadOnlySpan<byte> source, out EnaDataQueueReceiveDescriptor descriptor)
    {
        descriptor = default;
        if (source.Length < ReceiveDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        byte reserved2 = source[RxReserved2Offset];
        byte control = source[RxControlOffset];
        ushort reserved6 = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxReserved6Offset, sizeof(ushort)));
        ushort reserved16 = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxReserved16Offset, sizeof(ushort)));
        if (reserved2 != 0 || reserved6 != 0 || reserved16 != 0 || (control & RxDescriptorReservedControlMask) != 0)
        {
            return EnaDataQueueDescriptorStatus.ReservedBitsSet;
        }

        if ((control & RxDescriptorMustBeOneFlag) == 0)
        {
            return EnaDataQueueDescriptorStatus.MissingRequiredControlBit;
        }

        ushort encodedLength = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxLengthOffset, sizeof(ushort)));
        uint bufferLength = encodedLength == 0 ? RxEncodedZeroLengthBytes : encodedLength;
        uint addressLow = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(RxAddressLowOffset, sizeof(uint)));
        ushort addressHigh = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxAddressHighOffset, sizeof(ushort)));
        descriptor = new EnaDataQueueReceiveDescriptor(EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxRequestIdOffset, sizeof(ushort))), bufferLength, (ulong)addressLow | ((ulong)addressHigh << 32), (control & RxDescriptorPhaseFlag) != 0, (control & RxDescriptorFirstFlag) != 0, (control & RxDescriptorLastFlag) != 0, (control & RxDescriptorCompletionRequestedFlag) != 0);
        return ValidateReceiveDescriptor(descriptor);
    }

    public static EnaDataQueueDescriptorStatus TryReadTransmitCompletion(ReadOnlySpan<byte> source, out EnaDataQueueTransmitCompletion completion)
    {
        completion = default;
        if (source.Length < TransmitCompletionDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        byte flags = source[TxCompletionFlagsOffset];
        if ((flags & ~CompletionPhaseFlag) != 0)
        {
            return EnaDataQueueDescriptorStatus.ReservedBitsSet;
        }

        completion = new EnaDataQueueTransmitCompletion(EndianPrimitives.ReadUInt16LittleEndian(source.Slice(TxCompletionRequestIdOffset, sizeof(ushort))), source[TxCompletionStatusOffset], flags, EndianPrimitives.ReadUInt16LittleEndian(source.Slice(TxCompletionSubmissionQueueIdOffset, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(TxCompletionSubmissionQueueHeadIndexOffset, sizeof(ushort))));
        return EnaDataQueueDescriptorStatus.Success;
    }

    public static EnaDataQueueDescriptorStatus ValidateTransmitCompletion(EnaDataQueueTransmitCompletion completion, ushort expectedRequestId, bool expectedPhase, ushort expectedSubmissionQueueId)
    {
        if (!completion.IsInitialized)
        {
            return EnaDataQueueDescriptorStatus.CompletionNotInitialized;
        }

        if (completion.RequestId != expectedRequestId)
        {
            return EnaDataQueueDescriptorStatus.RequestIdMismatch;
        }

        if (completion.Phase != expectedPhase)
        {
            return EnaDataQueueDescriptorStatus.PhaseMismatch;
        }

        if (completion.SubmissionQueueId != expectedSubmissionQueueId)
        {
            return EnaDataQueueDescriptorStatus.QueueIdMismatch;
        }

        if (completion.Status != 0)
        {
            return EnaDataQueueDescriptorStatus.CompletionStatusNotSuccess;
        }

        return EnaDataQueueDescriptorStatus.Success;
    }

    public static EnaDataQueueDescriptorStatus TryReadReceiveCompletion(ReadOnlySpan<byte> source, out EnaDataQueueReceiveCompletion completion)
    {
        completion = default;
        if (source.Length < ReceiveCompletionBaseDescriptorByteLength)
        {
            return EnaDataQueueDescriptorStatus.BufferTooSmall;
        }

        uint status = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(RxCompletionStatusOffset, sizeof(uint)));
        if ((status & RxCompletionReservedStatusMask) != 0 || source[RxCompletionReservedOffset] != 0)
        {
            return EnaDataQueueDescriptorStatus.ReservedBitsSet;
        }

        completion = new EnaDataQueueReceiveCompletion(status, EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxCompletionLengthOffset, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxCompletionRequestIdOffset, sizeof(ushort))), EndianPrimitives.ReadUInt32LittleEndian(source.Slice(RxCompletionHashOffset, sizeof(uint))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(RxCompletionSubmissionQueueIdOffset, sizeof(ushort))), source[RxCompletionOffsetOffset]);
        return EnaDataQueueDescriptorStatus.Success;
    }

    public static EnaDataQueueDescriptorStatus ValidateReceiveCompletion(EnaDataQueueReceiveCompletion completion, ushort expectedRequestId, bool expectedPhase, ushort expectedSubmissionQueueId)
    {
        if (completion.RequestId != expectedRequestId)
        {
            return EnaDataQueueDescriptorStatus.RequestIdMismatch;
        }

        if (completion.Phase != expectedPhase)
        {
            return EnaDataQueueDescriptorStatus.PhaseMismatch;
        }

        if (completion.SubmissionQueueId != expectedSubmissionQueueId)
        {
            return EnaDataQueueDescriptorStatus.QueueIdMismatch;
        }

        if (!completion.Buffer)
        {
            return EnaDataQueueDescriptorStatus.CompletionNotBuffer;
        }

        if (!completion.First || !completion.Last)
        {
            return EnaDataQueueDescriptorStatus.CompletionNotSingleBuffer;
        }

        if (completion.Length == 0)
        {
            return EnaDataQueueDescriptorStatus.InvalidLength;
        }

        return EnaDataQueueDescriptorStatus.Success;
    }

    private static EnaDataQueueDescriptorStatus ValidateTransmitDescriptor(EnaDataQueueTransmitDescriptor descriptor)
    {
        if (descriptor.Length == 0 || descriptor.Length > MaximumTransmitBufferLength)
        {
            return EnaDataQueueDescriptorStatus.InvalidLength;
        }

        if (descriptor.PhysicalAddress > MaximumPhysicalAddress)
        {
            return EnaDataQueueDescriptorStatus.PhysicalAddressOutOfRange;
        }

        if (descriptor.CompletionRequested && !descriptor.First)
        {
            return EnaDataQueueDescriptorStatus.CompletionRequestWithoutFirstDescriptor;
        }

        return EnaDataQueueDescriptorStatus.Success;
    }

    private static EnaDataQueueDescriptorStatus ValidateReceiveDescriptor(EnaDataQueueReceiveDescriptor descriptor)
    {
        if (descriptor.BufferLength == 0 || descriptor.BufferLength > MaximumReceiveBufferLength)
        {
            return EnaDataQueueDescriptorStatus.InvalidLength;
        }

        if (descriptor.PhysicalAddress > MaximumPhysicalAddress)
        {
            return EnaDataQueueDescriptorStatus.PhysicalAddressOutOfRange;
        }

        if (descriptor.CompletionRequested && !descriptor.First)
        {
            return EnaDataQueueDescriptorStatus.CompletionRequestWithoutFirstDescriptor;
        }

        return EnaDataQueueDescriptorStatus.Success;
    }
}
