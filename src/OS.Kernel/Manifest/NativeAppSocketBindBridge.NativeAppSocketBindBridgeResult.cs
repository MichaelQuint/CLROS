using OS.Pal;

namespace OS.Manifest;
public readonly struct NativeAppSocketBindBridgeResult
{
    private NativeAppSocketBindBridgeResult(NativeAppSocketBindBridgeStatus status, NativeAppSocketBindPolicyResult policy, OSHostStatus socketStatus, OSHostSocketHandle socket, bool isInitialized)
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

    public static NativeAppSocketBindBridgeResult Create(NativeAppSocketBindBridgeStatus status, NativeAppSocketBindPolicyResult policy, OSHostStatus socketStatus, OSHostSocketHandle socket)
    {
        return new NativeAppSocketBindBridgeResult(status, policy, socketStatus, socket, isInitialized: true);
    }
}
