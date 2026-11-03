namespace OS.Drivers;

public readonly struct EnaPciCandidateSelectionResult
{
    private EnaPciCandidateSelectionResult(bool isInitialized, EnaPciCandidateSelectionStatus status, int inspectedDeviceCount, int candidateCount, int requiredCandidateCapacity, int failingSourceIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        InspectedDeviceCount = inspectedDeviceCount;
        CandidateCount = candidateCount;
        RequiredCandidateCapacity = requiredCandidateCapacity;
        FailingSourceIndex = failingSourceIndex;
    }

    public const int NoFailingSourceIndex = -1;
    public EnaPciCandidateSelectionStatus Status
    {
        get;
    }
    public int InspectedDeviceCount
    {
        get;
    }
    public int CandidateCount
    {
        get;
    }
    public int RequiredCandidateCapacity
    {
        get;
    }
    public int FailingSourceIndex
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaPciCandidateSelectionStatus.Success;

    public static EnaPciCandidateSelectionResult Success(int inspectedDeviceCount, int candidateCount)
    {
        return new EnaPciCandidateSelectionResult(isInitialized: true, EnaPciCandidateSelectionStatus.Success, inspectedDeviceCount, candidateCount, candidateCount, NoFailingSourceIndex);
    }

    public static EnaPciCandidateSelectionResult DestinationTooSmall(int inspectedDeviceCount, int candidateCount, int requiredCandidateCapacity, int failingSourceIndex)
    {
        return new EnaPciCandidateSelectionResult(isInitialized: true, EnaPciCandidateSelectionStatus.DestinationTooSmall, inspectedDeviceCount, candidateCount, requiredCandidateCapacity, failingSourceIndex);
    }
}
