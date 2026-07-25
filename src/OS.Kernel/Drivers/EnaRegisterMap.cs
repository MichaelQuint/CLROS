namespace OS.Drivers;

public enum EnaRegisterApertureValidationStatus
{
    Success = 0,
    AdminQueueRegistersTruncated = 1,
    ReadinessRegistersTruncated = 2
}

public readonly struct EnaRegisterApertureValidationResult
{
    private EnaRegisterApertureValidationResult(
        bool isInitialized,
        EnaRegisterApertureValidationStatus status,
        ulong apertureLength,
        ulong requiredLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureLength = apertureLength;
        RequiredLength = requiredLength;
    }

    public EnaRegisterApertureValidationStatus Status { get; }

    public ulong ApertureLength { get; }

    public ulong RequiredLength { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == EnaRegisterApertureValidationStatus.Success;

    public static EnaRegisterApertureValidationResult Success(
        ulong apertureLength,
        ulong requiredLength)
    {
        return new EnaRegisterApertureValidationResult(
            isInitialized: true,
            EnaRegisterApertureValidationStatus.Success,
            apertureLength,
            requiredLength);
    }

    public static EnaRegisterApertureValidationResult Fail(
        EnaRegisterApertureValidationStatus status,
        ulong apertureLength,
        ulong requiredLength)
    {
        return new EnaRegisterApertureValidationResult(
            isInitialized: true,
            status,
            apertureLength,
            requiredLength);
    }
}

public enum EnaAdminReadinessStatus
{
    Success = 0,
    RegisterApertureTooSmall = 1,
    UnsupportedContiguousQueueRequirement = 2,
    DeviceFatalError = 3,
    DeviceResetInProgress = 4,
    AdminQueueRestartInProgress = 5,
    DeviceQuiescentStateInProgress = 6,
    DeviceNotReady = 7
}

public readonly struct EnaAdminReadinessResult
{
    private EnaAdminReadinessResult(
        bool isInitialized,
        EnaAdminReadinessStatus status,
        EnaRegisterApertureValidationResult apertureValidation,
        uint capabilities,
        uint deviceStatus)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureValidation = apertureValidation;
        Capabilities = capabilities;
        DeviceStatus = deviceStatus;
    }

    public EnaAdminReadinessStatus Status { get; }

    public EnaRegisterApertureValidationResult ApertureValidation { get; }

    public uint Capabilities { get; }

    public uint DeviceStatus { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaAdminReadinessStatus.Success;

    public bool RequiresContiguousQueueMemory =>
        EnaRegisterMap.Capabilities.RequiresContiguousQueueMemory(Capabilities);

    public uint ResetTimeoutEncoded =>
        EnaRegisterMap.Capabilities.GetResetTimeoutEncoded(Capabilities);

    public uint DmaAddressWidthBits =>
        EnaRegisterMap.Capabilities.GetDmaAddressWidthBits(Capabilities);

    public uint AdminCommandTimeoutEncoded =>
        EnaRegisterMap.Capabilities.GetAdminCommandTimeoutEncoded(Capabilities);

    public bool DeviceReady => EnaRegisterMap.DeviceStatus.IsReady(DeviceStatus);

    public bool DeviceFatalError => EnaRegisterMap.DeviceStatus.HasFatalError(DeviceStatus);

    public bool DeviceResetInProgress =>
        EnaRegisterMap.DeviceStatus.IsResetInProgress(DeviceStatus);

    public bool AdminQueueRestartInProgress =>
        EnaRegisterMap.DeviceStatus.IsAdminQueueRestartInProgress(DeviceStatus);

    public bool DeviceQuiescentStateInProgress =>
        EnaRegisterMap.DeviceStatus.IsQuiescentStateInProgress(DeviceStatus);

    public static EnaAdminReadinessResult Success(
        EnaRegisterApertureValidationResult apertureValidation,
        uint capabilities,
        uint deviceStatus)
    {
        return new EnaAdminReadinessResult(
            isInitialized: true,
            EnaAdminReadinessStatus.Success,
            apertureValidation,
            capabilities,
            deviceStatus);
    }

    public static EnaAdminReadinessResult Fail(
        EnaAdminReadinessStatus status,
        EnaRegisterApertureValidationResult apertureValidation,
        uint capabilities,
        uint deviceStatus)
    {
        return new EnaAdminReadinessResult(
            isInitialized: true,
            status,
            apertureValidation,
            capabilities,
            deviceStatus);
    }
}

public static class EnaAdminReadinessPlanner
{
    public static EnaAdminReadinessResult Assess(
        ulong registerBarApertureLength,
        uint capabilities,
        uint deviceStatus)
    {
        EnaRegisterApertureValidationResult apertureValidation =
            EnaRegisterMap.ValidateAdminReadinessRegisterAperture(registerBarApertureLength);
        if (!apertureValidation.Succeeded)
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.RegisterApertureTooSmall,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (EnaRegisterMap.Capabilities.RequiresContiguousQueueMemory(capabilities))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.UnsupportedContiguousQueueRequirement,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.HasFatalError(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.DeviceFatalError,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsResetInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.DeviceResetInProgress,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsAdminQueueRestartInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.AdminQueueRestartInProgress,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsQuiescentStateInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.DeviceQuiescentStateInProgress,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        if (!EnaRegisterMap.DeviceStatus.IsReady(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(
                EnaAdminReadinessStatus.DeviceNotReady,
                apertureValidation,
                capabilities,
                deviceStatus);
        }

        return EnaAdminReadinessResult.Success(apertureValidation, capabilities, deviceStatus);
    }
}

public static class EnaRegisterMap
{
    public const uint RegisterBaseOffset = 0x0000;
    public const uint RegisterWidth = sizeof(uint);
    public const uint RequiredAdminQueueRegisterApertureLength =
        AdminCompletionQueueTail.Offset + AdminCompletionQueueTail.Width;
    public const uint RequiredAdminReadinessRegisterApertureLength =
        DeviceStatus.Offset + DeviceStatus.Width;

    public static class Version
    {
        public const uint Offset = 0x0000;
        public const uint Width = sizeof(uint);
        public const uint MinorVersionMask = 0x000000FF;
        public const byte MajorVersionShift = 8;
        public const uint MajorVersionMask = 0x0000FF00;
    }

    public static class ControllerVersion
    {
        public const uint Offset = 0x0004;
        public const uint Width = sizeof(uint);
        public const uint SubminorVersionMask = 0x000000FF;
        public const byte MinorVersionShift = 8;
        public const uint MinorVersionMask = 0x0000FF00;
        public const byte MajorVersionShift = 16;
        public const uint MajorVersionMask = 0x00FF0000;
        public const byte ImplementationIdShift = 24;
        public const uint ImplementationIdMask = 0xFF000000;
    }

    public static class Capabilities
    {
        public const uint Offset = 0x0008;
        public const uint Width = sizeof(uint);
        public const byte ContiguousQueueRequiredBit = 0;
        public const uint ContiguousQueueRequired = 1u << ContiguousQueueRequiredBit;
        public const byte ResetTimeoutShift = 1;
        public const uint ResetTimeoutMask = 0x0000003E;
        public const byte DmaAddressWidthShift = 8;
        public const uint DmaAddressWidthMask = 0x0000FF00;
        public const byte AdminCommandTimeoutShift = 16;
        public const uint AdminCommandTimeoutMask = 0x000F0000;

        public static bool RequiresContiguousQueueMemory(uint capabilities)
        {
            return (capabilities & ContiguousQueueRequired) != 0;
        }

        public static uint GetResetTimeoutEncoded(uint capabilities)
        {
            return (capabilities & ResetTimeoutMask) >> ResetTimeoutShift;
        }

        public static uint GetDmaAddressWidthBits(uint capabilities)
        {
            return (capabilities & DmaAddressWidthMask) >> DmaAddressWidthShift;
        }

        public static uint GetAdminCommandTimeoutEncoded(uint capabilities)
        {
            return (capabilities & AdminCommandTimeoutMask) >> AdminCommandTimeoutShift;
        }
    }

    public static class CapabilitiesExtension
    {
        public const uint Offset = 0x000C;
        public const uint Width = sizeof(uint);
    }

    public static class AdminSubmissionQueueBase
    {
        public const uint LowOffset = 0x0010;
        public const uint HighOffset = 0x0014;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }

    public static class AdminQueueCapabilities
    {
        public const uint Offset = 0x0018;
        public const uint Width = sizeof(uint);
        public const uint DepthMask = 0x0000FFFF;
        public const byte EntrySizeShift = 16;
        public const uint EntrySizeMask = 0xFFFF0000;

        public static uint GetDepth(uint capabilities)
        {
            return capabilities & DepthMask;
        }

        public static uint GetEntrySizeBytes(uint capabilities)
        {
            return (capabilities & EntrySizeMask) >> EntrySizeShift;
        }
    }

    public static class AdminCompletionQueueBase
    {
        public const uint LowOffset = 0x0020;
        public const uint HighOffset = 0x0024;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }

    public static class AdminCompletionQueueCapabilities
    {
        public const uint Offset = 0x0028;
        public const uint Width = sizeof(uint);
        public const uint DepthMask = 0x0000FFFF;
        public const byte EntrySizeShift = 16;
        public const uint EntrySizeMask = 0xFFFF0000;

        public static uint GetDepth(uint capabilities)
        {
            return capabilities & DepthMask;
        }

        public static uint GetEntrySizeBytes(uint capabilities)
        {
            return (capabilities & EntrySizeMask) >> EntrySizeShift;
        }
    }

    public static class AdminSubmissionQueueDoorbell
    {
        public const uint Offset = 0x002C;
        public const uint Width = sizeof(uint);
    }

    public static class AdminCompletionQueueTail
    {
        public const uint Offset = 0x0030;
        public const uint Width = sizeof(uint);
    }

    public static class AsyncEventNotificationQueueCapabilities
    {
        public const uint Offset = 0x0034;
        public const uint Width = sizeof(uint);
        public const uint DepthMask = 0x0000FFFF;
        public const byte EntrySizeShift = 16;
        public const uint EntrySizeMask = 0xFFFF0000;
    }

    public static class AsyncEventNotificationQueueBase
    {
        public const uint LowOffset = 0x0038;
        public const uint HighOffset = 0x003C;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }

    public static class AsyncEventNotificationQueueHeadDoorbell
    {
        public const uint Offset = 0x0040;
        public const uint Width = sizeof(uint);
    }

    public static class AsyncEventNotificationQueueTail
    {
        public const uint Offset = 0x0044;
        public const uint Width = sizeof(uint);
    }

    public static class InterruptMask
    {
        public const uint Offset = 0x004C;
        public const uint Width = sizeof(uint);
    }

    public static class DeviceControl
    {
        public const uint Offset = 0x0054;
        public const uint Width = sizeof(uint);
        public const byte DeviceResetBit = 0;
        public const uint DeviceReset = 1u << DeviceResetBit;
        public const byte AdminQueueRestartShift = 1;
        public const uint AdminQueueRestart = 0x00000002;
        public const byte QuiescentShift = 2;
        public const uint Quiescent = 0x00000004;
        public const byte IoResumeShift = 3;
        public const uint IoResume = 0x00000008;
        public const byte ResetReasonShift = 28;
        public const uint ResetReasonMask = 0xF0000000;
    }

    public static class DeviceStatus
    {
        public const uint Offset = 0x0058;
        public const uint Width = sizeof(uint);
        public const byte ReadyBit = 0;
        public const uint Ready = 1u << ReadyBit;
        public const byte AdminQueueRestartInProgressShift = 1;
        public const uint AdminQueueRestartInProgress = 0x00000002;
        public const byte AdminQueueRestartFinishedShift = 2;
        public const uint AdminQueueRestartFinished = 0x00000004;
        public const byte ResetInProgressShift = 3;
        public const uint ResetInProgress = 0x00000008;
        public const byte ResetFinishedShift = 4;
        public const uint ResetFinished = 0x00000010;
        public const byte FatalErrorShift = 5;
        public const uint FatalError = 0x00000020;
        public const byte QuiescentStateInProgressShift = 6;
        public const uint QuiescentStateInProgress = 0x00000040;
        public const byte QuiescentStateAchievedShift = 7;
        public const uint QuiescentStateAchieved = 0x00000080;

        public static bool IsReady(uint deviceStatus)
        {
            return (deviceStatus & Ready) != 0;
        }

        public static bool IsAdminQueueRestartInProgress(uint deviceStatus)
        {
            return (deviceStatus & AdminQueueRestartInProgress) != 0;
        }

        public static bool IsResetInProgress(uint deviceStatus)
        {
            return (deviceStatus & ResetInProgress) != 0;
        }

        public static bool HasFatalError(uint deviceStatus)
        {
            return (deviceStatus & FatalError) != 0;
        }

        public static bool IsQuiescentStateInProgress(uint deviceStatus)
        {
            return (deviceStatus & QuiescentStateInProgress) != 0;
        }
    }

    public static class MmioRegisterRead
    {
        public const uint Offset = 0x005C;
        public const uint Width = sizeof(uint);
        public const uint RequestIdMask = 0x0000FFFF;
        public const byte RegisterOffsetShift = 16;
        public const uint RegisterOffsetMask = 0xFFFF0000;
    }

    public static class MmioResponse
    {
        public const uint LowOffset = 0x0060;
        public const uint HighOffset = 0x0064;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }

    public static class RssIndirectionEntryUpdate
    {
        public const uint Offset = 0x0068;
        public const uint Width = sizeof(uint);
        public const uint IndexMask = 0x0000FFFF;
        public const byte CompletionQueueIndexShift = 16;
        public const uint CompletionQueueIndexMask = 0xFFFF0000;
    }

    public static class PhcDoorbell
    {
        public const uint Offset = 0x0100;
        public const uint Width = sizeof(uint);
        public const uint RequestIdMask = 0x0000FFFF;
    }

    public static EnaRegisterApertureValidationResult ValidateAdminQueueRegisterAperture(
        ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminQueueRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return EnaRegisterApertureValidationResult.Fail(
                EnaRegisterApertureValidationStatus.AdminQueueRegistersTruncated,
                apertureLength,
                requiredLength);
        }

        return EnaRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }

    public static EnaRegisterApertureValidationResult ValidateAdminReadinessRegisterAperture(
        ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminReadinessRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return EnaRegisterApertureValidationResult.Fail(
                EnaRegisterApertureValidationStatus.ReadinessRegistersTruncated,
                apertureLength,
                requiredLength);
        }

        return EnaRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }
}
