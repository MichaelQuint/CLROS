using OS.Pal;

namespace OS.Manifest;
public static class NativeAppSocketBindBridge
{
    public const bool UsesMockSocketTable = true;
    public const bool CallsPolicyFirst = true;
    public const bool ExportsPalFunctions = false;
    public const bool OpensLiveSockets = false;
    public const bool StartsKestrel = false;
    public static NativeAppSocketBindBridgeStatus BindManifestApprovedEndpoint(ReadOnlySpan<byte> manifestSource, NativeAppLaunchPlanResult launchPlan, OSHostSocketTable? table, OSHostSocketHandle socket, OSHostIpv4Endpoint endpoint, out NativeAppSocketBindBridgeResult result)
    {
        NativeAppSocketBindPolicyStatus policyStatus = NativeAppSocketBindPolicy.ValidateBindEndpoint(manifestSource, launchPlan, endpoint, out NativeAppSocketBindPolicyResult policy);
        if (policyStatus != NativeAppSocketBindPolicyStatus.Success)
        {
            result = NativeAppSocketBindBridgeResult.Create(NativeAppSocketBindBridgeStatus.PolicyRejected, policy, OSHostStatus.AccessDenied, socket);
            return result.Status;
        }

        if (table is null)
        {
            result = NativeAppSocketBindBridgeResult.Create(NativeAppSocketBindBridgeStatus.MissingSocketTable, policy, OSHostStatus.InvalidArgument, socket);
            return result.Status;
        }

        OSHostStatus bindStatus = table.Bind(socket, endpoint);
        if (bindStatus != OSHostStatus.Ok)
        {
            result = NativeAppSocketBindBridgeResult.Create(NativeAppSocketBindBridgeStatus.SocketBindFailed, policy, bindStatus, socket);
            return result.Status;
        }

        result = NativeAppSocketBindBridgeResult.Create(NativeAppSocketBindBridgeStatus.Success, policy, bindStatus, socket);
        return result.Status;
    }
}
