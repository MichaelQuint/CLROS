namespace OS.Cloud;
public static class AwsImdsV2RequestPlanner
{
    public const string PlatformHost = "169.254.169.254";
    public const ushort PlatformPort = 80;
    public const string TokenPath = "/latest/api/token";
    public const string MetadataPathPrefix = "/latest/meta-data/";
    public const string UserDataPath = "/latest/user-data";
    public const string TokenTtlHeaderName = "X-aws-ec2-metadata-token-ttl-seconds";
    public const string TokenHeaderName = "X-aws-ec2-metadata-token";
    public const uint MaxTokenTtlSeconds = 21600;
    public const int MaxTokenChars = 8192;
    public const int MaxMetadataPathChars = 512;
    public static AwsImdsRequestPlanResult CreateTokenRequest(uint ttlSeconds)
    {
        if (ttlSeconds == 0 || ttlSeconds > MaxTokenTtlSeconds)
        {
            return AwsImdsRequestPlanResult.Fail(AwsImdsRequestPlanStatus.InvalidTokenTtl);
        }

        return AwsImdsRequestPlanResult.Success(new AwsImdsV2HttpRequestPlan("PUT", PlatformHost, PlatformPort, TokenPath, TokenTtlHeaderName, ttlSeconds.ToString()));
    }

    public static AwsImdsRequestPlanResult CreateMetadataGetRequest(string? metadataPath, string? token)
    {
        AwsImdsRequestPlanStatus tokenStatus = ValidateToken(token);
        if (tokenStatus != AwsImdsRequestPlanStatus.Success)
        {
            return AwsImdsRequestPlanResult.Fail(tokenStatus);
        }

        AwsImdsRequestPlanStatus pathStatus = ValidateMetadataPath(metadataPath);
        if (pathStatus != AwsImdsRequestPlanStatus.Success)
        {
            return AwsImdsRequestPlanResult.Fail(pathStatus);
        }

        return AwsImdsRequestPlanResult.Success(new AwsImdsV2HttpRequestPlan("GET", PlatformHost, PlatformPort, MetadataPathPrefix + metadataPath, TokenHeaderName, token!));
    }

    public static AwsImdsRequestPlanResult CreateUserDataGetRequest(string? token)
    {
        AwsImdsRequestPlanStatus tokenStatus = ValidateToken(token);
        if (tokenStatus != AwsImdsRequestPlanStatus.Success)
        {
            return AwsImdsRequestPlanResult.Fail(tokenStatus);
        }

        return AwsImdsRequestPlanResult.Success(new AwsImdsV2HttpRequestPlan("GET", PlatformHost, PlatformPort, UserDataPath, TokenHeaderName, token!));
    }

    private static AwsImdsRequestPlanStatus ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return AwsImdsRequestPlanStatus.EmptyToken;
        }

        if (token.Length > MaxTokenChars)
        {
            return AwsImdsRequestPlanStatus.TokenTooLong;
        }

        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (c <= 0x20 || c > 0x7e)
            {
                return AwsImdsRequestPlanStatus.EmptyToken;
            }
        }

        return AwsImdsRequestPlanStatus.Success;
    }

    private static AwsImdsRequestPlanStatus ValidateMetadataPath(string? metadataPath)
    {
        if (string.IsNullOrWhiteSpace(metadataPath))
        {
            return AwsImdsRequestPlanStatus.EmptyMetadataPath;
        }

        if (metadataPath.Length > MaxMetadataPathChars)
        {
            return AwsImdsRequestPlanStatus.MetadataPathTooLong;
        }

        if (metadataPath[0] == '/' || metadataPath.Contains("..", StringComparison.Ordinal) || metadataPath.Contains("//", StringComparison.Ordinal) || metadataPath.Contains("://", StringComparison.Ordinal))
        {
            return AwsImdsRequestPlanStatus.UnsafeMetadataPath;
        }

        for (int i = 0; i < metadataPath.Length; i++)
        {
            char c = metadataPath[i];
            if (c <= 0x20 || c > 0x7e || c == '\\' || c == '?' || c == '#')
            {
                return AwsImdsRequestPlanStatus.UnsafeMetadataPath;
            }
        }

        return AwsImdsRequestPlanStatus.Success;
    }
}
