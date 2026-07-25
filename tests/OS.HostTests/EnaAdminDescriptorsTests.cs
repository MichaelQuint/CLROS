using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class EnaAdminDescriptorsTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.admin.desc exposes descriptor size constants", TestDescriptorSizeConstants, ref failures);
        Run("drivers.ena.admin.desc writes get feature device attributes", TestWritesGetFeatureDeviceAttributes, ref failures);
        Run("drivers.ena.admin.desc parses successful completion", TestParsesSuccessfulCompletion, ref failures);
        Run("drivers.ena.admin.desc reports command id mismatch", TestReportsCommandIdMismatch, ref failures);
        Run("drivers.ena.admin.desc reports completion status", TestReportsCompletionStatus, ref failures);
        Run("drivers.ena.admin.desc rejects short buffers", TestRejectsShortBuffers, ref failures);
        Run("drivers.ena.admin.desc preserves phase status bits", TestPreservesPhaseStatusBits, ref failures);
        Run("drivers.ena.admin.desc rejects default completion entry", TestRejectsDefaultCompletionEntry, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA admin descriptor host tests failed: " + failures);
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

    private static bool TestDescriptorSizeConstants()
    {
        return EnaAdminDescriptors.AdminSubmissionDescriptorByteLength == 64
            && EnaAdminDescriptors.AdminCompletionDescriptorByteLength == 64
            && EnaAdminDescriptors.AqCommonDescriptorByteLength == 4
            && EnaAdminDescriptors.AcqCommonDescriptorByteLength == 8
            && EnaAdminDescriptors.ControlBufferInfoByteLength == 12
            && EnaAdminDescriptors.GetSetFeatureCommonDescriptorByteLength == 4
            && EnaAdminDescriptors.GetFeatureCommandByteLength == 64
            && EnaAdminDescriptors.GetFeatureResponseByteLength == 64
            && EnaAdminDescriptors.CommandIdMask == 0x0FFF
            && EnaAdminDescriptors.PhaseFlag == 0x01
            && EnaAdminDescriptors.ControlDataFlag == 0x02
            && EnaAdminDescriptors.ControlDataIndirectFlag == 0x04
            && EnaAdminDescriptors.FeatureSelectMask == 0x03
            && EnaAdminDescriptors.MaximumPhysicalAddress == 0x0000_FFFF_FFFF_FFFFUL
            && !EnaAdminDescriptors.AllocatesDmaMemory
            && !EnaAdminDescriptors.PerformsMmio
            && !EnaAdminDescriptors.RingsDoorbell
            && !EnaAdminDescriptors.ExecutesAdminCommand;
    }

    private static bool TestWritesGetFeatureDeviceAttributes()
    {
        byte[] descriptor = new byte[EnaAdminDescriptors.GetFeatureCommandByteLength + 2];
        Array.Fill(descriptor, (byte)0xCC);

        EnaAdminGetFeatureCommand command =
            EnaAdminGetFeatureCommand.CreateDeviceAttributes(
                commandId: 0x0123,
                phase: true);
        EnaAdminDescriptorStatus writeStatus =
            EnaAdminDescriptors.TryWriteGetFeatureCommand(
                descriptor,
                command,
                out int bytesWritten);
        EnaAdminDescriptorStatus readStatus =
            EnaAdminDescriptors.TryReadGetFeatureCommand(
                descriptor.AsSpan(0, EnaAdminDescriptors.GetFeatureCommandByteLength),
                out EnaAdminGetFeatureCommand parsed);

        bool reservedPayloadZero = true;
        for (int index = 19; index < EnaAdminDescriptors.GetFeatureCommandByteLength; index++)
        {
            reservedPayloadZero &= descriptor[index] == 0;
        }

        return writeStatus == EnaAdminDescriptorStatus.Success
            && readStatus == EnaAdminDescriptorStatus.Success
            && bytesWritten == EnaAdminDescriptors.GetFeatureCommandByteLength
            && descriptor[0] == 0x23
            && descriptor[1] == 0x01
            && descriptor[2] == (byte)EnaAdminOpcode.GetFeature
            && descriptor[3] == EnaAdminDescriptors.PhaseFlag
            && descriptor[4] == 0
            && descriptor[8] == 0
            && descriptor[12] == 0
            && descriptor[14] == 0
            && descriptor[16] == (byte)EnaAdminFeatureSelect.CurrentValue
            && descriptor[17] == (byte)EnaAdminFeatureId.DeviceAttributes
            && descriptor[18] == 0
            && reservedPayloadZero
            && descriptor[EnaAdminDescriptors.GetFeatureCommandByteLength] == 0xCC
            && descriptor[EnaAdminDescriptors.GetFeatureCommandByteLength + 1] == 0xCC
            && parsed.CommonDescriptor.CommandId == 0x0123
            && parsed.CommonDescriptor.Opcode == (byte)EnaAdminOpcode.GetFeature
            && parsed.CommonDescriptor.Phase
            && !parsed.CommonDescriptor.HasControlBuffer
            && !parsed.CommonDescriptor.HasIndirectControlBuffer
            && parsed.ControlBuffer.Length == 0
            && parsed.ControlBuffer.PhysicalAddress == 0
            && parsed.FeatureSelect == (byte)EnaAdminFeatureSelect.CurrentValue
            && parsed.FeatureId == (byte)EnaAdminFeatureId.DeviceAttributes
            && parsed.FeatureVersion == 0;
    }

    private static bool TestParsesSuccessfulCompletion()
    {
        byte[] completion = CreateCompletion(
            rawCommand: 0xA456,
            status: (byte)EnaAdminCompletionStatus.Success,
            flags: EnaAdminDescriptors.PhaseFlag,
            extendedStatus: 0x0012,
            submissionQueueHeadIndex: 0x0020);

        EnaAdminDescriptorStatus parseStatus =
            EnaAdminDescriptors.TryReadCompletionEntry(
                completion,
                out EnaAdminAcqEntry entry);
        EnaAdminDescriptorStatus validationStatus =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                entry,
                expectedCommandId: 0x0456,
                expectedPhase: true);

        EnaAdminAcqCommonDescriptor common = entry.CommonDescriptor;
        return parseStatus == EnaAdminDescriptorStatus.Success
            && validationStatus == EnaAdminDescriptorStatus.Success
            && entry.IsInitialized
            && common.RawCommand == 0xA456
            && common.CommandId == 0x0456
            && common.Status == (byte)EnaAdminCompletionStatus.Success
            && common.CompletionStatus == EnaAdminCompletionStatus.Success
            && common.Flags == EnaAdminDescriptors.PhaseFlag
            && common.Phase
            && common.ExtendedStatus == 0x0012
            && common.SubmissionQueueHeadIndex == 0x0020;
    }

    private static bool TestReportsCommandIdMismatch()
    {
        byte[] completion = CreateCompletion(
            rawCommand: 0x0100,
            status: (byte)EnaAdminCompletionStatus.Success,
            flags: EnaAdminDescriptors.PhaseFlag,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1);

        EnaAdminDescriptorStatus parseStatus =
            EnaAdminDescriptors.TryReadCompletionEntry(
                completion,
                out EnaAdminAcqEntry entry);
        EnaAdminDescriptorStatus validationStatus =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                entry,
                expectedCommandId: 0x0101,
                expectedPhase: true);

        return parseStatus == EnaAdminDescriptorStatus.Success
            && validationStatus == EnaAdminDescriptorStatus.CommandIdMismatch
            && entry.CommonDescriptor.CommandId == 0x0100;
    }

    private static bool TestReportsCompletionStatus()
    {
        byte[] completion = CreateCompletion(
            rawCommand: 0x0002,
            status: (byte)EnaAdminCompletionStatus.IllegalParameter,
            flags: EnaAdminDescriptors.PhaseFlag,
            extendedStatus: 0xBEEF,
            submissionQueueHeadIndex: 4);

        EnaAdminDescriptorStatus parseStatus =
            EnaAdminDescriptors.TryReadCompletionEntry(
                completion,
                out EnaAdminAcqEntry entry);
        EnaAdminDescriptorStatus validationStatus =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                entry,
                expectedCommandId: 0x0002,
                expectedPhase: true);

        return parseStatus == EnaAdminDescriptorStatus.Success
            && validationStatus == EnaAdminDescriptorStatus.CompletionStatusNotSuccess
            && entry.CommonDescriptor.Status == (byte)EnaAdminCompletionStatus.IllegalParameter
            && entry.CommonDescriptor.CompletionStatus == EnaAdminCompletionStatus.IllegalParameter
            && entry.CommonDescriptor.ExtendedStatus == 0xBEEF;
    }

    private static bool TestRejectsShortBuffers()
    {
        byte[] shortSubmission = new byte[EnaAdminDescriptors.GetFeatureCommandByteLength - 1];
        byte[] shortCompletion = new byte[EnaAdminDescriptors.AdminCompletionDescriptorByteLength - 1];

        EnaAdminDescriptorStatus shortWrite =
            EnaAdminDescriptors.TryWriteGetFeatureCommand(
                shortSubmission,
                EnaAdminGetFeatureCommand.CreateDeviceAttributes(
                    commandId: 1,
                    phase: false),
                out int bytesWritten);
        EnaAdminDescriptorStatus shortReadCommand =
            EnaAdminDescriptors.TryReadGetFeatureCommand(
                shortSubmission,
                out _);
        EnaAdminDescriptorStatus shortReadCompletion =
            EnaAdminDescriptors.TryReadCompletionEntry(
                shortCompletion,
                out _);
        EnaAdminDescriptorStatus commandIdOutOfRange =
            EnaAdminDescriptors.TryWriteGetFeatureCommand(
                new byte[EnaAdminDescriptors.GetFeatureCommandByteLength],
                EnaAdminGetFeatureCommand.CreateDeviceAttributes(
                    commandId: 0x1000,
                    phase: false),
                out _);

        EnaAdminControlBuffer invalidAddress = new EnaAdminControlBuffer(
            length: 16,
            physicalAddress: EnaAdminDescriptors.MaximumPhysicalAddress + 1,
            reserved: 0);
        EnaAdminDescriptorStatus physicalAddressOutOfRange =
            EnaAdminDescriptors.TryWriteGetFeatureCommand(
                new byte[EnaAdminDescriptors.GetFeatureCommandByteLength],
                EnaAdminGetFeatureCommand.Create(
                    commandId: 1,
                    phase: false,
                    EnaAdminFeatureId.DeviceAttributes,
                    featureVersion: 0,
                    EnaAdminFeatureSelect.CurrentValue,
                    invalidAddress,
                    controlBufferAddressValid: true,
                    controlBufferIndirect: false),
                out _);

        return shortWrite == EnaAdminDescriptorStatus.BufferTooSmall
            && bytesWritten == 0
            && shortReadCommand == EnaAdminDescriptorStatus.BufferTooSmall
            && shortReadCompletion == EnaAdminDescriptorStatus.BufferTooSmall
            && commandIdOutOfRange == EnaAdminDescriptorStatus.CommandIdOutOfRange
            && physicalAddressOutOfRange == EnaAdminDescriptorStatus.PhysicalAddressOutOfRange;
    }

    private static bool TestPreservesPhaseStatusBits()
    {
        byte[] completion = CreateCompletion(
            rawCommand: 0x0007,
            status: 0x87,
            flags: 0xA5,
            extendedStatus: 0x0102,
            submissionQueueHeadIndex: 0x0304);

        EnaAdminDescriptorStatus parseStatus =
            EnaAdminDescriptors.TryReadCompletionEntry(
                completion,
                out EnaAdminAcqEntry entry);
        EnaAdminDescriptorStatus phaseMismatch =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                entry,
                expectedCommandId: 0x0007,
                expectedPhase: false);

        return parseStatus == EnaAdminDescriptorStatus.Success
            && phaseMismatch == EnaAdminDescriptorStatus.PhaseMismatch
            && entry.CommonDescriptor.Status == 0x87
            && entry.CommonDescriptor.Flags == 0xA5
            && entry.CommonDescriptor.Phase
            && entry.CommonDescriptor.CommandId == 0x0007
            && entry.CommonDescriptor.ExtendedStatus == 0x0102
            && entry.CommonDescriptor.SubmissionQueueHeadIndex == 0x0304;
    }

    private static bool TestRejectsDefaultCompletionEntry()
    {
        EnaAdminAcqEntry entry = default;
        EnaAdminDescriptorStatus status =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                entry,
                expectedCommandId: 0,
                expectedPhase: false);

        return !entry.IsInitialized
            && status == EnaAdminDescriptorStatus.CompletionEntryNotInitialized
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaAdminAcqEntry>();
    }

    private static byte[] CreateCompletion(
        ushort rawCommand,
        byte status,
        byte flags,
        ushort extendedStatus,
        ushort submissionQueueHeadIndex)
    {
        byte[] completion = new byte[EnaAdminDescriptors.AdminCompletionDescriptorByteLength];
        EndianPrimitives.WriteUInt16LittleEndian(
            completion.AsSpan(0, sizeof(ushort)),
            rawCommand);
        completion[2] = status;
        completion[3] = flags;
        EndianPrimitives.WriteUInt16LittleEndian(
            completion.AsSpan(4, sizeof(ushort)),
            extendedStatus);
        EndianPrimitives.WriteUInt16LittleEndian(
            completion.AsSpan(6, sizeof(ushort)),
            submissionQueueHeadIndex);
        return completion;
    }
}
