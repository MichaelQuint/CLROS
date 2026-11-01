namespace OS.Boot;
/// <summary>
/// Immutable coordinator result. Success means the requested host-modeled
/// checkpoint is internally consistent; only the final checkpoint satisfies
/// <see cref="Succeeded"/>.
/// </summary>

public readonly struct M01LoadedImageStackIntegrationResult
{
    private readonly M01LoadedImageStackStatus _status;
    private readonly M01LoadedImageStackCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly M01LoadedImageStackEvidence _evidence;
    private readonly M01LoadedImageStackTransferTarget _transferTarget;
    private M01LoadedImageStackIntegrationResult(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint, M01LoadedImageStackEvidence evidence, M01LoadedImageStackTransferTarget transferTarget)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _evidence = evidence;
        _transferTarget = transferTarget;
    }

    public M01LoadedImageStackStatus Status => _status;
    public M01LoadedImageStackCheckpoint LastCompletedCheckpoint => _checkpoint;
    public M01LoadedImageStackEvidence Evidence => _evidence;
    public M01LoadedImageStackTransferTarget TransferTarget => _transferTarget;
    public bool IsPrepared => ValidateProgress(M01LoadedImageStackCheckpoint.SwitchArmed);
    public bool IsTransferRecorded => ValidateProgress(M01LoadedImageStackCheckpoint.TransferRecorded);
    public bool Succeeded => ValidateProgress(M01LoadedImageStackCheckpoint.ContinuationRspContained);

    internal static M01LoadedImageStackIntegrationResult Success(M01LoadedImageStackCheckpoint checkpoint, M01LoadedImageStackEvidence evidence, M01LoadedImageStackTransferTarget transferTarget = default)
    {
        return new M01LoadedImageStackIntegrationResult(M01LoadedImageStackStatus.Success, checkpoint, evidence, transferTarget);
    }

    internal static M01LoadedImageStackIntegrationResult Fail(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint, M01LoadedImageStackEvidence evidence)
    {
        return new M01LoadedImageStackIntegrationResult(status, checkpoint, evidence, default);
    }

    private bool ValidateProgress(M01LoadedImageStackCheckpoint requiredCheckpoint)
    {
        OwnedBootStackSwitchPlan switchPlan = _evidence.SwitchPlanResultValue.Plan;
        if (_status != M01LoadedImageStackStatus.Success || _checkpoint != requiredCheckpoint || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0 || _reserved5 != 0 || !_evidence.LoadedImageCaptureValue.Succeeded || !_evidence.LoadedImageVirtualMappingValue.IsValidFor(_evidence.LoadedImageCaptureValue) || !_evidence.StageOneAddressMappingValue.IsValidFor(_evidence.CodeAllocationValue, _evidence.DataAllocationValue, _evidence.MapLedgerEvidenceValue.RefreshedMap) || _evidence.AllocationValidationStatusValue != OwnedBootStackSwitchStatus.Success || _evidence.AllocationAddressStatusValue != AddressValueStatus.Success || _evidence.InvalidAllocationRecordIndexValue != OwnedBootStackSwitchPlan.NoIndex || !_evidence.AllocationEvidenceValue.IsWellFormed || !_evidence.MapLedgerEvidenceValue.IsWellFormedFor(_evidence.AllocationEvidenceValue) || !_evidence.ReservationValidationValue.Succeeded || _evidence.ReservationValidationValue.ReservationCount != _evidence.MapLedgerEvidenceValue.LedgerCount || _evidence.ReservationValidationValue.AvailableReservationCapacity != _evidence.MapLedgerEvidenceValue.LedgerCapacity || !_evidence.SwitchPlanResultValue.Succeeded || !_evidence.StubVerificationValue.Succeeded || _evidence.StubVerificationValue.Operation != OwnedBootStackSwitchByteOperation.Verify || !_evidence.SwitchStateResultValue.Succeeded || _evidence.StaticStateAddressValue == 0 || _evidence.StaticStateCapacityValue < M01LoadedImageStackStaticState.RequiredStorageBytes || !RegionEquals(_evidence.CodeAllocationValue, switchPlan.CodeAllocation) || !RegionEquals(_evidence.DataAllocationValue, switchPlan.DataAllocation) || !_evidence.StubReservationValue.Equals(switchPlan.StubReservation) || !_evidence.GuardReservationValue.Equals(switchPlan.StackLayout.GuardReservation) || !_evidence.StackReservationValue.Equals(switchPlan.StackLayout.StackReservation) || (_evidence.StubByteModeValue == M01StageOneStubByteMode.EmitAndVerify && !_evidence.StubEmissionValue.Plan.Equals(switchPlan)) || !_evidence.StubVerificationValue.Plan.Equals(switchPlan) || !_evidence.SwitchStateResultValue.Plan.Equals(switchPlan) || !LayoutTopologyEquals(_evidence.StackLayoutResultValue.Layout, switchPlan.StackLayout) || switchPlan.StateStorageStart.TryGetRawValueForUefiBoundary(out ulong plannedStateAddress) != AddressValueStatus.Success || plannedStateAddress != _evidence.StaticStateAddressValue + M01LoadedImageStackStaticState.SwitchStateStorageOffset)
        {
            return false;
        }

        if (switchPlan.Handoff.StorageStart.TryGetRawValueForUefiBoundary(out ulong plannedHandoffAddress) != AddressValueStatus.Success || plannedHandoffAddress != _evidence.StaticStateAddressValue + M01LoadedImageStackStaticState.HandoffStorageOffset)
        {
            return false;
        }

        if (_evidence.StubByteModeValue != M01StageOneStubByteMode.EmitAndVerify || !_evidence.StubEmissionValue.Succeeded || _evidence.StubEmissionValue.Operation != OwnedBootStackSwitchByteOperation.Emit)
        {
            return false;
        }

        if (requiredCheckpoint == M01LoadedImageStackCheckpoint.SwitchArmed)
        {
            return _evidence.StackLayoutResultValue.Succeeded && _evidence.StackLayoutResultValue.CurrentLifetime == OwnedBootStackLifetime.Ready && _evidence.SwitchStateResultValue.CurrentState == OwnedBootStackSwitchState.Armed && !_transferTarget.Entry.IsValid;
        }

        if (requiredCheckpoint == M01LoadedImageStackCheckpoint.TransferRecorded)
        {
            return _evidence.StackLayoutResultValue.Succeeded && _evidence.StackLayoutResultValue.CurrentLifetime == OwnedBootStackLifetime.Ready && _evidence.SwitchStateResultValue.CurrentState == OwnedBootStackSwitchState.TransferRecorded && _transferTarget.IsValidFor(_evidence.SwitchPlanResultValue.Plan);
        }

        return _evidence.StackLayoutResultValue.Succeeded && _evidence.StackLayoutResultValue.CurrentLifetime == OwnedBootStackLifetime.Active && _evidence.SwitchStateResultValue.CurrentState == OwnedBootStackSwitchState.ContinuationObserved && _evidence.ObservedRspValue.IsValid && _evidence.ObservedRspValue.Equals(_evidence.SwitchPlanResultValue.Plan.ExpectedContinuationStackPointer) && _transferTarget.IsValidFor(_evidence.SwitchPlanResultValue.Plan);
    }

    private static bool RegionEquals(BootMemoryRegionRecord left, BootMemoryRegionRecord right)
    {
        return left.UefiType == right.UefiType && left.PhysicalStart == right.PhysicalStart && left.VirtualStart == right.VirtualStart && left.PageCount == right.PageCount && left.Attribute == right.Attribute && left.ByteLength == right.ByteLength && left.PhysicalEndExclusive == right.PhysicalEndExclusive;
    }

    private static bool LayoutTopologyEquals(OwnedBootStackLayout left, OwnedBootStackLayout right)
    {
        return left.BackingStart.Equals(right.BackingStart) && left.BackingEndExclusive.Equals(right.BackingEndExclusive) && left.BackingPageCount == right.BackingPageCount && left.BackingByteLength == right.BackingByteLength && left.GuardReservation.Equals(right.GuardReservation) && left.StackReservation.Equals(right.StackReservation) && left.PhysicalStackTop.Equals(right.PhysicalStackTop);
    }
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
