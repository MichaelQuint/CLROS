namespace OS.Drivers;
public readonly struct EnaAdminCommandSubmissionResult
{
    private EnaAdminCommandSubmissionResult(bool isInitialized, EnaAdminCommandPlanStatus status, EnaAdminDescriptorStatus descriptorStatus, int destinationLength, int bytesWritten)
    {
        IsInitialized = isInitialized;
        Status = status;
        DescriptorStatus = descriptorStatus;
        DestinationLength = destinationLength;
        BytesWritten = bytesWritten;
    }

    public EnaAdminCommandPlanStatus Status { get; }
    public EnaAdminDescriptorStatus DescriptorStatus { get; }
    public int DestinationLength { get; }
    public int BytesWritten { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == EnaAdminCommandPlanStatus.Success;

    internal static EnaAdminCommandSubmissionResult Create(EnaAdminCommandPlanStatus status, EnaAdminDescriptorStatus descriptorStatus, int destinationLength, int bytesWritten)
    {
        return new EnaAdminCommandSubmissionResult(isInitialized: true, status, descriptorStatus, destinationLength, bytesWritten);
    }
}
