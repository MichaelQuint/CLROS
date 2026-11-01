namespace OS.Boot;
/// <summary>
/// Deterministic reverse-order release plan for pre-entry or safely restored
/// failures. This type does not call FreePages.
/// </summary>

public readonly struct M01LivePagingCleanupPlan
{
    public const ulong StructureSize = 48;
    public const ulong NoIndex = ~0UL;
    private readonly M01LivePagingCleanupStatus _status;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _completedAllocationCount;
    private readonly ulong _releaseCount;
    private readonly ulong _firstReleaseIndex;
    private readonly ulong _secondReleaseIndex;
    private readonly ulong _thirdReleaseIndex;
    internal M01LivePagingCleanupPlan(M01LivePagingCleanupStatus status, ulong completedAllocationCount, ulong releaseCount, ulong firstReleaseIndex, ulong secondReleaseIndex, ulong thirdReleaseIndex)
    {
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _completedAllocationCount = completedAllocationCount;
        _releaseCount = releaseCount;
        _firstReleaseIndex = firstReleaseIndex;
        _secondReleaseIndex = secondReleaseIndex;
        _thirdReleaseIndex = thirdReleaseIndex;
    }

    public M01LivePagingCleanupStatus Status => _status;
    public ulong CompletedAllocationCount => _completedAllocationCount;
    public ulong ReleaseCount => _releaseCount;
    public ulong FirstReleaseIndex => _firstReleaseIndex;
    public ulong SecondReleaseIndex => _secondReleaseIndex;
    public ulong ThirdReleaseIndex => _thirdReleaseIndex;
    public bool Succeeded => _status == M01LivePagingCleanupStatus.Success && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _completedAllocationCount == _releaseCount && _releaseCount <= M01LivePagingIntegration.RequiredAllocationRecordCount && (_releaseCount == 0 ? _firstReleaseIndex == NoIndex && _secondReleaseIndex == NoIndex && _thirdReleaseIndex == NoIndex : _releaseCount == 1 ? _firstReleaseIndex == 0 && _secondReleaseIndex == NoIndex && _thirdReleaseIndex == NoIndex : _releaseCount == 2 ? _firstReleaseIndex == 1 && _secondReleaseIndex == 0 && _thirdReleaseIndex == NoIndex : _firstReleaseIndex == 2 && _secondReleaseIndex == 1 && _thirdReleaseIndex == 0);
}
