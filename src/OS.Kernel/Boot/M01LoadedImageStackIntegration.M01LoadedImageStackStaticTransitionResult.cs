namespace OS.Boot;
/// <summary>
/// Carries the transition evidence needed by a future static continuation.
/// It is derived only from caller-owned static storage and contains no
/// allocation-table or reservation-ledger address.
/// </summary>

public readonly struct M01LoadedImageStackStaticTransitionResult
{
    private readonly M01LoadedImageStackStatus _status;
    private readonly M01LoadedImageStackCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly OwnedBootStackSwitchStateResult _stateResult;
    private readonly OwnedBootStackLayoutResult _layoutResult;
    private readonly M01LoadedImageStackTransferTarget _target;
    private M01LoadedImageStackStaticTransitionResult(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint, OwnedBootStackSwitchStateResult stateResult, OwnedBootStackLayoutResult layoutResult, M01LoadedImageStackTransferTarget target)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _stateResult = stateResult;
        _layoutResult = layoutResult;
        _target = target;
    }

    public M01LoadedImageStackStatus Status => _status;
    public M01LoadedImageStackCheckpoint Checkpoint => _checkpoint;
    public OwnedBootStackSwitchStateResult StateResult => _stateResult;
    public OwnedBootStackLayoutResult LayoutResult => _layoutResult;
    public M01LoadedImageStackTransferTarget TransferTarget => _target;
    public bool TransferRecorded => Validate(M01LoadedImageStackCheckpoint.TransferRecorded, OwnedBootStackSwitchState.TransferRecorded, OwnedBootStackLifetime.Ready);
    public bool ContinuationRspContained => Validate(M01LoadedImageStackCheckpoint.ContinuationRspContained, OwnedBootStackSwitchState.ContinuationObserved, OwnedBootStackLifetime.Active);

    internal static M01LoadedImageStackStaticTransitionResult Success(M01LoadedImageStackCheckpoint checkpoint, OwnedBootStackSwitchStateResult stateResult, OwnedBootStackLayoutResult layoutResult, M01LoadedImageStackTransferTarget target)
    {
        return new M01LoadedImageStackStaticTransitionResult(M01LoadedImageStackStatus.Success, checkpoint, stateResult, layoutResult, target);
    }

    internal static M01LoadedImageStackStaticTransitionResult Fail(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint, OwnedBootStackSwitchStateResult stateResult = default, OwnedBootStackLayoutResult layoutResult = default)
    {
        return new M01LoadedImageStackStaticTransitionResult(status, checkpoint, stateResult, layoutResult, default);
    }

    private bool Validate(M01LoadedImageStackCheckpoint checkpoint, OwnedBootStackSwitchState state, OwnedBootStackLifetime lifetime)
    {
        return _status == M01LoadedImageStackStatus.Success && _checkpoint == checkpoint && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _stateResult.Succeeded && _stateResult.CurrentState == state && _layoutResult.Succeeded && _layoutResult.CurrentLifetime == lifetime && _target.IsValidFor(_stateResult.Plan);
    }
}
