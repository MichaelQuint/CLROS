namespace OS.Cloud;

public enum AwsImdsAccessDecisionStatus
{
    Allowed = 0,
    DeniedByDefault = 1,
    SupervisorRequired = 2,
    MetadataDisabled = 3,
    UserDataDisabled = 4,
    IamCredentialsDisabled = 5,
    UnsupportedRequestKind = 6
}
