using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class EnaDeviceAttributesTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.deviceattr parses mac and mtu", TestParsesMacAndMtu, ref failures);
        Run("drivers.ena.deviceattr preserves address widths", TestPreservesAddressWidths, ref failures);
        Run("drivers.ena.deviceattr rejects zero mac", TestRejectsZeroMac, ref failures);
        Run("drivers.ena.deviceattr rejects broadcast mac", TestRejectsBroadcastMac, ref failures);
        Run("drivers.ena.deviceattr reports completion failure", TestReportsCompletionFailure, ref failures);
        Run("drivers.ena.deviceattr rejects malformed responses", TestRejectsMalformedResponses, ref failures);
        Run("drivers.ena.deviceattr rejects default parse result", TestRejectsDefaultParseResult, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA device attributes host tests failed: " + failures);
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

    private static bool TestRejectsDefaultParseResult()
    {
        EnaDeviceAttributesParseResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == EnaDeviceAttributesStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaDeviceAttributesParseResult>();
    }

    private static bool TestParsesMacAndMtu()
    {
        byte[] response = CreateDeviceAttributesResponse(
            rawCommand: 0x0123,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 4,
            implementationId: 0x11223344,
            deviceVersion: 0x01020304,
            supportedFeatures: 0x0000_4007,
            capabilities: 0x0000_0085,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 9001);

        EnaDeviceAttributesParseResult result =
            EnaDeviceAttributesParser.TryParse(
                response,
                expectedCommandId: 0x0123,
                expectedPhase: true);

        EnaDeviceAttributes attributes = result.Attributes;
        return result.Succeeded
            && result.Status == EnaDeviceAttributesStatus.Success
            && result.InputLength == EnaDeviceAttributesParser.ResponseByteLength
            && result.BytesConsumed == EnaDeviceAttributesParser.ResponseByteLength
            && attributes.CommandId == 0x0123
            && attributes.Phase
            && attributes.CompletionStatus == EnaAdminCompletionStatus.Success
            && attributes.SubmissionQueueHeadIndex == 4
            && attributes.FeatureId == EnaAdminFeatureId.DeviceAttributes
            && attributes.ImplementationId == 0x11223344
            && attributes.DeviceVersion == 0x01020304
            && attributes.SupportedFeatures == 0x0000_4007
            && attributes.Capabilities == 0x0000_0085
            && attributes.PhysicalAddressWidth == 48
            && attributes.VirtualAddressWidth == 64
            && attributes.MacAddress == TestMac
            && attributes.MaxMtu == 9001
            && !EnaDeviceAttributesParser.PerformsMmio
            && !EnaDeviceAttributesParser.AllocatesDmaMemory
            && !EnaDeviceAttributesParser.ExecutesAdminCommand;
    }

    private static bool TestPreservesAddressWidths()
    {
        byte[] response = CreateDeviceAttributesResponse(
            rawCommand: 0x0007,
            status: EnaAdminCompletionStatus.Success,
            phase: false,
            extendedStatus: 0x0002,
            submissionQueueHeadIndex: 1,
            implementationId: 0xAABBCCDD,
            deviceVersion: 0x00010002,
            supportedFeatures: 0x00000003,
            capabilities: 0x00000009,
            physicalAddressWidth: 32,
            virtualAddressWidth: 33,
            macAddress: new MacAddress(0x06, 0x10, 0x20, 0x30, 0x40, 0x50),
            maxMtu: 1500);

        EnaDeviceAttributesParseResult result =
            EnaDeviceAttributesParser.TryParse(
                response,
                expectedCommandId: 0x0007,
                expectedPhase: false);

        return result.Succeeded
            && result.Attributes.PhysicalAddressWidth == 32
            && result.Attributes.VirtualAddressWidth == 33
            && result.Attributes.ExtendedStatus == 0x0002
            && result.Attributes.SupportedFeatures == 0x00000003
            && result.Attributes.Capabilities == 0x00000009
            && result.Attributes.MaxMtu == 1500;
    }

    private static bool TestRejectsZeroMac()
    {
        byte[] response = CreateDeviceAttributesResponse(
            rawCommand: 0x0001,
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

        EnaDeviceAttributesParseResult result =
            EnaDeviceAttributesParser.TryParse(
                response,
                expectedCommandId: 0x0001,
                expectedPhase: true);

        return !result.Succeeded
            && result.Status == EnaDeviceAttributesStatus.ZeroMacAddress
            && result.CommandId == 0x0001
            && result.CompletionStatus == EnaAdminCompletionStatus.Success
            && result.BytesConsumed == 0;
    }

    private static bool TestRejectsBroadcastMac()
    {
        byte[] response = CreateDeviceAttributesResponse(
            rawCommand: 0x0002,
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
            macAddress: new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            maxMtu: 1500);

        EnaDeviceAttributesParseResult result =
            EnaDeviceAttributesParser.TryParse(
                response,
                expectedCommandId: 0x0002,
                expectedPhase: true);

        return !result.Succeeded
            && result.Status == EnaDeviceAttributesStatus.BroadcastMacAddress
            && result.CommandId == 0x0002
            && result.ExpectedCommandId == 0x0002
            && result.Phase
            && result.ExpectedPhase;
    }

    private static bool TestReportsCompletionFailure()
    {
        byte[] completionFailure = CreateDeviceAttributesResponse(
            rawCommand: 0x0003,
            status: EnaAdminCompletionStatus.IllegalParameter,
            phase: true,
            extendedStatus: 0xBEEF,
            submissionQueueHeadIndex: 2,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] commandMismatch = CreateDeviceAttributesResponse(
            rawCommand: 0x0004,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 2,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] phaseMismatch = CreateDeviceAttributesResponse(
            rawCommand: 0x0005,
            status: EnaAdminCompletionStatus.Success,
            phase: false,
            extendedStatus: 0,
            submissionQueueHeadIndex: 2,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);

        EnaDeviceAttributesParseResult completionResult =
            EnaDeviceAttributesParser.TryParse(
                completionFailure,
                expectedCommandId: 0x0003,
                expectedPhase: true);
        EnaDeviceAttributesParseResult commandResult =
            EnaDeviceAttributesParser.TryParse(
                commandMismatch,
                expectedCommandId: 0x0006,
                expectedPhase: true);
        EnaDeviceAttributesParseResult phaseResult =
            EnaDeviceAttributesParser.TryParse(
                phaseMismatch,
                expectedCommandId: 0x0005,
                expectedPhase: true);
        EnaDeviceAttributesParseResult commandOutOfRange =
            EnaDeviceAttributesParser.TryParse(
                phaseMismatch,
                expectedCommandId: 0x1000,
                expectedPhase: true);

        return !completionResult.Succeeded
            && completionResult.Status == EnaDeviceAttributesStatus.CompletionStatusNotSuccess
            && completionResult.CompletionStatus == EnaAdminCompletionStatus.IllegalParameter
            && completionResult.CommandId == 0x0003
            && !commandResult.Succeeded
            && commandResult.Status == EnaDeviceAttributesStatus.CommandIdMismatch
            && commandResult.CommandId == 0x0004
            && commandResult.ExpectedCommandId == 0x0006
            && !phaseResult.Succeeded
            && phaseResult.Status == EnaDeviceAttributesStatus.PhaseMismatch
            && !phaseResult.Phase
            && phaseResult.ExpectedPhase
            && !commandOutOfRange.Succeeded
            && commandOutOfRange.Status == EnaDeviceAttributesStatus.CommandIdOutOfRange;
    }

    private static bool TestRejectsMalformedResponses()
    {
        byte[] tooShort = new byte[EnaDeviceAttributesParser.ResponseByteLength - 1];
        byte[] invalidPhysicalWidth = CreateDeviceAttributesResponse(
            rawCommand: 0x0008,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 31,
            virtualAddressWidth: 64,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] invalidVirtualWidth = CreateDeviceAttributesResponse(
            rawCommand: 0x0009,
            status: EnaAdminCompletionStatus.Success,
            phase: true,
            extendedStatus: 0,
            submissionQueueHeadIndex: 1,
            implementationId: 1,
            deviceVersion: 2,
            supportedFeatures: 3,
            capabilities: 4,
            physicalAddressWidth: 48,
            virtualAddressWidth: 65,
            macAddress: TestMac,
            maxMtu: 1500);
        byte[] invalidMtu = CreateDeviceAttributesResponse(
            rawCommand: 0x000A,
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
            maxMtu: 0);

        EnaDeviceAttributesParseResult shortResult =
            EnaDeviceAttributesParser.TryParse(
                tooShort,
                expectedCommandId: 0x0008,
                expectedPhase: true);
        EnaDeviceAttributesParseResult physicalWidthResult =
            EnaDeviceAttributesParser.TryParse(
                invalidPhysicalWidth,
                expectedCommandId: 0x0008,
                expectedPhase: true);
        EnaDeviceAttributesParseResult virtualWidthResult =
            EnaDeviceAttributesParser.TryParse(
                invalidVirtualWidth,
                expectedCommandId: 0x0009,
                expectedPhase: true);
        EnaDeviceAttributesParseResult mtuResult =
            EnaDeviceAttributesParser.TryParse(
                invalidMtu,
                expectedCommandId: 0x000A,
                expectedPhase: true);

        return !shortResult.Succeeded
            && shortResult.Status == EnaDeviceAttributesStatus.BufferTooSmall
            && shortResult.InputLength == tooShort.Length
            && !physicalWidthResult.Succeeded
            && physicalWidthResult.Status == EnaDeviceAttributesStatus.InvalidPhysicalAddressWidth
            && !virtualWidthResult.Succeeded
            && virtualWidthResult.Status == EnaDeviceAttributesStatus.InvalidVirtualAddressWidth
            && !mtuResult.Succeeded
            && mtuResult.Status == EnaDeviceAttributesStatus.InvalidMtu;
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
        new MacAddress(0x02, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0);
}
