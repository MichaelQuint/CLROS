using OS.Pal;

namespace OS.Manifest;
public readonly struct NativeAppSocketBindPolicyResult
{
    private NativeAppSocketBindPolicyResult(NativeAppSocketBindPolicyStatus status, NativeAppLaunchStatus launchStatus, ushort expectedPort, ushort actualPort, byte expectedAddress0, byte expectedAddress1, byte expectedAddress2, byte expectedAddress3, byte actualAddress0, byte actualAddress1, byte actualAddress2, byte actualAddress3, bool wildcard, bool isInitialized)
    {
        Status = status;
        LaunchStatus = launchStatus;
        ExpectedPort = expectedPort;
        ActualPort = actualPort;
        ExpectedAddress0 = expectedAddress0;
        ExpectedAddress1 = expectedAddress1;
        ExpectedAddress2 = expectedAddress2;
        ExpectedAddress3 = expectedAddress3;
        ActualAddress0 = actualAddress0;
        ActualAddress1 = actualAddress1;
        ActualAddress2 = actualAddress2;
        ActualAddress3 = actualAddress3;
        Wildcard = wildcard;
        IsInitialized = isInitialized;
    }

    public NativeAppSocketBindPolicyStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppSocketBindPolicyStatus.Success;
    public NativeAppLaunchStatus LaunchStatus { get; }
    public ushort ExpectedPort { get; }
    public ushort ActualPort { get; }
    public byte ExpectedAddress0 { get; }
    public byte ExpectedAddress1 { get; }
    public byte ExpectedAddress2 { get; }
    public byte ExpectedAddress3 { get; }
    public byte ActualAddress0 { get; }
    public byte ActualAddress1 { get; }
    public byte ActualAddress2 { get; }
    public byte ActualAddress3 { get; }
    public bool Wildcard { get; }

    public static NativeAppSocketBindPolicyResult Create(NativeAppSocketBindPolicyStatus status, NativeAppLaunchStatus launchStatus, ushort expectedPort, ushort actualPort, byte expectedAddress0, byte expectedAddress1, byte expectedAddress2, byte expectedAddress3, OSHostIpv4Endpoint actualEndpoint, bool wildcard)
    {
        return new NativeAppSocketBindPolicyResult(status, launchStatus, expectedPort, actualPort, expectedAddress0, expectedAddress1, expectedAddress2, expectedAddress3, actualEndpoint.Address0, actualEndpoint.Address1, actualEndpoint.Address2, actualEndpoint.Address3, wildcard, isInitialized: true);
    }
}
