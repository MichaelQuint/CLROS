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

public readonly struct AwsImdsV2HttpRequestPlan
{
    public AwsImdsV2HttpRequestPlan(
        string method,
        string host,
        ushort port,
        string pathAndQuery,
        string headerName,
        string headerValue)
    {
        Method = method;
        Host = host;
        Port = port;
        PathAndQuery = pathAndQuery;
        HeaderName = headerName;
        HeaderValue = headerValue;
    }

    public string Method { get; }

    public string Host { get; }

    public ushort Port { get; }

    public string PathAndQuery { get; }

    public string HeaderName { get; }

    public string HeaderValue { get; }

    public bool HasHeader => HeaderName.Length != 0;
}

public readonly struct AwsImdsRequestPlanResult
{
    private AwsImdsRequestPlanResult(
        AwsImdsRequestPlanStatus status,
        AwsImdsV2HttpRequestPlan request)
    {
        Status = status;
        Request = request;
    }

    public AwsImdsRequestPlanStatus Status { get; }

    public AwsImdsV2HttpRequestPlan Request { get; }

    public bool Succeeded => Status == AwsImdsRequestPlanStatus.Success;

    public static AwsImdsRequestPlanResult Success(AwsImdsV2HttpRequestPlan request)
    {
        return new AwsImdsRequestPlanResult(AwsImdsRequestPlanStatus.Success, request);
    }

    public static AwsImdsRequestPlanResult Fail(AwsImdsRequestPlanStatus status)
    {
        return new AwsImdsRequestPlanResult(status, default);
    }
}

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

        return AwsImdsRequestPlanResult.Success(
            new AwsImdsV2HttpRequestPlan(
                "PUT",
                PlatformHost,
                PlatformPort,
                TokenPath,
                TokenTtlHeaderName,
                ttlSeconds.ToString()));
    }

    public static AwsImdsRequestPlanResult CreateMetadataGetRequest(
        string? metadataPath,
        string? token)
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

        return AwsImdsRequestPlanResult.Success(
            new AwsImdsV2HttpRequestPlan(
                "GET",
                PlatformHost,
                PlatformPort,
                MetadataPathPrefix + metadataPath,
                TokenHeaderName,
                token!));
    }

    public static AwsImdsRequestPlanResult CreateUserDataGetRequest(string? token)
    {
        AwsImdsRequestPlanStatus tokenStatus = ValidateToken(token);
        if (tokenStatus != AwsImdsRequestPlanStatus.Success)
        {
            return AwsImdsRequestPlanResult.Fail(tokenStatus);
        }

        return AwsImdsRequestPlanResult.Success(
            new AwsImdsV2HttpRequestPlan(
                "GET",
                PlatformHost,
                PlatformPort,
                UserDataPath,
                TokenHeaderName,
                token!));
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

        if (metadataPath[0] == '/'
            || metadataPath.Contains("..", StringComparison.Ordinal)
            || metadataPath.Contains("//", StringComparison.Ordinal)
            || metadataPath.Contains("://", StringComparison.Ordinal))
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

public enum AwsImdsAppRequestKind
{
    Metadata = 0,
    UserData = 1,
    IamRoleCredentials = 2
}

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

public readonly struct AwsImdsAppMetadataPolicy
{
    public AwsImdsAppMetadataPolicy(
        bool allowMetadata,
        bool allowUserData,
        bool allowIamRoleCredentials)
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

public readonly struct AwsImdsAccessDecision
{
    private AwsImdsAccessDecision(
        AwsImdsAccessDecisionStatus status,
        AwsImdsAppRequestKind requestKind,
        bool supervisorMediated)
    {
        Status = status;
        RequestKind = requestKind;
        SupervisorMediated = supervisorMediated;
    }

    public AwsImdsAccessDecisionStatus Status { get; }

    public AwsImdsAppRequestKind RequestKind { get; }

    public bool SupervisorMediated { get; }

    public bool Allowed => Status == AwsImdsAccessDecisionStatus.Allowed;

    public static AwsImdsAccessDecision Allow(
        AwsImdsAppRequestKind requestKind,
        bool supervisorMediated)
    {
        return new AwsImdsAccessDecision(
            AwsImdsAccessDecisionStatus.Allowed,
            requestKind,
            supervisorMediated);
    }

    public static AwsImdsAccessDecision Deny(
        AwsImdsAccessDecisionStatus status,
        AwsImdsAppRequestKind requestKind,
        bool supervisorMediated)
    {
        return new AwsImdsAccessDecision(status, requestKind, supervisorMediated);
    }
}

public static class AwsImdsAppMetadataPolicyPlanner
{
    public static AwsImdsAccessDecision Evaluate(
        AwsImdsAppRequestKind requestKind,
        bool supervisorMediated,
        AwsImdsAppMetadataPolicy policy)
    {
        if (!supervisorMediated)
        {
            return AwsImdsAccessDecision.Deny(
                AwsImdsAccessDecisionStatus.SupervisorRequired,
                requestKind,
                supervisorMediated);
        }

        return requestKind switch
        {
            AwsImdsAppRequestKind.Metadata when policy.AllowMetadata =>
                AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.Metadata =>
                AwsImdsAccessDecision.Deny(
                    AwsImdsAccessDecisionStatus.MetadataDisabled,
                    requestKind,
                    supervisorMediated),
            AwsImdsAppRequestKind.UserData when policy.AllowMetadata && policy.AllowUserData =>
                AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.UserData =>
                AwsImdsAccessDecision.Deny(
                    AwsImdsAccessDecisionStatus.UserDataDisabled,
                    requestKind,
                    supervisorMediated),
            AwsImdsAppRequestKind.IamRoleCredentials when policy.AllowMetadata && policy.AllowIamRoleCredentials =>
                AwsImdsAccessDecision.Allow(requestKind, supervisorMediated),
            AwsImdsAppRequestKind.IamRoleCredentials =>
                AwsImdsAccessDecision.Deny(
                    AwsImdsAccessDecisionStatus.IamCredentialsDisabled,
                    requestKind,
                    supervisorMediated),
            _ => AwsImdsAccessDecision.Deny(
                AwsImdsAccessDecisionStatus.UnsupportedRequestKind,
                requestKind,
                supervisorMediated)
        };
    }
}
