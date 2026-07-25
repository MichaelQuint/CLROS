using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public enum FetchAppHttpRequestEndpointStatus
{
    Success = 0,
    ManifestNotReady = 1,
    UnsupportedScheme = 2,
    MissingHost = 3,
    InvalidPort = 4,
    EmptyPath = 5,
    FieldTooLarge = 6,
    DestinationTooSmall = 7,
    InvalidUrl = 8
}

public readonly struct FetchAppHttpRequestEndpointResult
{
    private const uint InitializedFlag = 1;
    private const uint UrlConfiguredFlag = 1u << 1;
    private const uint HostConfiguredFlag = 1u << 2;
    private const uint PortConfiguredFlag = 1u << 3;
    private const uint PathConfiguredFlag = 1u << 4;

    private FetchAppHttpRequestEndpointResult(
        FetchAppHttpRequestEndpointStatus status,
        ushort port,
        int hostBytesWritten,
        int pathBytesWritten,
        int requiredHostBytes,
        int requiredPathBytes,
        int availableHostBytes,
        int availablePathBytes,
        uint flags)
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

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpRequestEndpointStatus.Success &&
        Port >= FetchAppHttpRequestEndpoint.MinimumPort &&
        HostBytesWritten == RequiredHostBytes &&
        PathBytesWritten == RequiredPathBytes &&
        RequiredHostBytes > 0 &&
        RequiredHostBytes <= FetchAppHttpRequestEndpoint.MaximumHostLength &&
        RequiredPathBytes > 0 &&
        RequiredPathBytes <= FetchAppHttpRequestEndpoint.MaximumPathLength;

    public static FetchAppHttpRequestEndpointResult Create(
        FetchAppHttpRequestEndpointStatus status,
        ushort port,
        int hostBytesWritten,
        int pathBytesWritten,
        int requiredHostBytes,
        int requiredPathBytes,
        int availableHostBytes,
        int availablePathBytes,
        bool hasConfiguredUrl,
        bool hasConfiguredHost,
        bool hasConfiguredPort,
        bool hasConfiguredPath)
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

        return new FetchAppHttpRequestEndpointResult(
            status,
            port,
            hostBytesWritten,
            pathBytesWritten,
            requiredHostBytes,
            requiredPathBytes,
            availableHostBytes,
            availablePathBytes,
            flags);
    }
}

public static class FetchAppHttpRequestEndpoint
{
    public const ushort DefaultPort = 80;
    public const ushort MinimumPort = 1;
    public const ushort MaximumPort = ushort.MaxValue;
    public const int MaximumHostLength = 64;
    public const int MaximumPathLength = 128;

    public static FetchAppHttpRequestEndpointResult FromManifest(
        in NativeManifestParseResult manifestParse,
        ReadOnlySpan<byte> manifestBytes,
        Span<byte> hostDestination,
        Span<byte> pathDestination)
    {
        if (!manifestParse.Succeeded)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.ManifestNotReady,
                DefaultPort,
                GetDefaultHost().Length,
                GetDefaultPath().Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl: false,
                hasConfiguredHost: false,
                hasConfiguredPort: false,
                hasConfiguredPath: false);
        }

        bool hasUrl = false;
        bool hasHost = false;
        bool hasPort = false;
        bool hasPath = false;
        ReadOnlySpan<byte> url = ReadOnlySpan<byte>.Empty;
        ReadOnlySpan<byte> host = GetDefaultHost();
        ReadOnlySpan<byte> port = ReadOnlySpan<byte>.Empty;
        ReadOnlySpan<byte> path = GetDefaultPath();

        NativeManifest manifest = manifestParse.Manifest;
        for (int index = 0; index < manifest.EnvironmentCount; index++)
        {
            NativeManifestEnvironmentEntry entry = manifest.GetEnvironmentEntry(index);
            if (!entry.TryGetKey(manifestBytes, out ReadOnlySpan<byte> key) ||
                !entry.TryGetValue(manifestBytes, out ReadOnlySpan<byte> value))
            {
                return Fail(
                    FetchAppHttpRequestEndpointStatus.ManifestNotReady,
                    DefaultPort,
                    host.Length,
                    path.Length,
                    hostDestination.Length,
                    pathDestination.Length,
                    hasUrl,
                    hasHost,
                    hasPort,
                    hasPath);
            }

            if (OS.Std.MemoryExtensions.SequenceEqual(key, "FETCH_HTTP_URL"u8))
            {
                hasUrl = true;
                url = value;
            }
            else if (OS.Std.MemoryExtensions.SequenceEqual(key, "FETCH_HTTP_HOST"u8))
            {
                hasHost = true;
                host = value;
            }
            else if (OS.Std.MemoryExtensions.SequenceEqual(key, "FETCH_HTTP_PORT"u8))
            {
                hasPort = true;
                port = value;
            }
            else if (OS.Std.MemoryExtensions.SequenceEqual(key, "FETCH_HTTP_PATH"u8))
            {
                hasPath = true;
                path = value;
            }
        }

        if (hasUrl)
        {
            return FromUrl(url, hostDestination, pathDestination);
        }

        return ValidateAndCopy(
            host,
            port,
            path,
            hostDestination,
            pathDestination,
            hasConfiguredUrl: false,
            hasConfiguredHost: hasHost,
            hasConfiguredPort: hasPort,
            hasConfiguredPath: hasPath);
    }

    private static FetchAppHttpRequestEndpointResult FromUrl(
        ReadOnlySpan<byte> url,
        Span<byte> hostDestination,
        Span<byte> pathDestination)
    {
        if (OS.Std.MemoryExtensions.StartsWith(url, "https://"u8))
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.UnsupportedScheme,
                DefaultPort,
                0,
                GetDefaultPath().Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl: true,
                hasConfiguredHost: false,
                hasConfiguredPort: false,
                hasConfiguredPath: false);
        }

        if (!OS.Std.MemoryExtensions.StartsWith(url, "http://"u8))
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.UnsupportedScheme,
                DefaultPort,
                0,
                GetDefaultPath().Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl: true,
                hasConfiguredHost: false,
                hasConfiguredPort: false,
                hasConfiguredPath: false);
        }

        ReadOnlySpan<byte> remainder = url["http://"u8.Length..];
        int slashIndex = OS.Std.MemoryExtensions.IndexOf(remainder, (byte)'/');
        ReadOnlySpan<byte> authority = slashIndex >= 0 ? remainder[..slashIndex] : remainder;
        ReadOnlySpan<byte> path = slashIndex >= 0 ? remainder[slashIndex..] : GetDefaultPath();
        if (slashIndex >= 0 && path.Length == 0)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.EmptyPath,
                DefaultPort,
                authority.Length,
                0,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl: true,
                hasConfiguredHost: false,
                hasConfiguredPort: false,
                hasConfiguredPath: false);
        }

        int colonIndex = OS.Std.MemoryExtensions.IndexOf(authority, (byte)':');
        ReadOnlySpan<byte> host = colonIndex >= 0 ? authority[..colonIndex] : authority;
        ReadOnlySpan<byte> port = colonIndex >= 0 ? authority[(colonIndex + 1)..] : ReadOnlySpan<byte>.Empty;
        return ValidateAndCopy(
            host,
            port,
            path,
            hostDestination,
            pathDestination,
            hasConfiguredUrl: true,
            hasConfiguredHost: false,
            hasConfiguredPort: colonIndex >= 0,
            hasConfiguredPath: slashIndex >= 0);
    }

    private static FetchAppHttpRequestEndpointResult ValidateAndCopy(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> portText,
        ReadOnlySpan<byte> path,
        Span<byte> hostDestination,
        Span<byte> pathDestination,
        bool hasConfiguredUrl,
        bool hasConfiguredHost,
        bool hasConfiguredPort,
        bool hasConfiguredPath)
    {
        ushort port = DefaultPort;
        if (host.Length == 0)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.MissingHost,
                port,
                host.Length,
                path.Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl,
                hasConfiguredHost,
                hasConfiguredPort,
                hasConfiguredPath);
        }

        if (path.Length == 0)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.EmptyPath,
                port,
                host.Length,
                path.Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl,
                hasConfiguredHost,
                hasConfiguredPort,
                hasConfiguredPath);
        }

        if (host.Length > MaximumHostLength || path.Length > MaximumPathLength)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.FieldTooLarge,
                port,
                host.Length,
                path.Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl,
                hasConfiguredHost,
                hasConfiguredPort,
                hasConfiguredPath);
        }

        if (hasConfiguredPort)
        {
            AsciiUInt32ParseResult parsedPort = AsciiDecimalParser.ParseUInt32(portText);
            if (!parsedPort.Succeeded ||
                parsedPort.Value < MinimumPort ||
                parsedPort.Value > MaximumPort)
            {
                return Fail(
                    FetchAppHttpRequestEndpointStatus.InvalidPort,
                    0,
                    host.Length,
                    path.Length,
                    hostDestination.Length,
                    pathDestination.Length,
                    hasConfiguredUrl,
                    hasConfiguredHost,
                    hasConfiguredPort,
                    hasConfiguredPath);
            }

            port = (ushort)parsedPort.Value;
        }

        if (hostDestination.Length < host.Length || pathDestination.Length < path.Length)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.DestinationTooSmall,
                port,
                host.Length,
                path.Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl,
                hasConfiguredHost,
                hasConfiguredPort,
                hasConfiguredPath);
        }

        BufferOperationResult hostCopy = BufferOperations.MemoryCopy(host, hostDestination);
        BufferOperationResult pathCopy = BufferOperations.MemoryCopy(path, pathDestination);
        if (!hostCopy.Succeeded || !pathCopy.Succeeded)
        {
            return Fail(
                FetchAppHttpRequestEndpointStatus.DestinationTooSmall,
                port,
                host.Length,
                path.Length,
                hostDestination.Length,
                pathDestination.Length,
                hasConfiguredUrl,
                hasConfiguredHost,
                hasConfiguredPort,
                hasConfiguredPath);
        }

        return FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.Success,
            port,
            host.Length,
            path.Length,
            host.Length,
            path.Length,
            hostDestination.Length,
            pathDestination.Length,
            hasConfiguredUrl,
            hasConfiguredHost,
            hasConfiguredPort,
            hasConfiguredPath);
    }

    private static FetchAppHttpRequestEndpointResult Fail(
        FetchAppHttpRequestEndpointStatus status,
        ushort port,
        int requiredHostBytes,
        int requiredPathBytes,
        int availableHostBytes,
        int availablePathBytes,
        bool hasConfiguredUrl,
        bool hasConfiguredHost,
        bool hasConfiguredPort,
        bool hasConfiguredPath)
    {
        return FetchAppHttpRequestEndpointResult.Create(
            status,
            port,
            hostBytesWritten: 0,
            pathBytesWritten: 0,
            requiredHostBytes,
            requiredPathBytes,
            availableHostBytes,
            availablePathBytes,
            hasConfiguredUrl,
            hasConfiguredHost,
            hasConfiguredPort,
            hasConfiguredPath);
    }

    private static ReadOnlySpan<byte> GetDefaultHost()
    {
        return "example.com"u8;
    }

    private static ReadOnlySpan<byte> GetDefaultPath()
    {
        return "/"u8;
    }

}
