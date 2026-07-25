using System.Runtime.CompilerServices;
using OS.Cloud;

internal static class AwsImdsV2RequestPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("cloud.aws.imds plans token request", ref failures, TestPlansTokenRequest);
        Run("cloud.aws.imds plans metadata get with token", ref failures, TestPlansMetadataGetWithToken);
        Run("cloud.aws.imds rejects unsafe metadata request", ref failures, TestRejectsUnsafeMetadataRequest);
        Run("cloud.aws.imds denies app by default", ref failures, TestDeniesAppByDefault);
        Run("cloud.aws.imds allows supervisor mediated request", ref failures, TestAllowsSupervisorMediatedRequest);

        if (failures != 0)
        {
            throw new InvalidOperationException("AWS IMDSv2 host tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestPlansTokenRequest()
    {
        AwsImdsRequestPlanResult result =
            AwsImdsV2RequestPlanner.CreateTokenRequest(21600);
        AwsImdsRequestPlanResult zero =
            AwsImdsV2RequestPlanner.CreateTokenRequest(0);
        AwsImdsRequestPlanResult oversized =
            AwsImdsV2RequestPlanner.CreateTokenRequest(AwsImdsV2RequestPlanner.MaxTokenTtlSeconds + 1);

        return result.Succeeded
            && result.Request.Method == "PUT"
            && result.Request.Host == AwsImdsV2RequestPlanner.PlatformHost
            && result.Request.Port == AwsImdsV2RequestPlanner.PlatformPort
            && result.Request.PathAndQuery == AwsImdsV2RequestPlanner.TokenPath
            && result.Request.HeaderName == AwsImdsV2RequestPlanner.TokenTtlHeaderName
            && result.Request.HeaderValue == "21600"
            && zero.Status == AwsImdsRequestPlanStatus.InvalidTokenTtl
            && oversized.Status == AwsImdsRequestPlanStatus.InvalidTokenTtl;
    }

    private static bool TestPlansMetadataGetWithToken()
    {
        AwsImdsRequestPlanResult metadata =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest("placement/region", "token-123");
        AwsImdsRequestPlanResult userData =
            AwsImdsV2RequestPlanner.CreateUserDataGetRequest("token-123");

        return metadata.Succeeded
            && metadata.Request.Method == "GET"
            && metadata.Request.PathAndQuery == "/latest/meta-data/placement/region"
            && metadata.Request.HeaderName == AwsImdsV2RequestPlanner.TokenHeaderName
            && metadata.Request.HeaderValue == "token-123"
            && userData.Succeeded
            && userData.Request.Method == "GET"
            && userData.Request.PathAndQuery == AwsImdsV2RequestPlanner.UserDataPath
            && userData.Request.HeaderName == AwsImdsV2RequestPlanner.TokenHeaderName;
    }

    private static bool TestRejectsUnsafeMetadataRequest()
    {
        AwsImdsRequestPlanResult emptyToken =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest("instance-id", string.Empty);
        AwsImdsRequestPlanResult absolutePath =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest("/instance-id", "token-123");
        AwsImdsRequestPlanResult traversal =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest("../iam/security-credentials", "token-123");
        AwsImdsRequestPlanResult absoluteUrl =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest("http://example.invalid/latest/meta-data", "token-123");
        AwsImdsRequestPlanResult oversized =
            AwsImdsV2RequestPlanner.CreateMetadataGetRequest(
                new string('a', AwsImdsV2RequestPlanner.MaxMetadataPathChars + 1),
                "token-123");

        return emptyToken.Status == AwsImdsRequestPlanStatus.EmptyToken
            && absolutePath.Status == AwsImdsRequestPlanStatus.UnsafeMetadataPath
            && traversal.Status == AwsImdsRequestPlanStatus.UnsafeMetadataPath
            && absoluteUrl.Status == AwsImdsRequestPlanStatus.UnsafeMetadataPath
            && oversized.Status == AwsImdsRequestPlanStatus.MetadataPathTooLong;
    }

    private static bool TestDeniesAppByDefault()
    {
        AwsImdsAccessDecision direct =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.Metadata,
                supervisorMediated: false,
                AwsImdsAppMetadataPolicy.DenyAll);
        AwsImdsAccessDecision mediated =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.Metadata,
                supervisorMediated: true,
                AwsImdsAppMetadataPolicy.DenyAll);
        AwsImdsAccessDecision iam =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.IamRoleCredentials,
                supervisorMediated: true,
                AwsImdsAppMetadataPolicy.MetadataOnly);

        return !direct.Allowed
            && direct.Status == AwsImdsAccessDecisionStatus.SupervisorRequired
            && !mediated.Allowed
            && mediated.Status == AwsImdsAccessDecisionStatus.MetadataDisabled
            && !iam.Allowed
            && iam.Status == AwsImdsAccessDecisionStatus.IamCredentialsDisabled;
    }

    private static bool TestAllowsSupervisorMediatedRequest()
    {
        AwsImdsAppMetadataPolicy policy =
            new AwsImdsAppMetadataPolicy(
                allowMetadata: true,
                allowUserData: true,
                allowIamRoleCredentials: false);
        AwsImdsAccessDecision metadata =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.Metadata,
                supervisorMediated: true,
                policy);
        AwsImdsAccessDecision userData =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.UserData,
                supervisorMediated: true,
                policy);
        AwsImdsAccessDecision direct =
            AwsImdsAppMetadataPolicyPlanner.Evaluate(
                AwsImdsAppRequestKind.UserData,
                supervisorMediated: false,
                policy);

        return metadata.Allowed
            && userData.Allowed
            && !direct.Allowed
            && direct.Status == AwsImdsAccessDecisionStatus.SupervisorRequired;
    }
}
