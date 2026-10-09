using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Net;

internal static class FetchAppHttpRequestEndpointBootSequenceTests
{

    [HostTest("boot.sequence fetch http endpoint ready", HostLane.Fast, 0)]
    private static bool TestReadyMarker()
    {
        FetchAppHttpRequestEndpointResult endpoint = ReadyEndpoint();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpRequestEndpointReadiness(
            endpoint,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpRequestEndpointReadyMarker(endpoint);

        return HostCheck.Result(endpoint.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_HTTP_ENDPOINT_READY status=Ready initialized=true endpoint=Success port=8080 hostBytes=14 pathBytes=8 requiredHostBytes=14 requiredPathBytes=8 availableHostBytes=64 availablePathBytes=128 configuredUrl=false configuredHost=true configuredPort=true configuredPath=true"
            && !expectedMarker.Contains("fetch.internal", StringComparison.Ordinal)
            && !expectedMarker.Contains("/healthz", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpRequestEndpointResult>());
    }

    [HostTest("boot.sequence fetch http endpoint rejects failed result", HostLane.Fast, 1)]
    private static bool TestRejectsFailedResult()
    {
        FetchAppHttpRequestEndpointResult endpoint = FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.InvalidPort,
            port: 0,
            hostBytesWritten: 0,
            pathBytesWritten: 0,
            requiredHostBytes: 14,
            requiredPathBytes: 8,
            availableHostBytes: FetchAppHttpRequestEndpoint.MaximumHostLength,
            availablePathBytes: FetchAppHttpRequestEndpoint.MaximumPathLength,
            hasConfiguredUrl: false,
            hasConfiguredHost: true,
            hasConfiguredPort: true,
            hasConfiguredPath: true);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpRequestEndpointReadiness(
            endpoint,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpRequestEndpointNotReadyMarker(
            FetchAppHttpRequestEndpointReadinessStatus.EndpointFailed,
            endpoint);

        return HostCheck.Result(endpoint.IsInitialized
            && !endpoint.Succeeded
            && endpoint.Status == FetchAppHttpRequestEndpointStatus.InvalidPort
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(FetchAppHttpRequestEndpointReadinessStatus.EndpointFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_HTTP_ENDPOINT_NOT_READY status=EndpointFailed initialized=true endpoint=InvalidPort port=0 hostBytes=0 pathBytes=0 requiredHostBytes=14 requiredPathBytes=8 availableHostBytes=64 availablePathBytes=128 configuredUrl=false configuredHost=true configuredPort=true configuredPath=true");
    }

    [HostTest("boot.sequence fetch http endpoint rejects default result", HostLane.Fast, 2)]
    private static bool TestRejectsDefaultResult()
    {
        FetchAppHttpRequestEndpointResult endpoint = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpRequestEndpointReadiness(
            endpoint,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpRequestEndpointNotReadyMarker(
            FetchAppHttpRequestEndpointReadinessStatus.NotInitialized,
            endpoint);

        return HostCheck.Result(!endpoint.IsInitialized
            && !endpoint.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(FetchAppHttpRequestEndpointReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_HTTP_ENDPOINT_NOT_READY status=NotInitialized initialized=false endpoint=Success port=0 hostBytes=0 pathBytes=0 requiredHostBytes=0 requiredPathBytes=0 availableHostBytes=0 availablePathBytes=0 configuredUrl=false configuredHost=false configuredPort=false configuredPath=false");
    }

    [HostTest("boot.sequence fetch http endpoint preserves default markers", HostLane.Fast, 3)]
    private static bool TestPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(!serial.ContainsPrefix(BootSequence.FetchAppHttpRequestEndpointReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.FetchAppHttpRequestEndpointNotReadyPrefix));
    }

    [HostTest("fetch http endpoint uses shared memory prefix and equality helpers", HostLane.Fast, 4)]
    private static bool TestUsesSharedMemoryHelpers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string endpointSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "FetchAppHttpRequestEndpoint.cs"));

        return HostCheck.Result(endpointSource.Contains(
                "OS.Std.MemoryExtensions.StartsWith(url, \"https://\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.StartsWith(url, \"http://\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(key, \"FETCH_HTTP_URL\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(key, \"FETCH_HTTP_HOST\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(key, \"FETCH_HTTP_PORT\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(key, \"FETCH_HTTP_PATH\"u8)",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.IndexOf(remainder, (byte)'/')",
                StringComparison.Ordinal)
            && endpointSource.Contains(
                "OS.Std.MemoryExtensions.IndexOf(authority, (byte)':')",
                StringComparison.Ordinal)
            && !endpointSource.Contains(
                "private static bool StartsWith(ReadOnlySpan<byte>",
                StringComparison.Ordinal)
            && !endpointSource.Contains(
                "private static bool AsciiEquals(ReadOnlySpan<byte>",
                StringComparison.Ordinal)
            && !endpointSource.Contains(
                "private static int IndexOf(ReadOnlySpan<byte>",
                StringComparison.Ordinal));
    }

    [HostTest("fetch.http request endpoint formatter uses shared ascii text builder", HostLane.Fast, 5)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Net",
                "FetchAppHttpRequestEndpointDiagnosticFormatter.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.Port)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.HostBytesWritten)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.PathBytesWritten)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.RequiredHostBytes)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.RequiredPathBytes)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void Append(",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal));
    }

    private static FetchAppHttpRequestEndpointResult ReadyEndpoint()
    {
        return FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.Success,
            port: 8080,
            hostBytesWritten: 14,
            pathBytesWritten: 8,
            requiredHostBytes: 14,
            requiredPathBytes: 8,
            availableHostBytes: FetchAppHttpRequestEndpoint.MaximumHostLength,
            availablePathBytes: FetchAppHttpRequestEndpoint.MaximumPathLength,
            hasConfiguredUrl: false,
            hasConfiguredHost: true,
            hasConfiguredPort: true,
            hasConfiguredPath: true);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
