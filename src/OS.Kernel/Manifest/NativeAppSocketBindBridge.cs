using OS.Pal;

namespace OS.Manifest;

public enum NativeAppSocketBindBridgeStatus
{
    Success = 0,
    PolicyRejected = 1,
    MissingSocketTable = 2,
    SocketBindFailed = 3
}

public readonly struct NativeAppSocketBindBridgeResult
{
    private NativeAppSocketBindBridgeResult(
        NativeAppSocketBindBridgeStatus status,
        NativeAppSocketBindPolicyResult policy,
        OSHostStatus socketStatus,
        OSHostSocketHandle socket,
        bool isInitialized)
    {
        Status = status;
        Policy = policy;
        SocketStatus = socketStatus;
        Socket = socket;
        IsInitialized = isInitialized;
    }

    public NativeAppSocketBindBridgeStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppSocketBindBridgeStatus.Success;

    public NativeAppSocketBindPolicyResult Policy { get; }

    public NativeAppSocketBindPolicyStatus PolicyStatus => Policy.Status;

    public NativeAppLaunchStatus LaunchStatus => Policy.LaunchStatus;

    public OSHostStatus SocketStatus { get; }

    public OSHostSocketHandle Socket { get; }

    public static NativeAppSocketBindBridgeResult Create(
        NativeAppSocketBindBridgeStatus status,
        NativeAppSocketBindPolicyResult policy,
        OSHostStatus socketStatus,
        OSHostSocketHandle socket)
    {
        return new NativeAppSocketBindBridgeResult(
            status,
            policy,
            socketStatus,
            socket,
            isInitialized: true);
    }
}

public static class NativeAppSocketBindBridge
{
    public const bool UsesMockSocketTable = true;
    public const bool CallsPolicyFirst = true;
    public const bool ExportsPalFunctions = false;
    public const bool OpensLiveSockets = false;
    public const bool StartsKestrel = false;

    public static NativeAppSocketBindBridgeStatus BindManifestApprovedEndpoint(
        ReadOnlySpan<byte> manifestSource,
        NativeAppLaunchPlanResult launchPlan,
        OSHostSocketTable? table,
        OSHostSocketHandle socket,
        OSHostIpv4Endpoint endpoint,
        out NativeAppSocketBindBridgeResult result)
    {
        NativeAppSocketBindPolicyStatus policyStatus = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            manifestSource,
            launchPlan,
            endpoint,
            out NativeAppSocketBindPolicyResult policy);
        if (policyStatus != NativeAppSocketBindPolicyStatus.Success)
        {
            result = NativeAppSocketBindBridgeResult.Create(
                NativeAppSocketBindBridgeStatus.PolicyRejected,
                policy,
                OSHostStatus.AccessDenied,
                socket);
            return result.Status;
        }

        if (table is null)
        {
            result = NativeAppSocketBindBridgeResult.Create(
                NativeAppSocketBindBridgeStatus.MissingSocketTable,
                policy,
                OSHostStatus.InvalidArgument,
                socket);
            return result.Status;
        }

        OSHostStatus bindStatus = table.Bind(socket, endpoint);
        if (bindStatus != OSHostStatus.Ok)
        {
            result = NativeAppSocketBindBridgeResult.Create(
                NativeAppSocketBindBridgeStatus.SocketBindFailed,
                policy,
                bindStatus,
                socket);
            return result.Status;
        }

        result = NativeAppSocketBindBridgeResult.Create(
            NativeAppSocketBindBridgeStatus.Success,
            policy,
            bindStatus,
            socket);
        return result.Status;
    }
}
