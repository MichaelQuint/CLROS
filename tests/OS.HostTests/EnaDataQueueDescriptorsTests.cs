using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class EnaDataQueueDescriptorsTests
{
    private const ulong TxPhysicalAddress = 0x0000_1234_5678_9ABCUL;
    private const ulong RxPhysicalAddress = 0x0000_4321_8765_1000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.data.desc writes tx descriptor", TestWritesTxDescriptor, ref failures);
        Run("drivers.ena.data.desc writes rx descriptor", TestWritesRxDescriptor, ref failures);
        Run("drivers.ena.data.desc parses completions", TestParsesCompletions, ref failures);
        Run("drivers.ena.data.desc rejects malformed buffers", TestRejectsMalformedBuffers, ref failures);
        Run("drivers.ena.data.desc rejects completion failures", TestRejectsCompletionFailures, ref failures);
        Run("drivers.ena.data.desc rejects default tx completion", TestRejectsDefaultTxCompletion, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA data queue descriptor host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestWritesTxDescriptor()
    {
        byte[] descriptorBytes =
            new byte[EnaDataQueueDescriptors.TransmitDescriptorByteLength + 2];
        Array.Fill(descriptorBytes, (byte)0xcc);

        EnaDataQueueTransmitDescriptor descriptor =
            new EnaDataQueueTransmitDescriptor(
                requestId: 0xabcd,
                length: 1514,
                physicalAddress: TxPhysicalAddress,
                phase: true,
                first: true,
                last: true,
                completionRequested: true,
                headerLength: 0);

        EnaDataQueueDescriptorStatus writeStatus =
            EnaDataQueueDescriptors.TryWriteTransmitDescriptor(
                descriptorBytes,
                descriptor,
                out int bytesWritten);
        EnaDataQueueDescriptorStatus readStatus =
            EnaDataQueueDescriptors.TryReadTransmitDescriptor(
                descriptorBytes.AsSpan(
                    0,
                    EnaDataQueueDescriptors.TransmitDescriptorByteLength),
                out EnaDataQueueTransmitDescriptor parsed);

        uint lengthControl = ReadUInt32(descriptorBytes, 0);
        uint metaControl = ReadUInt32(descriptorBytes, 4);
        uint addressLow = ReadUInt32(descriptorBytes, 8);
        uint addressHighHeaderLength = ReadUInt32(descriptorBytes, 12);

        return writeStatus == EnaDataQueueDescriptorStatus.Success
            && readStatus == EnaDataQueueDescriptorStatus.Success
            && bytesWritten == EnaDataQueueDescriptors.TransmitDescriptorByteLength
            && (lengthControl & EnaDataQueueDescriptors.TxDescriptorLengthMask) == 1514
            && ((lengthControl & EnaDataQueueDescriptors.TxDescriptorRequestIdHighMask) >> 16) ==
                (uint)(0xabcd >> EnaDataQueueDescriptors.TxRequestIdLowBitCount)
            && (lengthControl & EnaDataQueueDescriptors.TxDescriptorPhaseFlag) != 0
            && (lengthControl & EnaDataQueueDescriptors.TxDescriptorFirstFlag) != 0
            && (lengthControl & EnaDataQueueDescriptors.TxDescriptorLastFlag) != 0
            && (lengthControl & EnaDataQueueDescriptors.TxDescriptorCompletionRequestedFlag) != 0
            && (metaControl >> 22) == (0xabcd & EnaDataQueueDescriptors.TxRequestIdLowMask)
            && addressLow == (uint)(TxPhysicalAddress & 0xFFFF_FFFFUL)
            && (addressHighHeaderLength & EnaDataQueueDescriptors.TxDescriptorAddressHighMask) ==
                (uint)(TxPhysicalAddress >> 32)
            && (addressHighHeaderLength >> EnaDataQueueDescriptors.TxDescriptorHeaderLengthShift) == 0
            && descriptorBytes[EnaDataQueueDescriptors.TransmitDescriptorByteLength] == 0xcc
            && descriptorBytes[EnaDataQueueDescriptors.TransmitDescriptorByteLength + 1] == 0xcc
            && parsed.RequestId == 0xabcd
            && parsed.Length == 1514
            && parsed.PhysicalAddress == TxPhysicalAddress
            && parsed.Phase
            && parsed.First
            && parsed.Last
            && parsed.CompletionRequested
            && parsed.HeaderLength == 0
            && !EnaDataQueueDescriptors.AllocatesDmaMemory
            && !EnaDataQueueDescriptors.PerformsMmio
            && !EnaDataQueueDescriptors.RingsDoorbell
            && !EnaDataQueueDescriptors.ConfiguresInterrupts
            && !EnaDataQueueDescriptors.PerformsPacketIo;
    }

    private static bool TestWritesRxDescriptor()
    {
        byte[] descriptorBytes =
            new byte[EnaDataQueueDescriptors.ReceiveDescriptorByteLength + 1];
        Array.Fill(descriptorBytes, (byte)0xdd);

        EnaDataQueueReceiveDescriptor descriptor =
            new EnaDataQueueReceiveDescriptor(
                requestId: 0x2233,
                bufferLength: EnaDataQueueDescriptors.RxEncodedZeroLengthBytes,
                physicalAddress: RxPhysicalAddress,
                phase: false,
                first: true,
                last: true,
                completionRequested: true);

        EnaDataQueueDescriptorStatus writeStatus =
            EnaDataQueueDescriptors.TryWriteReceiveDescriptor(
                descriptorBytes,
                descriptor,
                out int bytesWritten);
        EnaDataQueueDescriptorStatus readStatus =
            EnaDataQueueDescriptors.TryReadReceiveDescriptor(
                descriptorBytes.AsSpan(
                    0,
                    EnaDataQueueDescriptors.ReceiveDescriptorByteLength),
                out EnaDataQueueReceiveDescriptor parsed);

        byte control = descriptorBytes[3];
        return writeStatus == EnaDataQueueDescriptorStatus.Success
            && readStatus == EnaDataQueueDescriptorStatus.Success
            && bytesWritten == EnaDataQueueDescriptors.ReceiveDescriptorByteLength
            && ReadUInt16(descriptorBytes, 0) == 0
            && descriptorBytes[2] == 0
            && (control & EnaDataQueueDescriptors.RxDescriptorPhaseFlag) == 0
            && (control & EnaDataQueueDescriptors.RxDescriptorFirstFlag) != 0
            && (control & EnaDataQueueDescriptors.RxDescriptorLastFlag) != 0
            && (control & EnaDataQueueDescriptors.RxDescriptorCompletionRequestedFlag) != 0
            && (control & EnaDataQueueDescriptors.RxDescriptorMustBeOneFlag) != 0
            && ReadUInt16(descriptorBytes, 4) == 0x2233
            && ReadUInt16(descriptorBytes, 6) == 0
            && ReadUInt32(descriptorBytes, 8) == (uint)(RxPhysicalAddress & 0xFFFF_FFFFUL)
            && ReadUInt16(descriptorBytes, 12) == (ushort)(RxPhysicalAddress >> 32)
            && ReadUInt16(descriptorBytes, 14) == 0
            && descriptorBytes[EnaDataQueueDescriptors.ReceiveDescriptorByteLength] == 0xdd
            && parsed.RequestId == 0x2233
            && parsed.BufferLength == EnaDataQueueDescriptors.RxEncodedZeroLengthBytes
            && parsed.EncodedLength == 0
            && parsed.PhysicalAddress == RxPhysicalAddress
            && !parsed.Phase
            && parsed.First
            && parsed.Last
            && parsed.CompletionRequested;
    }

    private static bool TestParsesCompletions()
    {
        byte[] txCompletion = CreateTxCompletion(
            requestId: 0x1001,
            status: 0,
            flags: EnaDataQueueDescriptors.CompletionPhaseFlag,
            submissionQueueId: 3,
            submissionQueueHeadIndex: 9);
        byte[] rxCompletion = CreateRxCompletion(
            status: EnaDataQueueDescriptors.RxCompletionPhaseFlag |
                EnaDataQueueDescriptors.RxCompletionFirstFlag |
                EnaDataQueueDescriptors.RxCompletionLastFlag |
                EnaDataQueueDescriptors.RxCompletionBufferFlag |
                8U |
                (13U << EnaDataQueueDescriptors.RxCompletionL4ProtocolShift) |
                EnaDataQueueDescriptors.RxCompletionL4ChecksumCheckedFlag,
            length: 64,
            requestId: 0x1002,
            hash: 0x1122_3344,
            submissionQueueId: 4,
            offset: 2,
            reserved: 0);

        EnaDataQueueDescriptorStatus txParse =
            EnaDataQueueDescriptors.TryReadTransmitCompletion(
                txCompletion,
                out EnaDataQueueTransmitCompletion tx);
        EnaDataQueueDescriptorStatus txValidation =
            EnaDataQueueDescriptors.ValidateTransmitCompletion(
                tx,
                expectedRequestId: 0x1001,
                expectedPhase: true,
                expectedSubmissionQueueId: 3);
        EnaDataQueueDescriptorStatus rxParse =
            EnaDataQueueDescriptors.TryReadReceiveCompletion(
                rxCompletion,
                out EnaDataQueueReceiveCompletion rx);
        EnaDataQueueDescriptorStatus rxValidation =
            EnaDataQueueDescriptors.ValidateReceiveCompletion(
                rx,
                expectedRequestId: 0x1002,
                expectedPhase: true,
                expectedSubmissionQueueId: 4);

        return txParse == EnaDataQueueDescriptorStatus.Success
            && txValidation == EnaDataQueueDescriptorStatus.Success
            && tx.IsInitialized
            && tx.RequestId == 0x1001
            && tx.Status == 0
            && tx.Phase
            && tx.SubmissionQueueId == 3
            && tx.SubmissionQueueHeadIndex == 9
            && rxParse == EnaDataQueueDescriptorStatus.Success
            && rxValidation == EnaDataQueueDescriptorStatus.Success
            && rx.RequestId == 0x1002
            && rx.Phase
            && rx.First
            && rx.Last
            && rx.Buffer
            && rx.Length == 64
            && rx.Hash == 0x1122_3344
            && rx.SubmissionQueueId == 4
            && rx.Offset == 2
            && rx.L3ProtocolIndex == 8
            && rx.L4ProtocolIndex == 13
            && rx.L4ChecksumChecked
            && !rx.HasL3ChecksumError
            && !rx.HasL4ChecksumError
            && !rx.IsIpv4Fragment;
    }

    private static bool TestRejectsMalformedBuffers()
    {
        byte[] shortTx = new byte[EnaDataQueueDescriptors.TransmitDescriptorByteLength - 1];
        byte[] shortRx = new byte[EnaDataQueueDescriptors.ReceiveDescriptorByteLength - 1];
        byte[] tx = new byte[EnaDataQueueDescriptors.TransmitDescriptorByteLength];
        byte[] rx = new byte[EnaDataQueueDescriptors.ReceiveDescriptorByteLength];

        EnaDataQueueDescriptorStatus shortTxWrite =
            EnaDataQueueDescriptors.TryWriteTransmitDescriptor(
                shortTx,
                CreateTxDescriptor(),
                out int shortTxBytesWritten);
        EnaDataQueueDescriptorStatus shortRxRead =
            EnaDataQueueDescriptors.TryReadReceiveDescriptor(
                shortRx,
                out _);
        EnaDataQueueDescriptorStatus invalidTxLength =
            EnaDataQueueDescriptors.TryWriteTransmitDescriptor(
                tx,
                new EnaDataQueueTransmitDescriptor(
                    requestId: 1,
                    length: 0,
                    physicalAddress: TxPhysicalAddress,
                    phase: true,
                    first: true,
                    last: true,
                    completionRequested: true,
                    headerLength: 0),
                out _);
        EnaDataQueueDescriptorStatus invalidRxLength =
            EnaDataQueueDescriptors.TryWriteReceiveDescriptor(
                rx,
                new EnaDataQueueReceiveDescriptor(
                    requestId: 1,
                    bufferLength: EnaDataQueueDescriptors.MaximumReceiveBufferLength + 1,
                    physicalAddress: RxPhysicalAddress,
                    phase: true,
                    first: true,
                    last: true,
                    completionRequested: true),
                out _);
        EnaDataQueueDescriptorStatus invalidAddress =
            EnaDataQueueDescriptors.TryWriteTransmitDescriptor(
                tx,
                new EnaDataQueueTransmitDescriptor(
                    requestId: 1,
                    length: 1,
                    physicalAddress: EnaDataQueueDescriptors.MaximumPhysicalAddress + 1,
                    phase: true,
                    first: true,
                    last: true,
                    completionRequested: true,
                    headerLength: 0),
                out _);

        rx.AsSpan().Clear();
        EndianPrimitives.WriteUInt16LittleEndian(rx.AsSpan(0, 2), 2048);
        rx[3] = EnaDataQueueDescriptors.RxDescriptorFirstFlag;
        EnaDataQueueDescriptorStatus missingMbo =
            EnaDataQueueDescriptors.TryReadReceiveDescriptor(rx, out _);

        return shortTxWrite == EnaDataQueueDescriptorStatus.BufferTooSmall
            && shortTxBytesWritten == 0
            && shortRxRead == EnaDataQueueDescriptorStatus.BufferTooSmall
            && invalidTxLength == EnaDataQueueDescriptorStatus.InvalidLength
            && invalidRxLength == EnaDataQueueDescriptorStatus.InvalidLength
            && invalidAddress == EnaDataQueueDescriptorStatus.PhysicalAddressOutOfRange
            && missingMbo == EnaDataQueueDescriptorStatus.MissingRequiredControlBit;
    }

    private static bool TestRejectsCompletionFailures()
    {
        byte[] txStatusFailure = CreateTxCompletion(
            requestId: 0x2001,
            status: 1,
            flags: EnaDataQueueDescriptors.CompletionPhaseFlag,
            submissionQueueId: 5,
            submissionQueueHeadIndex: 1);
        byte[] txReservedFlags = CreateTxCompletion(
            requestId: 0x2001,
            status: 0,
            flags: 0x80,
            submissionQueueId: 5,
            submissionQueueHeadIndex: 1);
        byte[] rxNoBuffer = CreateRxCompletion(
            status: EnaDataQueueDescriptors.RxCompletionPhaseFlag |
                EnaDataQueueDescriptors.RxCompletionFirstFlag |
                EnaDataQueueDescriptors.RxCompletionLastFlag,
            length: 32,
            requestId: 0x2002,
            hash: 0,
            submissionQueueId: 6,
            offset: 0,
            reserved: 0);
        byte[] rxReservedStatus = CreateRxCompletion(
            status: EnaDataQueueDescriptors.RxCompletionBufferFlag |
                EnaDataQueueDescriptors.RxCompletionFirstFlag |
                EnaDataQueueDescriptors.RxCompletionLastFlag |
                (1U << 31),
            length: 32,
            requestId: 0x2002,
            hash: 0,
            submissionQueueId: 6,
            offset: 0,
            reserved: 0);

        EnaDataQueueDescriptors.TryReadTransmitCompletion(
            txStatusFailure,
            out EnaDataQueueTransmitCompletion txFailure);
        EnaDataQueueDescriptorStatus txFailureStatus =
            EnaDataQueueDescriptors.ValidateTransmitCompletion(
                txFailure,
                expectedRequestId: 0x2001,
                expectedPhase: true,
                expectedSubmissionQueueId: 5);
        EnaDataQueueDescriptors.TryReadReceiveCompletion(
            rxNoBuffer,
            out EnaDataQueueReceiveCompletion rxFailure);
        EnaDataQueueDescriptorStatus rxFailureStatus =
            EnaDataQueueDescriptors.ValidateReceiveCompletion(
                rxFailure,
                expectedRequestId: 0x2002,
                expectedPhase: true,
                expectedSubmissionQueueId: 6);

        return txFailureStatus == EnaDataQueueDescriptorStatus.CompletionStatusNotSuccess
            && EnaDataQueueDescriptors.TryReadTransmitCompletion(
                txReservedFlags,
                out _) == EnaDataQueueDescriptorStatus.ReservedBitsSet
            && rxFailureStatus == EnaDataQueueDescriptorStatus.CompletionNotBuffer
            && EnaDataQueueDescriptors.TryReadReceiveCompletion(
                rxReservedStatus,
                out _) == EnaDataQueueDescriptorStatus.ReservedBitsSet;
    }

    private static bool TestRejectsDefaultTxCompletion()
    {
        EnaDataQueueTransmitCompletion completion = default;
        EnaDataQueueDescriptorStatus status =
            EnaDataQueueDescriptors.ValidateTransmitCompletion(
                completion,
                expectedRequestId: 0,
                expectedPhase: false,
                expectedSubmissionQueueId: 0);
        byte[] zeroRequestCompletion = CreateTxCompletion(
            requestId: 0,
            status: 0,
            flags: 0,
            submissionQueueId: 0,
            submissionQueueHeadIndex: 1);
        EnaDataQueueDescriptorStatus parsedStatus =
            EnaDataQueueDescriptors.TryReadTransmitCompletion(
                zeroRequestCompletion,
                out EnaDataQueueTransmitCompletion parsed);
        EnaDataQueueDescriptorStatus parsedValidationStatus =
            EnaDataQueueDescriptors.ValidateTransmitCompletion(
                parsed,
                expectedRequestId: 0,
                expectedPhase: false,
                expectedSubmissionQueueId: 0);

        return !completion.IsInitialized
            && status == EnaDataQueueDescriptorStatus.CompletionNotInitialized
            && parsedStatus == EnaDataQueueDescriptorStatus.Success
            && parsedValidationStatus == EnaDataQueueDescriptorStatus.Success
            && parsed.IsInitialized
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaDataQueueTransmitCompletion>();
    }

    private static EnaDataQueueTransmitDescriptor CreateTxDescriptor()
    {
        return new EnaDataQueueTransmitDescriptor(
            requestId: 1,
            length: 64,
            physicalAddress: TxPhysicalAddress,
            phase: true,
            first: true,
            last: true,
            completionRequested: true,
            headerLength: 0);
    }

    private static byte[] CreateTxCompletion(
        ushort requestId,
        byte status,
        byte flags,
        ushort submissionQueueId,
        ushort submissionQueueHeadIndex)
    {
        byte[] bytes = new byte[EnaDataQueueDescriptors.TransmitCompletionDescriptorByteLength];
        EndianPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), requestId);
        bytes[2] = status;
        bytes[3] = flags;
        EndianPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), submissionQueueId);
        EndianPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(6, 2),
            submissionQueueHeadIndex);
        return bytes;
    }

    private static byte[] CreateRxCompletion(
        uint status,
        ushort length,
        ushort requestId,
        uint hash,
        ushort submissionQueueId,
        byte offset,
        byte reserved)
    {
        byte[] bytes = new byte[EnaDataQueueDescriptors.ReceiveCompletionBaseDescriptorByteLength];
        EndianPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), status);
        EndianPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), length);
        EndianPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6, 2), requestId);
        EndianPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), hash);
        EndianPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12, 2), submissionQueueId);
        bytes[14] = offset;
        bytes[15] = reserved;
        return bytes;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
    }
}
