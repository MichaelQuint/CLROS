using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class EnaAdminCommandPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.admin.command plans get feature", TestPlansGetFeature, ref failures);
        Run("drivers.ena.admin.command validates completion", TestValidatesCompletion, ref failures);
        Run("drivers.ena.admin.command rejects timeout budget", TestRejectsTimeoutBudget, ref failures);
        Run("drivers.ena.admin.command rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("drivers.ena.admin.command rejects completion failures", TestRejectsCompletionFailures, ref failures);
        Run("drivers.ena.admin.command rejects default results", TestRejectsDefaultResults, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA admin command plan host tests failed: " + failures);
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

    private static bool TestRejectsDefaultResults()
    {
        EnaAdminCommandPlanResult plan = default;
        EnaAdminCommandSubmissionResult submission = default;
        EnaAdminCommandCompletionValidationResult completion = default;

        return !plan.IsInitialized
            && !plan.Succeeded
            && plan.Status == EnaAdminCommandPlanStatus.Success
            && !submission.IsInitialized
            && !submission.Succeeded
            && submission.Status == EnaAdminCommandPlanStatus.Success
            && !completion.IsInitialized
            && !completion.Succeeded
            && completion.Status == EnaAdminCommandPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaAdminCommandPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaAdminCommandSubmissionResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaAdminCommandCompletionValidationResult>();
    }

    private static bool TestPlansGetFeature()
    {
        EnaAdminCommandPlanResult result =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 0x0123,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);
        if (!result.Succeeded)
        {
            return false;
        }

        EnaAdminCommandPlan plan = result.Plan;
        byte[] descriptor = new byte[EnaAdminDescriptors.GetFeatureCommandByteLength + 1];
        Array.Fill(descriptor, (byte)0xCC);

        EnaAdminCommandSubmissionResult submission =
            plan.TryWriteSubmission(descriptor);
        EnaAdminDescriptorStatus readStatus =
            EnaAdminDescriptors.TryReadGetFeatureCommand(
                descriptor.AsSpan(0, plan.SubmissionDescriptorLength),
                out EnaAdminGetFeatureCommand command);

        return result.Status == EnaAdminCommandPlanStatus.Success
            && result.CommandId == 0x0123
            && result.FeatureId == EnaAdminFeatureId.DeviceAttributes
            && result.SubmissionPhase
            && result.ExpectedCompletionPhase
            && result.TimeoutTicks == EnaAdminCommandPlan.MinimumTimeoutTicks
            && plan.Opcode == EnaAdminOpcode.GetFeature
            && plan.FeatureId == EnaAdminFeatureId.DeviceAttributes
            && plan.CommandId == 0x0123
            && plan.ExpectedCompletionCommandId == 0x0123
            && plan.SubmissionPhase
            && plan.ExpectedCompletionPhase
            && plan.TimeoutTicks == EnaAdminCommandPlan.MinimumTimeoutTicks
            && plan.SubmissionDescriptorLength == EnaAdminDescriptors.GetFeatureCommandByteLength
            && plan.CompletionDescriptorLength == EnaDeviceAttributesParser.ResponseByteLength
            && submission.Succeeded
            && submission.Status == EnaAdminCommandPlanStatus.Success
            && submission.DescriptorStatus == EnaAdminDescriptorStatus.Success
            && submission.BytesWritten == EnaAdminDescriptors.GetFeatureCommandByteLength
            && submission.DestinationLength == descriptor.Length
            && readStatus == EnaAdminDescriptorStatus.Success
            && command.CommonDescriptor.CommandId == 0x0123
            && command.CommonDescriptor.Opcode == (byte)EnaAdminOpcode.GetFeature
            && command.CommonDescriptor.Phase
            && command.FeatureId == (byte)EnaAdminFeatureId.DeviceAttributes
            && command.FeatureSelect == (byte)EnaAdminFeatureSelect.CurrentValue
            && descriptor[EnaAdminDescriptors.GetFeatureCommandByteLength] == 0xCC
            && !EnaAdminCommandPlan.PerformsMmio
            && !EnaAdminCommandPlan.AllocatesDmaMemory
            && !EnaAdminCommandPlan.RingsDoorbell
            && !EnaAdminCommandPlan.ConfiguresInterrupts
            && !EnaAdminCommandPlan.PerformsPacketIo
            && !EnaAdminCommandPlan.ExecutesAdminCommand;
    }

    private static bool TestValidatesCompletion()
    {
        EnaAdminCommandPlanResult result =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 0x0007,
                submissionPhase: false,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks + 10);
        if (!result.Succeeded)
        {
            return false;
        }

        byte[] response = CreateDeviceAttributesResponse(
            rawCommand: 0x0007,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0x0002,
            submissionQueueHeadIndex: 0x0003,
            implementationId: 0x01020304,
            deviceVersion: 0x05060708,
            supportedFeatures: 0x00000003,
            capabilities: 0x00000005,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 9001);

        EnaAdminCommandCompletionValidationResult validation =
            result.Plan.ValidateDeviceAttributesCompletion(response);

        return validation.Succeeded
            && validation.Status == EnaAdminCommandPlanStatus.Success
            && validation.DeviceAttributesStatus == EnaDeviceAttributesStatus.Success
            && validation.InputLength == EnaDeviceAttributesParser.ResponseByteLength
            && validation.CommandId == 0x0007
            && validation.ExpectedCommandId == 0x0007
            && validation.Phase
            && validation.ExpectedPhase
            && validation.CompletionStatus == EnaAdminCompletionStatus.Success
            && validation.DeviceAttributes.CommandId == 0x0007
            && validation.DeviceAttributes.SubmissionQueueHeadIndex == 0x0003
            && validation.DeviceAttributes.ImplementationId == 0x01020304
            && validation.DeviceAttributes.DeviceVersion == 0x05060708
            && validation.DeviceAttributes.SupportedFeatures == 0x00000003
            && validation.DeviceAttributes.Capabilities == 0x00000005
            && validation.DeviceAttributes.PhysicalAddressWidth == 48
            && validation.DeviceAttributes.VirtualAddressWidth == 64
            && validation.DeviceAttributes.MacAddress == TestMac
            && validation.DeviceAttributes.MaxMtu == 9001;
    }

    private static bool TestRejectsTimeoutBudget()
    {
        EnaAdminCommandPlanResult zero =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 1,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: 0);
        EnaAdminCommandPlanResult tooSmall =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 1,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks - 1);
        EnaAdminCommandPlanResult exactMinimum =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 1,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);

        return !zero.Succeeded
            && zero.Status == EnaAdminCommandPlanStatus.TimeoutBudgetTooSmall
            && zero.TimeoutTicks == 0
            && !tooSmall.Succeeded
            && tooSmall.Status == EnaAdminCommandPlanStatus.TimeoutBudgetTooSmall
            && tooSmall.TimeoutTicks == EnaAdminCommandPlan.MinimumTimeoutTicks - 1
            && exactMinimum.Succeeded;
    }

    private static bool TestRejectsInvalidInputs()
    {
        EnaAdminCommandPlanResult commandOutOfRange =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 0x1000,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);
        EnaAdminCommandPlanResult unsupportedFeature =
            EnaAdminCommandPlan.CreateGetFeature(
                commandId: 0x0010,
                submissionPhase: true,
                expectedCompletionPhase: true,
                EnaAdminFeatureId.MaxQueuesNumber,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);
        EnaAdminCommandPlanResult valid =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 0x0010,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);
        if (!valid.Succeeded)
        {
            return false;
        }

        EnaAdminCommandSubmissionResult shortSubmission =
            valid.Plan.TryWriteSubmission(
                new byte[EnaAdminDescriptors.GetFeatureCommandByteLength - 1]);

        return !commandOutOfRange.Succeeded
            && commandOutOfRange.Status == EnaAdminCommandPlanStatus.CommandIdOutOfRange
            && commandOutOfRange.CommandId == 0x1000
            && !unsupportedFeature.Succeeded
            && unsupportedFeature.Status == EnaAdminCommandPlanStatus.UnsupportedFeature
            && unsupportedFeature.FeatureId == EnaAdminFeatureId.MaxQueuesNumber
            && !shortSubmission.Succeeded
            && shortSubmission.Status == EnaAdminCommandPlanStatus.SubmissionBufferTooSmall
            && shortSubmission.DescriptorStatus == EnaAdminDescriptorStatus.BufferTooSmall
            && shortSubmission.BytesWritten == 0;
    }

    private static bool TestRejectsCompletionFailures()
    {
        EnaAdminCommandPlanResult result =
            EnaAdminCommandPlan.CreateGetFeatureDeviceAttributes(
                commandId: 0x0020,
                submissionPhase: true,
                expectedCompletionPhase: true,
                timeoutTicks: EnaAdminCommandPlan.MinimumTimeoutTicks);
        if (!result.Succeeded)
        {
            return false;
        }

        EnaAdminCommandPlan plan = result.Plan;
        byte[] commandMismatch = CreateDeviceAttributesResponse(
            rawCommand: 0x0021,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] phaseMismatch = CreateDeviceAttributesResponse(
            rawCommand: 0x0020,
            status: EnaAdminCompletionStatus.Success,
            phase: false,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] completionFailure = CreateDeviceAttributesResponse(
            rawCommand: 0x0020,
            status: EnaAdminCompletionStatus.ResourceBusy,
            phase: true,
            extendedStatus: 0xBEEF,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] invalidPayload = CreateDeviceAttributesResponse(
            rawCommand: 0x0020,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: default,
            maxMtu: 1500);
        byte[] tooShort = new byte[EnaDeviceAttributesParser.ResponseByteLength - 1];

        EnaAdminCommandCompletionValidationResult commandResult =
            plan.ValidateDeviceAttributesCompletion(commandMismatch);
        EnaAdminCommandCompletionValidationResult phaseResult =
            plan.ValidateDeviceAttributesCompletion(phaseMismatch);
        EnaAdminCommandCompletionValidationResult completionResult =
            plan.ValidateDeviceAttributesCompletion(completionFailure);
        EnaAdminCommandCompletionValidationResult invalidPayloadResult =
            plan.ValidateDeviceAttributesCompletion(invalidPayload);
        EnaAdminCommandCompletionValidationResult shortResult =
            plan.ValidateDeviceAttributesCompletion(tooShort);

        return !commandResult.Succeeded
            && commandResult.Status == EnaAdminCommandPlanStatus.CompletionCommandIdMismatch
            && commandResult.DeviceAttributesStatus == EnaDeviceAttributesStatus.CommandIdMismatch
            && commandResult.CommandId == 0x0021
            && commandResult.ExpectedCommandId == 0x0020
            && !phaseResult.Succeeded
            && phaseResult.Status == EnaAdminCommandPlanStatus.CompletionPhaseMismatch
            && phaseResult.DeviceAttributesStatus == EnaDeviceAttributesStatus.PhaseMismatch
            && !phaseResult.Phase
            && phaseResult.ExpectedPhase
            && !completionResult.Succeeded
            && completionResult.Status == EnaAdminCommandPlanStatus.CompletionStatusNotSuccess
            && completionResult.DeviceAttributesStatus == EnaDeviceAttributesStatus.CompletionStatusNotSuccess
            && completionResult.CompletionStatus == EnaAdminCompletionStatus.ResourceBusy
            && !invalidPayloadResult.Succeeded
            && invalidPayloadResult.Status == EnaAdminCommandPlanStatus.DeviceAttributesRejected
            && invalidPayloadResult.DeviceAttributesStatus == EnaDeviceAttributesStatus.ZeroMacAddress
            && !shortResult.Succeeded
            && shortResult.Status == EnaAdminCommandPlanStatus.CompletionBufferTooSmall
            && shortResult.DeviceAttributesStatus == EnaDeviceAttributesStatus.BufferTooSmall
            && shortResult.InputLength == tooShort.Length;
    }

    private static byte[] CreateDeviceAttributesResponse(
        ushort rawCommand,
        EnaAdminCompletionStatus status,
        bool phase,
        ushort extendedStatus,
        ushort submissionQueueHeadIndex,
        uint implementationId,
        uint deviceVersion,
        uint supportedFeatures,
        uint capabilities,
        uint physicalAddressWidth,
        uint virtualAddressWidth,
        MacAddress macAddress,
        uint maxMtu)
    {
        byte[] response = new byte[EnaDeviceAttributesParser.ResponseByteLength];
        EndianPrimitives.WriteUInt16LittleEndian(
            response.AsSpan(0, sizeof(ushort)),
            rawCommand);
        response[2] = (byte)status;
        response[3] = phase ? EnaAdminDescriptors.PhaseFlag : (byte)0;
        EndianPrimitives.WriteUInt16LittleEndian(
            response.AsSpan(4, sizeof(ushort)),
            extendedStatus);
        EndianPrimitives.WriteUInt16LittleEndian(
            response.AsSpan(6, sizeof(ushort)),
            submissionQueueHeadIndex);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.ImplementationIdOffset, sizeof(uint)),
            implementationId);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.DeviceVersionOffset, sizeof(uint)),
            deviceVersion);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.SupportedFeaturesOffset, sizeof(uint)),
            supportedFeatures);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.CapabilitiesOffset, sizeof(uint)),
            capabilities);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.PhysicalAddressWidthOffset, sizeof(uint)),
            physicalAddressWidth);
        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.VirtualAddressWidthOffset, sizeof(uint)),
            virtualAddressWidth);
        PacketParseStatus writeStatus = macAddress.TryWrite(
            response,
            EnaDeviceAttributesParser.MacAddressOffset);
        if (writeStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build ENA device attributes MAC.");
        }

        EndianPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(EnaDeviceAttributesParser.MaxMtuOffset, sizeof(uint)),
            maxMtu);
        return response;
    }

    private static MacAddress TestMac =>
        new MacAddress(0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE);
}
