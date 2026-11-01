namespace OS.Drivers;
public readonly struct EnaAdminCommandCompletionValidationResult
{
    private EnaAdminCommandCompletionValidationResult(bool isInitialized, EnaAdminCommandPlanStatus status, EnaDeviceAttributesStatus deviceAttributesStatus, EnaAdminCompletionStatus completionStatus, int inputLength, ushort commandId, ushort expectedCommandId, bool phase, bool expectedPhase, EnaDeviceAttributes deviceAttributes)
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

    internal static EnaAdminCommandCompletionValidationResult FromDeviceAttributes(EnaAdminCommandPlanStatus status, EnaDeviceAttributesParseResult parseResult)
    {
        return new EnaAdminCommandCompletionValidationResult(isInitialized: true, status, parseResult.Status, parseResult.CompletionStatus, parseResult.InputLength, parseResult.CommandId, parseResult.ExpectedCommandId, parseResult.Phase, parseResult.ExpectedPhase, parseResult.Attributes);
    }
}
