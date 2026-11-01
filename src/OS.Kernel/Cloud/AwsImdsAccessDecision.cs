namespace OS.Cloud;
public readonly struct AwsImdsAccessDecision
{
    private AwsImdsAccessDecision(AwsImdsAccessDecisionStatus status, AwsImdsAppRequestKind requestKind, bool supervisorMediated)
    {
        Status = status;
        RequestKind = requestKind;
        SupervisorMediated = supervisorMediated;
    }

    public AwsImdsAccessDecisionStatus Status { get; }
    public AwsImdsAppRequestKind RequestKind { get; }
    public bool SupervisorMediated { get; }
    public bool Allowed => Status == AwsImdsAccessDecisionStatus.Allowed;

    public static AwsImdsAccessDecision Allow(AwsImdsAppRequestKind requestKind, bool supervisorMediated)
    {
        return new AwsImdsAccessDecision(AwsImdsAccessDecisionStatus.Allowed, requestKind, supervisorMediated);
    }

    public static AwsImdsAccessDecision Deny(AwsImdsAccessDecisionStatus status, AwsImdsAppRequestKind requestKind, bool supervisorMediated)
    {
        return new AwsImdsAccessDecision(status, requestKind, supervisorMediated);
    }
}
