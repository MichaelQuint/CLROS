namespace OS.Cloud;
public enum AwsImdsRequestPlanStatus
{
    Success = 0,
    InvalidTokenTtl = 1,
    EmptyToken = 2,
    TokenTooLong = 3,
    EmptyMetadataPath = 4,
    MetadataPathTooLong = 5,
    UnsafeMetadataPath = 6
}
