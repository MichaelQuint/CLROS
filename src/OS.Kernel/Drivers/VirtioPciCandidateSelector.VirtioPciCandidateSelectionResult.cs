namespace OS.Drivers;

public readonly struct VirtioPciCandidateSelectionResult
{
    private VirtioPciCandidateSelectionResult(VirtioPciCandidateSelectionStatus status, int inspectedDeviceCount, int candidateCount, int requiredCandidateCapacity, int failingSourceIndex, bool isInitialized)
    {
        Status = status;
        InspectedDeviceCount = inspectedDeviceCount;
        CandidateCount = candidateCount;
        RequiredCandidateCapacity = requiredCandidateCapacity;
        FailingSourceIndex = failingSourceIndex;
        IsInitialized = isInitialized;
    }

    public const int NoFailingSourceIndex = -1;
    public VirtioPciCandidateSelectionStatus Status
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
    public bool Succeeded => IsInitialized && Status == VirtioPciCandidateSelectionStatus.Success;

    public static VirtioPciCandidateSelectionResult Success(int inspectedDeviceCount, int candidateCount)
    {
        return new VirtioPciCandidateSelectionResult(VirtioPciCandidateSelectionStatus.Success, inspectedDeviceCount, candidateCount, candidateCount, NoFailingSourceIndex, isInitialized: true);
    }

    public static VirtioPciCandidateSelectionResult DestinationTooSmall(int inspectedDeviceCount, int candidateCount, int requiredCandidateCapacity, int failingSourceIndex)
    {
        return new VirtioPciCandidateSelectionResult(VirtioPciCandidateSelectionStatus.DestinationTooSmall, inspectedDeviceCount, candidateCount, requiredCandidateCapacity, failingSourceIndex, isInitialized: true);
    }
}
