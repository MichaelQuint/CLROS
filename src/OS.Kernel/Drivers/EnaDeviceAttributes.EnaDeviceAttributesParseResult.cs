namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct EnaDeviceAttributesParseResult
{
    private EnaDeviceAttributesParseResult(bool isInitialized, EnaDeviceAttributesStatus status, int inputLength, int bytesConsumed, EnaAdminCompletionStatus completionStatus, ushort commandId, ushort expectedCommandId, bool phase, bool expectedPhase, EnaDeviceAttributes attributes)
    {
        IsInitialized = isInitialized;
        Status = status;
        InputLength = inputLength;
        BytesConsumed = bytesConsumed;
        CompletionStatus = completionStatus;
        CommandId = commandId;
        ExpectedCommandId = expectedCommandId;
        Phase = phase;
        ExpectedPhase = expectedPhase;
        Attributes = attributes;
    }

    public EnaDeviceAttributesStatus Status { get; }
    public int InputLength { get; }
    public int BytesConsumed { get; }
    public EnaAdminCompletionStatus CompletionStatus { get; }
    public ushort CommandId { get; }
    public ushort ExpectedCommandId { get; }
    public bool Phase { get; }
    public bool ExpectedPhase { get; }
    public EnaDeviceAttributes Attributes { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == EnaDeviceAttributesStatus.Success;

    internal static EnaDeviceAttributesParseResult Success(int inputLength, EnaDeviceAttributes attributes)
    {
        return new EnaDeviceAttributesParseResult(isInitialized: true, EnaDeviceAttributesStatus.Success, inputLength, EnaDeviceAttributesParser.ResponseByteLength, attributes.CompletionStatus, attributes.CommandId, attributes.CommandId, attributes.Phase, attributes.Phase, attributes);
    }

    internal static EnaDeviceAttributesParseResult Fail(EnaDeviceAttributesStatus status, int inputLength, EnaAdminCompletionStatus completionStatus = default, ushort commandId = 0, ushort expectedCommandId = 0, bool phase = false, bool expectedPhase = false)
    {
        return new EnaDeviceAttributesParseResult(isInitialized: true, status, inputLength, 0, completionStatus, commandId, expectedCommandId, phase, expectedPhase, default);
    }
}
