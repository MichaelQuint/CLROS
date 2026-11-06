namespace OS.Cloud;

public static class AwsImdsAppMetadataPolicyPlanner
{
    public static AwsImdsAccessDecision Evaluate(AwsImdsAppRequestKind requestKind, bool supervisorMediated, AwsImdsAppMetadataPolicy policy)
    {
        if (!supervisorMediated)
        {
            return AwsImdsAccessDecision.Deny(AwsImdsAccessDecisionStatus.SupervisorRequired, requestKind, supervisorMediated);
        }

        return requestKind switch
        {
            AwsImdsAppRequestKind.Metadata when policy.AllowMetadata => AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.Metadata => AwsImdsAccessDecision.Deny(AwsImdsAccessDecisionStatus.MetadataDisabled, requestKind, supervisorMediated),
            AwsImdsAppRequestKind.UserData when policy.AllowMetadata && policy.AllowUserData => AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.UserData => AwsImdsAccessDecision.Deny(AwsImdsAccessDecisionStatus.UserDataDisabled, requestKind, supervisorMediated),
            AwsImdsAppRequestKind.IamRoleCredentials when policy.AllowMetadata && policy.AllowIamRoleCredentials => AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.IamRoleCredentials => AwsImdsAccessDecision.Deny(AwsImdsAccessDecisionStatus.IamCredentialsDisabled, requestKind, supervisorMediated),
            _ => AwsImdsAccessDecision.Deny(AwsImdsAccessDecisionStatus.UnsupportedRequestKind, requestKind, supervisorMediated)
        };
    }
}
