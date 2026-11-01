namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

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
    public const int DeviceAttributesMinimumLength = (MaxMtuOffset - DeviceAttributesOffset) + sizeof(uint);
    public const uint MinimumAddressWidth = 32;
    public const uint MaximumAddressWidth = 64;
    public const uint MinimumMtu = 576;
    public const uint MaximumMtu = 65535;
    public const bool PerformsMmio = false;
    public const bool AllocatesDmaMemory = false;
    public const bool ExecutesAdminCommand = false;
    public static EnaDeviceAttributesParseResult TryParse(ReadOnlySpan<byte> response, ushort expectedCommandId, bool expectedPhase)
    {
        if (response.Length < ResponseByteLength)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.BufferTooSmall, response.Length, expectedCommandId: expectedCommandId, expectedPhase: expectedPhase);
        }

        EnaAdminDescriptorStatus completionReadStatus = EnaAdminDescriptors.TryReadCompletionEntry(response.Slice(CompletionHeaderOffset, ResponseByteLength), out EnaAdminAcqEntry completion);
        if (completionReadStatus == EnaAdminDescriptorStatus.BufferTooSmall)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.BufferTooSmall, response.Length, expectedCommandId: expectedCommandId, expectedPhase: expectedPhase);
        }

        EnaAdminAcqCommonDescriptor common = completion.CommonDescriptor;
        if (expectedCommandId > EnaAdminDescriptors.CommandIdMask)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.CommandIdOutOfRange, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        EnaAdminDescriptorStatus completionStatus = EnaAdminDescriptors.ValidateSuccessfulCompletion(completion, expectedCommandId, expectedPhase);
        if (completionStatus != EnaAdminDescriptorStatus.Success)
        {
            return EnaDeviceAttributesParseResult.Fail(MapCompletionStatus(completionStatus), response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        uint physicalAddressWidth = EndianPrimitives.ReadUInt32LittleEndian(response.Slice(PhysicalAddressWidthOffset, sizeof(uint)));
        if (!IsValidAddressWidth(physicalAddressWidth))
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.InvalidPhysicalAddressWidth, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        uint virtualAddressWidth = EndianPrimitives.ReadUInt32LittleEndian(response.Slice(VirtualAddressWidthOffset, sizeof(uint)));
        if (!IsValidAddressWidth(virtualAddressWidth))
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.InvalidVirtualAddressWidth, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        MacAddress macAddress = new MacAddress(response[MacAddressOffset], response[MacAddressOffset + 1], response[MacAddressOffset + 2], response[MacAddressOffset + 3], response[MacAddressOffset + 4], response[MacAddressOffset + 5]);
        if (macAddress.IsZero)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.ZeroMacAddress, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        if (macAddress.IsBroadcast)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.BroadcastMacAddress, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        uint maxMtu = EndianPrimitives.ReadUInt32LittleEndian(response.Slice(MaxMtuOffset, sizeof(uint)));
        if (maxMtu < MinimumMtu || maxMtu > MaximumMtu)
        {
            return EnaDeviceAttributesParseResult.Fail(EnaDeviceAttributesStatus.InvalidMtu, response.Length, common.CompletionStatus, common.CommandId, expectedCommandId, common.Phase, expectedPhase);
        }

        EnaDeviceAttributes attributes = new EnaDeviceAttributes(common.CommandId, common.Phase, common.CompletionStatus, common.ExtendedStatus, common.SubmissionQueueHeadIndex, EnaAdminFeatureId.DeviceAttributes, EndianPrimitives.ReadUInt32LittleEndian(response.Slice(ImplementationIdOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(response.Slice(DeviceVersionOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(response.Slice(SupportedFeaturesOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(response.Slice(CapabilitiesOffset, sizeof(uint))), physicalAddressWidth, virtualAddressWidth, macAddress, maxMtu);
        return EnaDeviceAttributesParseResult.Success(response.Length, attributes);
    }

    private static EnaDeviceAttributesStatus MapCompletionStatus(EnaAdminDescriptorStatus status)
    {
        return status switch
        {
            EnaAdminDescriptorStatus.CommandIdOutOfRange => EnaDeviceAttributesStatus.CommandIdOutOfRange,
            EnaAdminDescriptorStatus.CommandIdMismatch => EnaDeviceAttributesStatus.CommandIdMismatch,
            EnaAdminDescriptorStatus.PhaseMismatch => EnaDeviceAttributesStatus.PhaseMismatch,
            EnaAdminDescriptorStatus.CompletionStatusNotSuccess => EnaDeviceAttributesStatus.CompletionStatusNotSuccess,
            _ => EnaDeviceAttributesStatus.CompletionStatusNotSuccess
        };
    }

    private static bool IsValidAddressWidth(uint addressWidth)
    {
        return addressWidth >= MinimumAddressWidth && addressWidth <= MaximumAddressWidth;
    }
}
