namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum EnaDeviceAttributesStatus
{
    Success = 0,
    BufferTooSmall = 1,
    CommandIdOutOfRange = 2,
    CommandIdMismatch = 3,
    PhaseMismatch = 4,
    CompletionStatusNotSuccess = 5,
    ZeroMacAddress = 6,
    BroadcastMacAddress = 7,
    InvalidMtu = 8,
    InvalidPhysicalAddressWidth = 9,
    InvalidVirtualAddressWidth = 10
}

public readonly struct EnaDeviceAttributes
{
    public EnaDeviceAttributes(
        ushort commandId,
        bool phase,
        EnaAdminCompletionStatus completionStatus,
        ushort extendedStatus,
        ushort submissionQueueHeadIndex,
        EnaAdminFeatureId featureId,
        uint implementationId,
        uint deviceVersion,
        uint supportedFeatures,
        uint capabilities,
        uint physicalAddressWidth,
        uint virtualAddressWidth,
        MacAddress macAddress,
        uint maxMtu)
    {
        CommandId = commandId;
        Phase = phase;
        CompletionStatus = completionStatus;
        ExtendedStatus = extendedStatus;
        SubmissionQueueHeadIndex = submissionQueueHeadIndex;
        FeatureId = featureId;
        ImplementationId = implementationId;
        DeviceVersion = deviceVersion;
        SupportedFeatures = supportedFeatures;
        Capabilities = capabilities;
        PhysicalAddressWidth = physicalAddressWidth;
        VirtualAddressWidth = virtualAddressWidth;
        MacAddress = macAddress;
        MaxMtu = maxMtu;
    }

    public ushort CommandId { get; }

    public bool Phase { get; }

    public EnaAdminCompletionStatus CompletionStatus { get; }

    public ushort ExtendedStatus { get; }

    public ushort SubmissionQueueHeadIndex { get; }

    public EnaAdminFeatureId FeatureId { get; }

    public uint ImplementationId { get; }

    public uint DeviceVersion { get; }

    public uint SupportedFeatures { get; }

    public uint Capabilities { get; }

    public uint PhysicalAddressWidth { get; }

    public uint VirtualAddressWidth { get; }

    public MacAddress MacAddress { get; }

    public uint MaxMtu { get; }
}

public readonly struct EnaDeviceAttributesParseResult
{
    private EnaDeviceAttributesParseResult(
        bool isInitialized,
        EnaDeviceAttributesStatus status,
        int inputLength,
        int bytesConsumed,
        EnaAdminCompletionStatus completionStatus,
        ushort commandId,
        ushort expectedCommandId,
        bool phase,
        bool expectedPhase,
        EnaDeviceAttributes attributes)
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

    internal static EnaDeviceAttributesParseResult Success(
        int inputLength,
        EnaDeviceAttributes attributes)
    {
        return new EnaDeviceAttributesParseResult(
            isInitialized: true,
            EnaDeviceAttributesStatus.Success,
            inputLength,
            EnaDeviceAttributesParser.ResponseByteLength,
            attributes.CompletionStatus,
            attributes.CommandId,
            attributes.CommandId,
            attributes.Phase,
            attributes.Phase,
            attributes);
    }

    internal static EnaDeviceAttributesParseResult Fail(
        EnaDeviceAttributesStatus status,
        int inputLength,
        EnaAdminCompletionStatus completionStatus = default,
        ushort commandId = 0,
        ushort expectedCommandId = 0,
        bool phase = false,
        bool expectedPhase = false)
    {
        return new EnaDeviceAttributesParseResult(
            isInitialized: true,
            status,
            inputLength,
            0,
            completionStatus,
            commandId,
            expectedCommandId,
            phase,
            expectedPhase,
            default);
    }
}

public static class EnaDeviceAttributesParser
{
    public const int ResponseByteLength = EnaAdminDescriptors.GetFeatureResponseByteLength;
    public const int CompletionHeaderOffset = 0;
    public const int CompletionHeaderLength = EnaAdminDescriptors.AcqCommonDescriptorByteLength;
    public const int DeviceAttributesOffset = CompletionHeaderOffset + CompletionHeaderLength;
    public const int ImplementationIdOffset = DeviceAttributesOffset;
    public const int DeviceVersionOffset = ImplementationIdOffset + sizeof(uint);
    public const int SupportedFeaturesOffset = DeviceVersionOffset + sizeof(uint);
    public const int CapabilitiesOffset = SupportedFeaturesOffset + sizeof(uint);
    public const int PhysicalAddressWidthOffset = CapabilitiesOffset + sizeof(uint);
    public const int VirtualAddressWidthOffset = PhysicalAddressWidthOffset + sizeof(uint);
    public const int MacAddressOffset = VirtualAddressWidthOffset + sizeof(uint);
    public const int MacAddressLength = MacAddress.Length;
    public const int ReservedAfterMacOffset = MacAddressOffset + MacAddressLength;
    public const int ReservedAfterMacLength = sizeof(ushort);
    public const int MaxMtuOffset = ReservedAfterMacOffset + ReservedAfterMacLength;
    public const int DeviceAttributesMinimumLength =
        (MaxMtuOffset - DeviceAttributesOffset) + sizeof(uint);

    public const uint MinimumAddressWidth = 32;
    public const uint MaximumAddressWidth = 64;
    public const uint MinimumMtu = 576;
    public const uint MaximumMtu = 65535;
    public const bool PerformsMmio = false;
    public const bool AllocatesDmaMemory = false;
    public const bool ExecutesAdminCommand = false;

    public static EnaDeviceAttributesParseResult TryParse(
        ReadOnlySpan<byte> response,
        ushort expectedCommandId,
        bool expectedPhase)
    {
        if (response.Length < ResponseByteLength)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.BufferTooSmall,
                response.Length,
                expectedCommandId: expectedCommandId,
                expectedPhase: expectedPhase);
        }

        EnaAdminDescriptorStatus completionReadStatus =
            EnaAdminDescriptors.TryReadCompletionEntry(
                response.Slice(CompletionHeaderOffset, ResponseByteLength),
                out EnaAdminAcqEntry completion);
        if (completionReadStatus == EnaAdminDescriptorStatus.BufferTooSmall)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.BufferTooSmall,
                response.Length,
                expectedCommandId: expectedCommandId,
                expectedPhase: expectedPhase);
        }

        EnaAdminAcqCommonDescriptor common = completion.CommonDescriptor;
        if (expectedCommandId > EnaAdminDescriptors.CommandIdMask)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.CommandIdOutOfRange,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        EnaAdminDescriptorStatus completionStatus =
            EnaAdminDescriptors.ValidateSuccessfulCompletion(
                completion,
                expectedCommandId,
                expectedPhase);
        if (completionStatus != EnaAdminDescriptorStatus.Success)
        {
            return EnaDeviceAttributesParseResult.Fail(
                MapCompletionStatus(completionStatus),
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        uint physicalAddressWidth = EndianPrimitives.ReadUInt32LittleEndian(
            response.Slice(PhysicalAddressWidthOffset, sizeof(uint)));
        if (!IsValidAddressWidth(physicalAddressWidth))
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.InvalidPhysicalAddressWidth,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        uint virtualAddressWidth = EndianPrimitives.ReadUInt32LittleEndian(
            response.Slice(VirtualAddressWidthOffset, sizeof(uint)));
        if (!IsValidAddressWidth(virtualAddressWidth))
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.InvalidVirtualAddressWidth,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        MacAddress macAddress = new MacAddress(
            response[MacAddressOffset],
            response[MacAddressOffset + 1],
            response[MacAddressOffset + 2],
            response[MacAddressOffset + 3],
            response[MacAddressOffset + 4],
            response[MacAddressOffset + 5]);
        if (macAddress.IsZero)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.ZeroMacAddress,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        if (macAddress.IsBroadcast)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.BroadcastMacAddress,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        uint maxMtu = EndianPrimitives.ReadUInt32LittleEndian(
            response.Slice(MaxMtuOffset, sizeof(uint)));
        if (maxMtu < MinimumMtu || maxMtu > MaximumMtu)
        {
            return EnaDeviceAttributesParseResult.Fail(
                EnaDeviceAttributesStatus.InvalidMtu,
                response.Length,
                common.CompletionStatus,
                common.CommandId,
                expectedCommandId,
                common.Phase,
                expectedPhase);
        }

        EnaDeviceAttributes attributes = new EnaDeviceAttributes(
            common.CommandId,
            common.Phase,
            common.CompletionStatus,
            common.ExtendedStatus,
            common.SubmissionQueueHeadIndex,
            EnaAdminFeatureId.DeviceAttributes,
            EndianPrimitives.ReadUInt32LittleEndian(
                response.Slice(ImplementationIdOffset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(
                response.Slice(DeviceVersionOffset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(
                response.Slice(SupportedFeaturesOffset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(
                response.Slice(CapabilitiesOffset, sizeof(uint))),
            physicalAddressWidth,
            virtualAddressWidth,
            macAddress,
            maxMtu);

        return EnaDeviceAttributesParseResult.Success(response.Length, attributes);
    }

    private static EnaDeviceAttributesStatus MapCompletionStatus(
        EnaAdminDescriptorStatus status)
    {
        return status switch
        {
            EnaAdminDescriptorStatus.CommandIdOutOfRange =>
                EnaDeviceAttributesStatus.CommandIdOutOfRange,
            EnaAdminDescriptorStatus.CommandIdMismatch =>
                EnaDeviceAttributesStatus.CommandIdMismatch,
            EnaAdminDescriptorStatus.PhaseMismatch =>
                EnaDeviceAttributesStatus.PhaseMismatch,
            EnaAdminDescriptorStatus.CompletionStatusNotSuccess =>
                EnaDeviceAttributesStatus.CompletionStatusNotSuccess,
            _ => EnaDeviceAttributesStatus.CompletionStatusNotSuccess
        };
    }

    private static bool IsValidAddressWidth(uint addressWidth)
    {
        return addressWidth >= MinimumAddressWidth &&
            addressWidth <= MaximumAddressWidth;
    }

}
