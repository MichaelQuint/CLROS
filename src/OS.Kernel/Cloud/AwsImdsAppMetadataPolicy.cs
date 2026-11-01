namespace OS.Cloud;
public readonly struct AwsImdsAppMetadataPolicy
{
    public AwsImdsAppMetadataPolicy(bool allowMetadata, bool allowUserData, bool allowIamRoleCredentials)
    {
        AllowMetadata = allowMetadata;
        AllowUserData = allowUserData;
        AllowIamRoleCredentials = allowIamRoleCredentials;
    }

    public bool AllowMetadata { get; }
    public bool AllowUserData { get; }
    public bool AllowIamRoleCredentials { get; }
    public static AwsImdsAppMetadataPolicy DenyAll => new AwsImdsAppMetadataPolicy(false, false, false);
    public static AwsImdsAppMetadataPolicy MetadataOnly => new AwsImdsAppMetadataPolicy(true, false, false);
}
