using OS.Pal;

namespace OS.Manifest;
public readonly struct NativeAppSocketListenBridgeResult
{
    private NativeAppSocketListenBridgeResult(NativeAppSocketListenBridgeStatus status, NativeAppSocketBindPolicyResult policy, OSHostIpv4Endpoint endpoint, int backlog, bool createAttempted, OSHostStatus createStatus, bool bindAttempted, OSHostStatus bindStatus, bool listenAttempted, OSHostStatus listenStatus, OSHostSocketHandle socket, bool isInitialized)
    {
        Status = status;
        Policy = policy;
        Endpoint = endpoint;
        Backlog = backlog;
        CreateAttempted = createAttempted;
        CreateStatus = createStatus;
        BindAttempted = bindAttempted;
        BindStatus = bindStatus;
        ListenAttempted = listenAttempted;
        ListenStatus = listenStatus;
        Socket = socket;
        IsInitialized = isInitialized;
    }

    public NativeAppSocketListenBridgeStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppSocketListenBridgeStatus.Success;
    public NativeAppSocketBindPolicyResult Policy { get; }
    public NativeAppSocketBindPolicyStatus PolicyStatus => Policy.Status;
    public NativeAppLaunchStatus LaunchStatus => Policy.LaunchStatus;
    public OSHostIpv4Endpoint Endpoint { get; }
    public int Backlog { get; }
    public bool CreateAttempted { get; }
    public OSHostStatus CreateStatus { get; }
    public bool BindAttempted { get; }
    public OSHostStatus BindStatus { get; }
    public bool ListenAttempted { get; }
    public OSHostStatus ListenStatus { get; }
    public OSHostSocketHandle Socket { get; }

    public static NativeAppSocketListenBridgeResult Create(NativeAppSocketListenBridgeStatus status, NativeAppSocketBindPolicyResult policy, OSHostIpv4Endpoint endpoint, int backlog, bool createAttempted, OSHostStatus createStatus, bool bindAttempted, OSHostStatus bindStatus, bool listenAttempted, OSHostStatus listenStatus, OSHostSocketHandle socket)
    {
        return new NativeAppSocketListenBridgeResult(status, policy, endpoint, backlog, createAttempted, createStatus, bindAttempted, bindStatus, listenAttempted, listenStatus, socket, isInitialized: true);
    }
}
