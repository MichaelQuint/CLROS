namespace OS.Boot;
/// <summary>
/// Value-only evidence retained across the split prepare, transfer-record,
/// and continuation-audit host calls. Raw binding addresses are pre-transfer
/// diagnostic evidence only and this aggregate must not cross the stack
/// switch.
/// </summary>

public struct M01LoadedImageStackEvidence
{
    internal EfiLoadedImageRangeCapturePlan LoadedImageCaptureValue;
    internal M01LoadedImageVirtualMappingEvidence LoadedImageVirtualMappingValue;
    internal M01StageOneAddressMappingEvidence StageOneAddressMappingValue;
    internal M01StageOneAllocationEvidence AllocationEvidenceValue;
    internal M01StageOneMapLedgerEvidence MapLedgerEvidenceValue;
    internal BootMemoryRegionRecord CodeAllocationValue;
    internal BootMemoryRegionRecord DataAllocationValue;
    internal OwnedBootStackSwitchStatus AllocationValidationStatusValue;
    internal AddressValueStatus AllocationAddressStatusValue;
    internal ulong InvalidAllocationRecordIndexValue;
    internal OS.Kernel.Memory.BootReservedRange StubReservationValue;
    internal OS.Kernel.Memory.BootReservedRange GuardReservationValue;
    internal OS.Kernel.Memory.BootReservedRange StackReservationValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidationValue;
    internal OwnedBootStackLayoutResult StackLayoutResultValue;
    internal OwnedBootStackSwitchPlanResult SwitchPlanResultValue;
    internal OwnedBootStackSwitchBytePlanResult StubEmissionValue;
    internal OwnedBootStackSwitchBytePlanResult StubVerificationValue;
    internal OwnedBootStackSwitchStateResult SwitchStateResultValue;
    internal ulong StaticStateAddressValue;
    internal ulong StaticStateCapacityValue;
    internal VirtualAddress ObservedRspValue;
    internal M01StageOneStubByteMode StubByteModeValue;
    public EfiLoadedImageRangeCapturePlan LoadedImageCapture => LoadedImageCaptureValue;
    public M01LoadedImageVirtualMappingEvidence LoadedImageVirtualMapping => LoadedImageVirtualMappingValue;
    public M01StageOneAddressMappingEvidence StageOneAddressMapping => StageOneAddressMappingValue;
    public M01StageOneAllocationEvidence AllocationEvidence => AllocationEvidenceValue;
    public M01StageOneMapLedgerEvidence MapLedgerEvidence => MapLedgerEvidenceValue;
    public BootMemoryRegionRecord CodeAllocation => CodeAllocationValue;
    public BootMemoryRegionRecord DataAllocation => DataAllocationValue;
    public OwnedBootStackSwitchStatus AllocationValidationStatus => AllocationValidationStatusValue;
    public AddressValueStatus AllocationAddressStatus => AllocationAddressStatusValue;
    public ulong InvalidAllocationRecordIndex => InvalidAllocationRecordIndexValue;
    public OS.Kernel.Memory.BootReservedRange StubReservation => StubReservationValue;
    public OS.Kernel.Memory.BootReservedRange GuardReservation => GuardReservationValue;
    public OS.Kernel.Memory.BootReservedRange StackReservation => StackReservationValue;
    public OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidation => ReservationValidationValue;
    public OwnedBootStackLayoutResult StackLayoutResult => StackLayoutResultValue;
    public OwnedBootStackSwitchPlanResult SwitchPlanResult => SwitchPlanResultValue;
    public OwnedBootStackSwitchBytePlanResult StubEmission => StubEmissionValue;
    public OwnedBootStackSwitchBytePlanResult StubVerification => StubVerificationValue;
    public OwnedBootStackSwitchStateResult SwitchStateResult => SwitchStateResultValue;
    public ulong StaticStateAddress => StaticStateAddressValue;
    public ulong StaticStateCapacity => StaticStateCapacityValue;
    public VirtualAddress ObservedRsp => ObservedRspValue;
    public M01StageOneStubByteMode StubByteMode => StubByteModeValue;
}
