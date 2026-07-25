namespace OS.Drivers;

public enum EnaAdminCommandPlanStatus
{
    Success = 0,
    CommandIdOutOfRange = 1,
    UnsupportedFeature = 2,
    TimeoutBudgetTooSmall = 3,
    SubmissionBufferTooSmall = 4,
    CompletionBufferTooSmall = 5,
    DescriptorWriteFailed = 6,
    CompletionCommandIdMismatch = 7,
    CompletionPhaseMismatch = 8,
    CompletionStatusNotSuccess = 9,
    DeviceAttributesRejected = 10
}

public readonly struct EnaAdminCommandPlanResult
{
    private EnaAdminCommandPlanResult(
        bool isInitialized,
        EnaAdminCommandPlanStatus status,
        ushort commandId,
        bool submissionPhase,
        bool expectedCompletionPhase,
        EnaAdminFeatureId featureId,
        uint timeoutTicks,
        EnaAdminCommandPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        CommandId = commandId;
        SubmissionPhase = submissionPhase;
        ExpectedCompletionPhase = expectedCompletionPhase;
        FeatureId = featureId;
        TimeoutTicks = timeoutTicks;
        Plan = plan;
    }

    public EnaAdminCommandPlanStatus Status { get; }

    public ushort CommandId { get; }

    public bool SubmissionPhase { get; }

    public bool ExpectedCompletionPhase { get; }

    public EnaAdminFeatureId FeatureId { get; }

    public uint TimeoutTicks { get; }

    public EnaAdminCommandPlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaAdminCommandPlanStatus.Success;

    internal static EnaAdminCommandPlanResult Success(EnaAdminCommandPlan plan)
    {
        return new EnaAdminCommandPlanResult(
            isInitialized: true,
            EnaAdminCommandPlanStatus.Success,
            plan.CommandId,
            plan.SubmissionPhase,
            plan.ExpectedCompletionPhase,
            plan.FeatureId,
            plan.TimeoutTicks,
            plan);
    }

    internal static EnaAdminCommandPlanResult Fail(
        EnaAdminCommandPlanStatus status,
        ushort commandId,
        bool submissionPhase,
        bool expectedCompletionPhase,
        EnaAdminFeatureId featureId,
        uint timeoutTicks)
    {
        return new EnaAdminCommandPlanResult(
            isInitialized: true,
            status,
            commandId,
            submissionPhase,
            expectedCompletionPhase,
            featureId,
            timeoutTicks,
            default);
    }
}

public readonly struct EnaAdminCommandSubmissionResult
{
    private EnaAdminCommandSubmissionResult(
        bool isInitialized,
        EnaAdminCommandPlanStatus status,
        EnaAdminDescriptorStatus descriptorStatus,
        int destinationLength,
        int bytesWritten)
    {
        IsInitialized = isInitialized;
        Status = status;
        DescriptorStatus = descriptorStatus;
        DestinationLength = destinationLength;
        BytesWritten = bytesWritten;
    }

    public EnaAdminCommandPlanStatus Status { get; }

    public EnaAdminDescriptorStatus DescriptorStatus { get; }

    public int DestinationLength { get; }

    public int BytesWritten { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaAdminCommandPlanStatus.Success;

    internal static EnaAdminCommandSubmissionResult Create(
        EnaAdminCommandPlanStatus status,
        EnaAdminDescriptorStatus descriptorStatus,
        int destinationLength,
        int bytesWritten)
    {
        return new EnaAdminCommandSubmissionResult(
            isInitialized: true,
            status,
            descriptorStatus,
            destinationLength,
            bytesWritten);
    }
}

public readonly struct EnaAdminCommandCompletionValidationResult
{
    private EnaAdminCommandCompletionValidationResult(
        bool isInitialized,
        EnaAdminCommandPlanStatus status,
        EnaDeviceAttributesStatus deviceAttributesStatus,
        EnaAdminCompletionStatus completionStatus,
        int inputLength,
        ushort commandId,
        ushort expectedCommandId,
        bool phase,
        bool expectedPhase,
        EnaDeviceAttributes deviceAttributes)
    {
        IsInitialized = isInitialized;
        Status = status;
        DeviceAttributesStatus = deviceAttributesStatus;
        CompletionStatus = completionStatus;
        InputLength = inputLength;
        CommandId = commandId;
        ExpectedCommandId = expectedCommandId;
        Phase = phase;
        ExpectedPhase = expectedPhase;
        DeviceAttributes = deviceAttributes;
    }

    public EnaAdminCommandPlanStatus Status { get; }

    public EnaDeviceAttributesStatus DeviceAttributesStatus { get; }

    public EnaAdminCompletionStatus CompletionStatus { get; }

    public int InputLength { get; }

    public ushort CommandId { get; }

    public ushort ExpectedCommandId { get; }

    public bool Phase { get; }

    public bool ExpectedPhase { get; }

    public EnaDeviceAttributes DeviceAttributes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaAdminCommandPlanStatus.Success;

    internal static EnaAdminCommandCompletionValidationResult FromDeviceAttributes(
        EnaAdminCommandPlanStatus status,
        EnaDeviceAttributesParseResult parseResult)
    {
        return new EnaAdminCommandCompletionValidationResult(
            isInitialized: true,
            status,
            parseResult.Status,
            parseResult.CompletionStatus,
            parseResult.InputLength,
            parseResult.CommandId,
            parseResult.ExpectedCommandId,
            parseResult.Phase,
            parseResult.ExpectedPhase,
            parseResult.Attributes);
    }
}

public readonly struct EnaAdminCommandPlan
{
    private EnaAdminCommandPlan(
        ushort commandId,
        bool submissionPhase,
        bool expectedCompletionPhase,
        EnaAdminFeatureId featureId,
        uint timeoutTicks)
    {
        CommandId = commandId;
        SubmissionPhase = submissionPhase;
        ExpectedCompletionCommandId = commandId;
        ExpectedCompletionPhase = expectedCompletionPhase;
        FeatureId = featureId;
        TimeoutTicks = timeoutTicks;
    }

    public const uint MinimumTimeoutTicks = 1_000;
    public const bool PerformsMmio = false;
    public const bool AllocatesDmaMemory = false;
    public const bool RingsDoorbell = false;
    public const bool ConfiguresInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool ExecutesAdminCommand = false;

    public EnaAdminOpcode Opcode => EnaAdminOpcode.GetFeature;

    public EnaAdminFeatureId FeatureId { get; }

    public ushort CommandId { get; }

    public bool SubmissionPhase { get; }

    public ushort ExpectedCompletionCommandId { get; }

    public bool ExpectedCompletionPhase { get; }

    public uint TimeoutTicks { get; }

    public int SubmissionDescriptorLength =>
        EnaAdminDescriptors.GetFeatureCommandByteLength;

    public int CompletionDescriptorLength =>
        EnaDeviceAttributesParser.ResponseByteLength;

    public static EnaAdminCommandPlanResult CreateGetFeatureDeviceAttributes(
        ushort commandId,
        bool submissionPhase,
        bool expectedCompletionPhase,
        uint timeoutTicks)
    {
        return CreateGetFeature(
            commandId,
            submissionPhase,
            expectedCompletionPhase,
            EnaAdminFeatureId.DeviceAttributes,
            timeoutTicks);
    }

    public static EnaAdminCommandPlanResult CreateGetFeature(
        ushort commandId,
        bool submissionPhase,
        bool expectedCompletionPhase,
        EnaAdminFeatureId featureId,
        uint timeoutTicks)
    {
        if (commandId > EnaAdminDescriptors.CommandIdMask)
        {
            return EnaAdminCommandPlanResult.Fail(
                EnaAdminCommandPlanStatus.CommandIdOutOfRange,
                commandId,
                submissionPhase,
                expectedCompletionPhase,
                featureId,
                timeoutTicks);
        }

        if (featureId != EnaAdminFeatureId.DeviceAttributes)
        {
            return EnaAdminCommandPlanResult.Fail(
                EnaAdminCommandPlanStatus.UnsupportedFeature,
                commandId,
                submissionPhase,
                expectedCompletionPhase,
                featureId,
                timeoutTicks);
        }

        if (timeoutTicks < MinimumTimeoutTicks)
        {
            return EnaAdminCommandPlanResult.Fail(
                EnaAdminCommandPlanStatus.TimeoutBudgetTooSmall,
                commandId,
                submissionPhase,
                expectedCompletionPhase,
                featureId,
                timeoutTicks);
        }

        return EnaAdminCommandPlanResult.Success(new EnaAdminCommandPlan(
            commandId,
            submissionPhase,
            expectedCompletionPhase,
            featureId,
            timeoutTicks));
    }

    public EnaAdminCommandSubmissionResult TryWriteSubmission(
        Span<byte> destination)
    {
        EnaAdminDescriptorStatus descriptorStatus =
            EnaAdminDescriptors.TryWriteGetFeatureCommand(
                destination,
                EnaAdminGetFeatureCommand.CreateDeviceAttributes(
                    CommandId,
                    SubmissionPhase),
                out int bytesWritten);

        return EnaAdminCommandSubmissionResult.Create(
            MapSubmissionStatus(descriptorStatus),
            descriptorStatus,
            destination.Length,
            bytesWritten);
    }

    public EnaAdminCommandCompletionValidationResult ValidateDeviceAttributesCompletion(
        ReadOnlySpan<byte> completion)
    {
        EnaDeviceAttributesParseResult parseResult =
            EnaDeviceAttributesParser.TryParse(
                completion,
                ExpectedCompletionCommandId,
                ExpectedCompletionPhase);

        return EnaAdminCommandCompletionValidationResult.FromDeviceAttributes(
            MapDeviceAttributesStatus(parseResult.Status),
            parseResult);
    }

    private static EnaAdminCommandPlanStatus MapSubmissionStatus(
        EnaAdminDescriptorStatus status)
    {
        return status switch
        {
            EnaAdminDescriptorStatus.Success => EnaAdminCommandPlanStatus.Success,
            EnaAdminDescriptorStatus.BufferTooSmall =>
                EnaAdminCommandPlanStatus.SubmissionBufferTooSmall,
            EnaAdminDescriptorStatus.CommandIdOutOfRange =>
                EnaAdminCommandPlanStatus.CommandIdOutOfRange,
            _ => EnaAdminCommandPlanStatus.DescriptorWriteFailed
        };
    }

    private static EnaAdminCommandPlanStatus MapDeviceAttributesStatus(
        EnaDeviceAttributesStatus status)
    {
        return status switch
        {
            EnaDeviceAttributesStatus.Success => EnaAdminCommandPlanStatus.Success,
            EnaDeviceAttributesStatus.BufferTooSmall =>
                EnaAdminCommandPlanStatus.CompletionBufferTooSmall,
            EnaDeviceAttributesStatus.CommandIdOutOfRange =>
                EnaAdminCommandPlanStatus.CommandIdOutOfRange,
            EnaDeviceAttributesStatus.CommandIdMismatch =>
                EnaAdminCommandPlanStatus.CompletionCommandIdMismatch,
            EnaDeviceAttributesStatus.PhaseMismatch =>
                EnaAdminCommandPlanStatus.CompletionPhaseMismatch,
            EnaDeviceAttributesStatus.CompletionStatusNotSuccess =>
                EnaAdminCommandPlanStatus.CompletionStatusNotSuccess,
            _ => EnaAdminCommandPlanStatus.DeviceAttributesRejected
        };
    }
}
