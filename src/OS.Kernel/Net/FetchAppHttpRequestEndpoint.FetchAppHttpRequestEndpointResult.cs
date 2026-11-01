using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;
public readonly struct FetchAppHttpRequestEndpointResult
{
    private const uint InitializedFlag = 1;
    private const uint UrlConfiguredFlag = 1u << 1;
    private const uint HostConfiguredFlag = 1u << 2;
    private const uint PortConfiguredFlag = 1u << 3;
    private const uint PathConfiguredFlag = 1u << 4;
    private FetchAppHttpRequestEndpointResult(FetchAppHttpRequestEndpointStatus status, ushort port, int hostBytesWritten, int pathBytesWritten, int requiredHostBytes, int requiredPathBytes, int availableHostBytes, int availablePathBytes, uint flags)
    {
        Status = status;
        Port = port;
        HostBytesWritten = hostBytesWritten;
        PathBytesWritten = pathBytesWritten;
        RequiredHostBytes = requiredHostBytes;
        RequiredPathBytes = requiredPathBytes;
        AvailableHostBytes = availableHostBytes;
        AvailablePathBytes = availablePathBytes;
        Flags = flags;
    }

    public FetchAppHttpRequestEndpointStatus Status { get; }
    public ushort Port { get; }
    public int HostBytesWritten { get; }
    public int PathBytesWritten { get; }
    public int RequiredHostBytes { get; }
    public int RequiredPathBytes { get; }
    public int AvailableHostBytes { get; }
    public int AvailablePathBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool HasConfiguredUrl => (Flags & UrlConfiguredFlag) != 0;
    public bool HasConfiguredHost => (Flags & HostConfiguredFlag) != 0;
    public bool HasConfiguredPort => (Flags & PortConfiguredFlag) != 0;
    public bool HasConfiguredPath => (Flags & PathConfiguredFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpRequestEndpointStatus.Success && Port >= FetchAppHttpRequestEndpoint.MinimumPort && HostBytesWritten == RequiredHostBytes && PathBytesWritten == RequiredPathBytes && RequiredHostBytes > 0 && RequiredHostBytes <= FetchAppHttpRequestEndpoint.MaximumHostLength && RequiredPathBytes > 0 && RequiredPathBytes <= FetchAppHttpRequestEndpoint.MaximumPathLength;

    public static FetchAppHttpRequestEndpointResult Create(FetchAppHttpRequestEndpointStatus status, ushort port, int hostBytesWritten, int pathBytesWritten, int requiredHostBytes, int requiredPathBytes, int availableHostBytes, int availablePathBytes, bool hasConfiguredUrl, bool hasConfiguredHost, bool hasConfiguredPort, bool hasConfiguredPath)
    {
        uint flags = InitializedFlag;
        if (hasConfiguredUrl)
        {
            flags |= UrlConfiguredFlag;
        }

        if (hasConfiguredHost)
        {
            flags |= HostConfiguredFlag;
        }

        if (hasConfiguredPort)
        {
            flags |= PortConfiguredFlag;
        }

        if (hasConfiguredPath)
        {
            flags |= PathConfiguredFlag;
        }

        return new FetchAppHttpRequestEndpointResult(status, port, hostBytesWritten, pathBytesWritten, requiredHostBytes, requiredPathBytes, availableHostBytes, availablePathBytes, flags);
    }
}
