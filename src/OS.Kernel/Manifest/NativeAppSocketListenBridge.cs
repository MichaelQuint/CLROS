using OS.Pal;

namespace OS.Manifest;
public static class NativeAppSocketListenBridge
{
    public const bool UsesMockSocketTable = true;
    public const bool CallsPolicyFirst = true;
    public const bool CreatesSocket = true;
    public const bool ExportsPalFunctions = false;
    public const bool OpensLiveSockets = false;
    public const bool WaitsOnScheduler = false;
    public const bool StartsKestrel = false;
    public static NativeAppSocketListenBridgeStatus CreateManifestApprovedListener(ReadOnlySpan<byte> manifestSource, NativeAppLaunchPlanResult launchPlan, OSHostSocketTable? table, OSHostIpv4Endpoint endpoint, int backlog, out NativeAppSocketListenBridgeResult result)
    {
        NativeAppSocketBindPolicyStatus policyStatus = NativeAppSocketBindPolicy.ValidateBindEndpoint(manifestSource, launchPlan, endpoint, out NativeAppSocketBindPolicyResult policy);
        if (policyStatus != NativeAppSocketBindPolicyStatus.Success)
        {
            result = CreateResult(NativeAppSocketListenBridgeStatus.PolicyRejected, policy, endpoint, backlog, createAttempted: false, createStatus: OSHostStatus.AccessDenied, bindAttempted: false, bindStatus: OSHostStatus.AccessDenied, listenAttempted: false, listenStatus: OSHostStatus.AccessDenied, socket: default);
            return result.Status;
        }

        if (table is null)
        {
            result = CreateResult(NativeAppSocketListenBridgeStatus.MissingSocketTable, policy, endpoint, backlog, createAttempted: false, createStatus: OSHostStatus.InvalidArgument, bindAttempted: false, bindStatus: OSHostStatus.InvalidArgument, listenAttempted: false, listenStatus: OSHostStatus.InvalidArgument, socket: default);
            return result.Status;
        }

        OSHostSocketCreateResult create = table.Create(CreateTcpSocketOptions());
        if (!create.Succeeded)
        {
            result = CreateResult(NativeAppSocketListenBridgeStatus.SocketCreateFailed, policy, endpoint, backlog, createAttempted: true, createStatus: create.Status, bindAttempted: false, bindStatus: OSHostStatus.InvalidArgument, listenAttempted: false, listenStatus: OSHostStatus.InvalidArgument, socket: create.Socket);
            return result.Status;
        }

        OSHostStatus bindStatus = table.Bind(create.Socket, endpoint);
        if (bindStatus != OSHostStatus.Ok)
        {
            result = CreateResult(NativeAppSocketListenBridgeStatus.SocketBindFailed, policy, endpoint, backlog, createAttempted: true, createStatus: create.Status, bindAttempted: true, bindStatus: bindStatus, listenAttempted: false, listenStatus: OSHostStatus.InvalidArgument, socket: create.Socket);
            return result.Status;
        }

        OSHostStatus listenStatus = table.Listen(create.Socket, backlog);
        if (listenStatus != OSHostStatus.Ok)
        {
            result = CreateResult(NativeAppSocketListenBridgeStatus.SocketListenFailed, policy, endpoint, backlog, createAttempted: true, createStatus: create.Status, bindAttempted: true, bindStatus: bindStatus, listenAttempted: true, listenStatus: listenStatus, socket: create.Socket);
            return result.Status;
        }

        result = CreateResult(NativeAppSocketListenBridgeStatus.Success, policy, endpoint, backlog, createAttempted: true, createStatus: create.Status, bindAttempted: true, bindStatus: bindStatus, listenAttempted: true, listenStatus: listenStatus, socket: create.Socket);
        return result.Status;
    }

    private static NativeAppSocketListenBridgeResult CreateResult(NativeAppSocketListenBridgeStatus status, NativeAppSocketBindPolicyResult policy, OSHostIpv4Endpoint endpoint, int backlog, bool createAttempted, OSHostStatus createStatus, bool bindAttempted, OSHostStatus bindStatus, bool listenAttempted, OSHostStatus listenStatus, OSHostSocketHandle socket)
    {
        return NativeAppSocketListenBridgeResult.Create(status, policy, endpoint, backlog, createAttempted, createStatus, bindAttempted, bindStatus, listenAttempted, listenStatus, socket);
    }

    private static OSHostSocketCreateOptions CreateTcpSocketOptions()
    {
        return new OSHostSocketCreateOptions(OSHostSocketConstants.DomainIpv4, OSHostSocketConstants.TypeStream, OSHostSocketConstants.ProtocolTcp, flags: 0, OSHostSocketTable.CreateOptionsVersion, reserved: 0);
    }
}
