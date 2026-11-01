namespace OS.Drivers;
public readonly struct EnaAdminCommandPlan
{
    private EnaAdminCommandPlan(ushort commandId, bool submissionPhase, bool expectedCompletionPhase, EnaAdminFeatureId featureId, uint timeoutTicks)
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
    public int SubmissionDescriptorLength => EnaAdminDescriptors.GetFeatureCommandByteLength;
    public int CompletionDescriptorLength => EnaDeviceAttributesParser.ResponseByteLength;

    public static EnaAdminCommandPlanResult CreateGetFeatureDeviceAttributes(ushort commandId, bool submissionPhase, bool expectedCompletionPhase, uint timeoutTicks)
    {
        return CreateGetFeature(commandId, submissionPhase, expectedCompletionPhase, EnaAdminFeatureId.DeviceAttributes, timeoutTicks);
    }

    public static EnaAdminCommandPlanResult CreateGetFeature(ushort commandId, bool submissionPhase, bool expectedCompletionPhase, EnaAdminFeatureId featureId, uint timeoutTicks)
    {
        if (commandId > EnaAdminDescriptors.CommandIdMask)
        {
            return EnaAdminCommandPlanResult.Fail(EnaAdminCommandPlanStatus.CommandIdOutOfRange, commandId, submissionPhase, expectedCompletionPhase, featureId, timeoutTicks);
        }

        if (featureId != EnaAdminFeatureId.DeviceAttributes)
        {
            return EnaAdminCommandPlanResult.Fail(EnaAdminCommandPlanStatus.UnsupportedFeature, commandId, submissionPhase, expectedCompletionPhase, featureId, timeoutTicks);
        }

        if (timeoutTicks < MinimumTimeoutTicks)
        {
            return EnaAdminCommandPlanResult.Fail(EnaAdminCommandPlanStatus.TimeoutBudgetTooSmall, commandId, submissionPhase, expectedCompletionPhase, featureId, timeoutTicks);
        }

        return EnaAdminCommandPlanResult.Success(new EnaAdminCommandPlan(commandId, submissionPhase, expectedCompletionPhase, featureId, timeoutTicks));
    }

    public EnaAdminCommandSubmissionResult TryWriteSubmission(Span<byte> destination)
    {
        EnaAdminDescriptorStatus descriptorStatus = EnaAdminDescriptors.TryWriteGetFeatureCommand(destination, EnaAdminGetFeatureCommand.CreateDeviceAttributes(CommandId, SubmissionPhase), out int bytesWritten);
        return EnaAdminCommandSubmissionResult.Create(MapSubmissionStatus(descriptorStatus), descriptorStatus, destination.Length, bytesWritten);
    }

    public EnaAdminCommandCompletionValidationResult ValidateDeviceAttributesCompletion(ReadOnlySpan<byte> completion)
    {
        EnaDeviceAttributesParseResult parseResult = EnaDeviceAttributesParser.TryParse(completion, ExpectedCompletionCommandId, ExpectedCompletionPhase);
        return EnaAdminCommandCompletionValidationResult.FromDeviceAttributes(MapDeviceAttributesStatus(parseResult.Status), parseResult);
    }

    private static EnaAdminCommandPlanStatus MapSubmissionStatus(EnaAdminDescriptorStatus status)
    {
        return status switch
        {
            EnaAdminDescriptorStatus.Success => EnaAdminCommandPlanStatus.Success,
            EnaAdminDescriptorStatus.BufferTooSmall => EnaAdminCommandPlanStatus.SubmissionBufferTooSmall,
            EnaAdminDescriptorStatus.CommandIdOutOfRange => EnaAdminCommandPlanStatus.CommandIdOutOfRange,
            _ => EnaAdminCommandPlanStatus.DescriptorWriteFailed
        };
    }

    private static EnaAdminCommandPlanStatus MapDeviceAttributesStatus(EnaDeviceAttributesStatus status)
    {
        return status switch
        {
            EnaDeviceAttributesStatus.Success => EnaAdminCommandPlanStatus.Success,
            EnaDeviceAttributesStatus.BufferTooSmall => EnaAdminCommandPlanStatus.CompletionBufferTooSmall,
            EnaDeviceAttributesStatus.CommandIdOutOfRange => EnaAdminCommandPlanStatus.CommandIdOutOfRange,
            EnaDeviceAttributesStatus.CommandIdMismatch => EnaAdminCommandPlanStatus.CompletionCommandIdMismatch,
            EnaDeviceAttributesStatus.PhaseMismatch => EnaAdminCommandPlanStatus.CompletionPhaseMismatch,
            EnaDeviceAttributesStatus.CompletionStatusNotSuccess => EnaAdminCommandPlanStatus.CompletionStatusNotSuccess,
            _ => EnaAdminCommandPlanStatus.DeviceAttributesRejected
        };
    }
}
