namespace OS.Kernel.Runtime;

public readonly struct NativeAotHelperLedgerEntry
{
    public NativeAotHelperLedgerEntry(NativeAotHelperArea area, NativeAotHelperStatus status, NativeAotCompletionGate completionGates, bool isEfiOnly, bool satisfiesKernelRuntimeCompletion, string helperSymbols, string evidence, string requiredContract, string allowedMilestoneStub, string testCoverage, string ownerSubsystem, string nextStep)
    {
        Area = area;
        Status = status;
        CompletionGates = completionGates;
        IsEfiOnly = isEfiOnly;
        SatisfiesKernelRuntimeCompletion = satisfiesKernelRuntimeCompletion;
        HelperSymbols = helperSymbols;
        Evidence = evidence;
        RequiredContract = requiredContract;
        AllowedMilestoneStub = allowedMilestoneStub;
        TestCoverage = testCoverage;
        OwnerSubsystem = ownerSubsystem;
        NextStep = nextStep;
    }

    public NativeAotHelperArea Area
    {
        get;
    }
    public NativeAotHelperStatus Status
    {
        get;
    }
    public NativeAotCompletionGate CompletionGates
    {
        get;
    }
    public bool IsEfiOnly
    {
        get;
    }
    public bool SatisfiesKernelRuntimeCompletion
    {
        get;
    }
    public string HelperSymbols
    {
        get;
    }
    public string Evidence
    {
        get;
    }
    public string RequiredContract
    {
        get;
    }
    public string AllowedMilestoneStub
    {
        get;
    }
    public string TestCoverage
    {
        get;
    }
    public string OwnerSubsystem
    {
        get;
    }
    public string NextStep
    {
        get;
    }
    public bool IsComplete => Status == NativeAotHelperStatus.Complete;

    public bool AppliesTo(NativeAotCompletionGate gate)
    {
        return gate != NativeAotCompletionGate.None && (CompletionGates & gate) == gate;
    }
}
