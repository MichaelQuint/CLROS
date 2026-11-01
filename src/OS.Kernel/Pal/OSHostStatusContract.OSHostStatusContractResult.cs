using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostStatusContractResult
{
    private const uint InitializedFlag = 1;
    private const uint AllowedFlag = 2;
    public readonly OSHostStatus Status;
    public readonly OSHostAbiStatusDomain Domain;
    public readonly OSHostStatus CandidateStatus;
    public readonly OSHostStatusCategory Category;
    public readonly ulong AllowedStatusMask;
    public readonly uint Flags;
    public OSHostStatusContractResult(OSHostStatus status, OSHostAbiStatusDomain domain, OSHostStatus candidateStatus, OSHostStatusCategory category, ulong allowedStatusMask, bool allowed, uint flags = InitializedFlag)
    {
        Status = status;
        Domain = domain;
        CandidateStatus = candidateStatus;
        Category = category;
        AllowedStatusMask = allowedStatusMask;
        Flags = allowed ? flags | AllowedFlag : flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool IsAllowed => (Flags & AllowedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && IsAllowed;
}
