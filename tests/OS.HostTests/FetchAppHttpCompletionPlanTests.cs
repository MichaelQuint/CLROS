using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Net;

internal static class FetchAppHttpCompletionPlanTests
{

    [HostTest("fetch.http completion accepts expected body marker", HostLane.Fast, 0)]
    private static bool TestAcceptsExpectedBodyMarker()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 27\r\nConnection: close\r\n\r\nprefix FETCH_OK suffix tail");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        int expectedMatchOffset = httpPlan.HttpBodyOffset + "prefix ".Length;
        return HostCheck.Result(result.IsInitialized
            && result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.Success
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && result.HttpStatusCode == 200
            && result.ExpectedHttpStatusCode == 200
            && result.ResponseBytesRead == response.Length
            && result.BodyOffset == httpPlan.HttpBodyOffset
            && result.BodyLength == httpPlan.HttpBodyLength
            && result.ExpectedMarkerLength == expectedMarker.Length
            && result.MatchOffset == expectedMatchOffset
            && response[result.MatchOffset] == (byte)'F'
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionPlanResult>());
    }

    [HostTest("fetch.http completion rejects failed http plan", HostLane.Fast, 1)]
    private static bool TestRejectsFailedHttpPlan()
    {
        HttpTcpClientGetPlanResult failedHttpPlan = CreateFailedHttpPlan();
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n");
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            failedHttpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.HttpPlanFailed
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.HttpResponseFailed
            && result.HttpStatusCode == 0
            && result.ExpectedHttpStatusCode == 200
            && result.ExpectedMarkerLength == expectedMarker.Length
            && result.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset);
    }

    [HostTest("fetch.http completion rejects missing body marker", HostLane.Fast, 2)]
    private static bool TestRejectsMissingBodyMarker()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 11\r\nConnection: close\r\n\r\nNO_MARKER!!");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.MissingExpectedMarker
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && result.HttpStatusCode == 200
            && result.BodyOffset == httpPlan.HttpBodyOffset
            && result.BodyLength == httpPlan.HttpBodyLength
            && result.ExpectedMarkerLength == expectedMarker.Length
            && result.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset);
    }

    [HostTest("fetch.http completion rejects invalid body range", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidBodyRange()
    {
        byte[] response = Encoding.ASCII.GetBytes("0123456789abcdef");
        HttpTcpClientGetPlanResult invalidRangePlan = CreateSyntheticSuccessPlan(
            httpStatusCode: 200,
            responseBytesRead: 12,
            bodyOffset: 5,
            bodyLength: 10);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            invalidRangePlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.InvalidBodyRange
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && result.ResponseBytesRead == 12
            && result.BodyOffset == 5
            && result.BodyLength == 10
            && result.ExpectedMarkerLength == expectedMarker.Length
            && result.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset);
    }

    [HostTest("fetch.http completion rejects empty expected marker", HostLane.Fast, 4)]
    private static bool TestRejectsEmptyExpectedMarker()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\n\r\nFETCH_OK");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            ReadOnlySpan<byte>.Empty);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.InvalidExpectedMarker
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && result.HttpStatusCode == 200
            && result.ExpectedMarkerLength == 0
            && result.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset);
    }

    [HostTest("fetch.http completion rejects short response buffer", HostLane.Fast, 5)]
    private static bool TestRejectsShortResponseBuffer()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\n\r\nFETCH_OK");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response.AsSpan(0, response.Length - 1),
            expectedHttpStatusCode: 200,
            expectedMarker);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall
            && result.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && result.ResponseBytesRead == response.Length
            && result.ExpectedMarkerLength == expectedMarker.Length
            && result.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset);
    }

    [HostTest("fetch.http completion default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        FetchAppHttpCompletionPlanResult defaultResult = default;
        HttpTcpClientGetPlanResult defaultHttpPlan = default;
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 8\r\n\r\nFETCH_OK");
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");

        FetchAppHttpCompletionPlanResult defaultInputResult = FetchAppHttpCompletionPlan.Evaluate(
            defaultHttpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == FetchAppHttpCompletionPlanStatus.Success
            && defaultResult.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && defaultResult.HttpStatusCode == 0
            && defaultResult.ExpectedHttpStatusCode == 0
            && defaultResult.ResponseBytesRead == 0
            && defaultResult.BodyOffset == 0
            && defaultResult.BodyLength == 0
            && defaultResult.ExpectedMarkerLength == 0
            && defaultResult.MatchOffset == 0
            && defaultInputResult.IsInitialized
            && !defaultInputResult.Succeeded
            && defaultInputResult.Status == FetchAppHttpCompletionPlanStatus.HttpPlanFailed
            && defaultInputResult.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && defaultInputResult.ExpectedMarkerLength == expectedMarker.Length
            && defaultInputResult.MatchOffset == FetchAppHttpCompletionPlanResult.NoMatchOffset
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionPlanResult>());
    }

    [HostTest("fetch.http completion uses shared memory helper", HostLane.Fast, 7)]
    private static bool TestUsesSharedMemoryHelper()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "FetchAppHttpCompletionPlan.cs"));

        return HostCheck.Result(source.Contains(
                "OS.Std.MemoryExtensions.IndexOf(body, expectedBodyMarker)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int IndexOf(ReadOnlySpan<byte>",
                StringComparison.Ordinal));
    }

    [HostTest("fetch.http completion config uses deterministic defaults", HostLane.Fast, 8)]
    private static bool TestConfigUsesDeterministicDefaults()
    {
        byte[] manifestBytes = NativeManifest();
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);

        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes);
        Span<byte> markerScratch = stackalloc byte[FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength];
        FetchAppHttpCompletionExpectedMarkerCopyResult markerCopy =
            FetchAppHttpCompletionExpectation.CopyExpectedMarker(
                expectation,
                manifestBytes,
                markerScratch);

        return HostCheck.Result(expectation.Succeeded
            && expectation.Status == FetchAppHttpCompletionExpectationStatus.Success
            && expectation.ExpectedHttpStatusCode == 200
            && expectation.ExpectedMarkerLength == "FETCH_OK".Length
            && expectation.UsesDefaultMarker
            && !expectation.HasConfiguredStatus
            && !expectation.HasConfiguredMarker
            && markerCopy.Succeeded
            && MarkerEquals(markerScratch[..markerCopy.BytesWritten], "FETCH_OK")
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionExpectationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionExpectedMarkerCopyResult>());
    }

    [HostTest("fetch.http completion config flows from manifest", HostLane.Fast, 9)]
    private static bool TestConfigFlowsFromManifest()
    {
        byte[] manifestBytes = NativeManifest(
            "env.FETCH_HTTP_EXPECTED_STATUS=202\n" +
            "env.FETCH_HTTP_EXPECTED_MARKER=CONFIG_OK\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes, requireConfiguredMarker: true);
        Span<byte> markerScratch = stackalloc byte[FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength];
        FetchAppHttpCompletionExpectedMarkerCopyResult markerCopy =
            FetchAppHttpCompletionExpectation.CopyExpectedMarker(
                expectation,
                manifestBytes,
                markerScratch);

        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 202 Accepted\r\nContent-Length: 16\r\nConnection: close\r\n\r\nprefix CONFIG_OK");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        FetchAppHttpCompletionPlanResult completion = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectation.ExpectedHttpStatusCode,
            markerScratch[..markerCopy.BytesWritten]);

        return HostCheck.Result(manifest.Succeeded
            && expectation.Succeeded
            && expectation.ExpectedHttpStatusCode == 202
            && expectation.ExpectedMarkerLength == "CONFIG_OK".Length
            && !expectation.UsesDefaultMarker
            && expectation.HasConfiguredStatus
            && expectation.HasConfiguredMarker
            && markerCopy.Succeeded
            && MarkerEquals(markerScratch[..markerCopy.BytesWritten], "CONFIG_OK")
            && completion.Succeeded
            && completion.Status == FetchAppHttpCompletionPlanStatus.Success
            && completion.ExpectedHttpStatusCode == 202
            && completion.ExpectedMarkerLength == "CONFIG_OK".Length
            && completion.HttpStatusCode == 202);
    }

    [HostTest("fetch.http completion config rejects invalid status", HostLane.Fast, 10)]
    private static bool TestConfigRejectsInvalidStatus()
    {
        return HostCheck.Result(ConfigStatusIs("env.FETCH_HTTP_EXPECTED_STATUS=99\n", FetchAppHttpCompletionExpectationStatus.InvalidStatusCode)
            && ConfigStatusIs("env.FETCH_HTTP_EXPECTED_STATUS=600\n", FetchAppHttpCompletionExpectationStatus.InvalidStatusCode)
            && ConfigStatusIs("env.FETCH_HTTP_EXPECTED_STATUS=OK\n", FetchAppHttpCompletionExpectationStatus.InvalidStatusCode));
    }

    [HostTest("fetch.http completion config rejects empty marker", HostLane.Fast, 11)]
    private static bool TestConfigRejectsEmptyMarker()
    {
        byte[] manifestBytes = NativeManifest("env.FETCH_HTTP_EXPECTED_BODY=\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes);

        return HostCheck.Result(manifest.Succeeded
            && expectation.IsInitialized
            && !expectation.Succeeded
            && expectation.Status == FetchAppHttpCompletionExpectationStatus.EmptyMarker
            && expectation.ExpectedMarkerLength == 0
            && expectation.HasConfiguredMarker
            && !expectation.UsesDefaultMarker);
    }

    [HostTest("fetch.http completion config rejects oversized marker", HostLane.Fast, 12)]
    private static bool TestConfigRejectsOversizedMarker()
    {
        string marker = new string('A', FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength + 1);
        byte[] manifestBytes = NativeManifest("env.OS_SMOKE_EXPECTED_BODY=" + marker + "\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes);

        return HostCheck.Result(manifest.Succeeded
            && expectation.IsInitialized
            && !expectation.Succeeded
            && expectation.Status == FetchAppHttpCompletionExpectationStatus.MarkerTooLarge
            && expectation.ExpectedMarkerLength == marker.Length
            && expectation.HasConfiguredMarker
            && !expectation.UsesDefaultMarker);
    }

    [HostTest("fetch.http completion config requires marker when requested", HostLane.Fast, 13)]
    private static bool TestConfigRequiresMarkerWhenRequested()
    {
        byte[] manifestBytes = NativeManifest("env.FETCH_HTTP_EXPECTED_STATUS=204\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes, requireConfiguredMarker: true);

        return HostCheck.Result(manifest.Succeeded
            && expectation.IsInitialized
            && !expectation.Succeeded
            && expectation.Status == FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig
            && expectation.ExpectedHttpStatusCode == 204
            && expectation.ExpectedMarkerLength == 0
            && expectation.HasConfiguredStatus
            && !expectation.HasConfiguredMarker);
    }

    [HostTest("fetch.http expectation uses shared ascii equality helper", HostLane.Fast, 14)]
    private static bool TestExpectationUsesSharedAsciiEqualityHelper()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "FetchAppHttpCompletionExpectation.cs"));

        return HostCheck.Result(source.Contains(
                "AsciiString.Equals(key, \"FETCH_HTTP_EXPECTED_STATUS\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiString.Equals(key, \"FETCH_HTTP_EXPECTED_MARKER\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiString.Equals(key, \"FETCH_HTTP_EXPECTED_BODY\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiString.Equals(key, \"OS_SMOKE_EXPECTED_BODY\"u8)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static bool AsciiEquals(ReadOnlySpan<byte>",
                StringComparison.Ordinal));
    }

    [HostTest("fetch.http expectation marker copy uses shared buffer operations", HostLane.Fast, 15)]
    private static bool TestExpectationMarkerCopyUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "FetchAppHttpCompletionExpectation.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(marker, destination)", StringComparison.Ordinal)
            && !source.Contains("marker.CopyTo(destination)", StringComparison.Ordinal));
    }

    [HostTest("fetch.http request endpoint defaults", HostLane.Fast, 16)]
    private static bool TestRequestEndpointDefaults()
    {
        byte[] manifestBytes = NativeManifest();
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        Span<byte> host = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> path = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];

        FetchAppHttpRequestEndpointResult result =
            FetchAppHttpRequestEndpoint.FromManifest(manifest, manifestBytes, host, path);

        return HostCheck.Result(manifest.Succeeded
            && result.Succeeded
            && result.Status == FetchAppHttpRequestEndpointStatus.Success
            && result.Port == 80
            && result.HostBytesWritten == "example.com".Length
            && result.PathBytesWritten == 1
            && !result.HasConfiguredUrl
            && !result.HasConfiguredHost
            && !result.HasConfiguredPort
            && !result.HasConfiguredPath
            && MarkerEquals(host[..result.HostBytesWritten], "example.com")
            && MarkerEquals(path[..result.PathBytesWritten], "/")
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpRequestEndpointResult>());
    }

    [HostTest("fetch.http request endpoint explicit config", HostLane.Fast, 17)]
    private static bool TestRequestEndpointExplicitConfig()
    {
        byte[] manifestBytes = NativeManifest(
            "env.FETCH_HTTP_HOST=fetch.internal\n" +
            "env.FETCH_HTTP_PORT=8080\n" +
            "env.FETCH_HTTP_PATH=/healthz\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        Span<byte> host = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> path = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];

        FetchAppHttpRequestEndpointResult explicitResult =
            FetchAppHttpRequestEndpoint.FromManifest(manifest, manifestBytes, host, path);

        byte[] urlManifestBytes = NativeManifest(
            "env.FETCH_HTTP_URL=http://url.example:9090/status\n" +
            "env.FETCH_HTTP_HOST=ignored.example\n" +
            "env.FETCH_HTTP_PORT=8080\n" +
            "env.FETCH_HTTP_PATH=/ignored\n");
        NativeManifestParseResult urlManifest = NativeManifestParser.Parse(urlManifestBytes);
        Span<byte> urlHost = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> urlPath = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];

        FetchAppHttpRequestEndpointResult urlResult =
            FetchAppHttpRequestEndpoint.FromManifest(urlManifest, urlManifestBytes, urlHost, urlPath);

        return HostCheck.Result(manifest.Succeeded
            && explicitResult.Succeeded
            && explicitResult.Port == 8080
            && explicitResult.HasConfiguredHost
            && explicitResult.HasConfiguredPort
            && explicitResult.HasConfiguredPath
            && MarkerEquals(host[..explicitResult.HostBytesWritten], "fetch.internal")
            && MarkerEquals(path[..explicitResult.PathBytesWritten], "/healthz")
            && urlManifest.Succeeded
            && urlResult.Succeeded
            && urlResult.Port == 9090
            && urlResult.HasConfiguredUrl
            && urlResult.HasConfiguredPort
            && urlResult.HasConfiguredPath
            && MarkerEquals(urlHost[..urlResult.HostBytesWritten], "url.example")
            && MarkerEquals(urlPath[..urlResult.PathBytesWritten], "/status"));
    }

    [HostTest("fetch.http request endpoint invalid scheme", HostLane.Fast, 18)]
    private static bool TestRequestEndpointInvalidScheme()
    {
        return HostCheck.Result(EndpointStatusIs(
                "env.FETCH_HTTP_URL=https://example.com/healthz\n",
                FetchAppHttpRequestEndpointStatus.UnsupportedScheme)
            && EndpointStatusIs(
                "env.FETCH_HTTP_URL=ftp://example.com/healthz\n",
                FetchAppHttpRequestEndpointStatus.UnsupportedScheme));
    }

    [HostTest("fetch.http request endpoint invalid port", HostLane.Fast, 19)]
    private static bool TestRequestEndpointInvalidPort()
    {
        return HostCheck.Result(EndpointStatusIs(
                "env.FETCH_HTTP_PORT=0\n",
                FetchAppHttpRequestEndpointStatus.InvalidPort)
            && EndpointStatusIs(
                "env.FETCH_HTTP_PORT=65536\n",
                FetchAppHttpRequestEndpointStatus.InvalidPort)
            && EndpointStatusIs(
                "env.FETCH_HTTP_URL=http://example.com:bad/healthz\n",
                FetchAppHttpRequestEndpointStatus.InvalidPort));
    }

    [HostTest("fetch.http request endpoint missing host", HostLane.Fast, 20)]
    private static bool TestRequestEndpointMissingHost()
    {
        return HostCheck.Result(EndpointStatusIs(
                "env.FETCH_HTTP_HOST=\n",
                FetchAppHttpRequestEndpointStatus.MissingHost)
            && EndpointStatusIs(
                "env.FETCH_HTTP_URL=http:///healthz\n",
                FetchAppHttpRequestEndpointStatus.MissingHost));
    }

    [HostTest("fetch.http request endpoint oversized fields", HostLane.Fast, 21)]
    private static bool TestRequestEndpointOversizedFields()
    {
        string oversizedHost = new string('a', FetchAppHttpRequestEndpoint.MaximumHostLength + 1);
        string oversizedPath = "/" + new string('b', FetchAppHttpRequestEndpoint.MaximumPathLength);

        return HostCheck.Result(EndpointStatusIs(
                "env.FETCH_HTTP_HOST=" + oversizedHost + "\n",
                FetchAppHttpRequestEndpointStatus.FieldTooLarge)
            && EndpointStatusIs(
                "env.FETCH_HTTP_PATH=" + oversizedPath + "\n",
                FetchAppHttpRequestEndpointStatus.FieldTooLarge));
    }

    [HostTest("fetch.http request endpoint copy uses shared buffer operations", HostLane.Fast, 22)]
    private static bool TestRequestEndpointCopyUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "FetchAppHttpRequestEndpoint.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(host, hostDestination)", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(path, pathDestination)", StringComparison.Ordinal)
            && !source.Contains("host.CopyTo(hostDestination)", StringComparison.Ordinal)
            && !source.Contains("path.CopyTo(pathDestination)", StringComparison.Ordinal));
    }

    [HostTest("fetch.http request endpoint formatter formats success marker", HostLane.Fast, 23)]
    private static bool TestRequestEndpointFormatterFormatsSuccessMarker()
    {
        byte[] manifestBytes = NativeManifest(
            "env.FETCH_HTTP_HOST=fetch.internal\n" +
            "env.FETCH_HTTP_PORT=8080\n" +
            "env.FETCH_HTTP_PATH=/healthz\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        Span<byte> host = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> path = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];
        FetchAppHttpRequestEndpointResult endpoint =
            FetchAppHttpRequestEndpoint.FromManifest(manifest, manifestBytes, host, path);

        FetchAppHttpRequestEndpointDiagnosticFormatResult query =
            FetchAppHttpRequestEndpointDiagnosticFormatter.QueryRequiredBytes(endpoint);
        byte[] destination = new byte[query.RequiredBytes];
        FetchAppHttpRequestEndpointDiagnosticFormatResult format =
            FetchAppHttpRequestEndpointDiagnosticFormatter.FormatResult(endpoint, destination);

        const string expected =
            "FETCH_HTTP_ENDPOINT status=Success initialized=true port=8080 hostBytes=14 pathBytes=8 requiredHostBytes=14 requiredPathBytes=8 source=manifest urlConfigured=false hostConfigured=true portConfigured=true pathConfigured=true";

        return HostCheck.Result(manifest.Succeeded
            && endpoint.Succeeded
            && query.IsInitialized
            && query.Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success
            && query.EndpointStatus == FetchAppHttpRequestEndpointStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && Encoding.ASCII.GetString(destination) == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpRequestEndpointDiagnosticFormatResult>());
    }

    [HostTest("fetch.http request endpoint formatter reports short destination", HostLane.Fast, 24)]
    private static bool TestRequestEndpointFormatterReportsShortDestination()
    {
        byte[] manifestBytes = NativeManifest("env.FETCH_HTTP_URL=http://url.example:9090/status\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        Span<byte> host = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> path = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];
        FetchAppHttpRequestEndpointResult endpoint =
            FetchAppHttpRequestEndpoint.FromManifest(manifest, manifestBytes, host, path);

        FetchAppHttpRequestEndpointDiagnosticFormatResult query =
            FetchAppHttpRequestEndpointDiagnosticFormatter.QueryRequiredBytes(endpoint);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        FetchAppHttpRequestEndpointDiagnosticFormatResult format =
            FetchAppHttpRequestEndpointDiagnosticFormatter.FormatResult(endpoint, destination);

        return HostCheck.Result(manifest.Succeeded
            && endpoint.Succeeded
            && query.IsInitialized
            && query.Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.DestinationTooSmall
            && format.EndpointStatus == FetchAppHttpRequestEndpointStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("fetch.http request endpoint formatter rejects default result", HostLane.Fast, 25)]
    private static bool TestRequestEndpointFormatterRejectsDefaultResult()
    {
        FetchAppHttpRequestEndpointResult defaultResult = default;
        byte[] destination = new byte[128];

        FetchAppHttpRequestEndpointDiagnosticFormatResult format =
            FetchAppHttpRequestEndpointDiagnosticFormatter.FormatResult(defaultResult, destination);
        FetchAppHttpRequestEndpointDiagnosticFormatResult query =
            FetchAppHttpRequestEndpointDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        FetchAppHttpRequestEndpointDiagnosticFormatResult defaultFormat = default;

        return HostCheck.Result(format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.InvalidResult
            && format.EndpointStatus == FetchAppHttpRequestEndpointStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded);
    }

    [HostTest("fetch.http completion expectation formatter formats success marker", HostLane.Fast, 26)]
    private static bool TestExpectationFormatterFormatsSuccessMarker()
    {
        byte[] manifestBytes = NativeManifest(
            "env.FETCH_HTTP_EXPECTED_STATUS=202\n" +
            "env.FETCH_HTTP_EXPECTED_MARKER=CONFIG_OK\n");
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes, requireConfiguredMarker: true);

        FetchAppHttpCompletionExpectationDiagnosticFormatResult query =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.QueryRequiredBytes(expectation);
        byte[] destination = new byte[query.RequiredBytes];
        FetchAppHttpCompletionExpectationDiagnosticFormatResult format =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.FormatResult(expectation, destination);

        const string expected =
            "FETCH_HTTP_EXPECTATION status=Success initialized=true expectedHttpStatus=202 expectedMarkerBytes=9 source=manifest statusConfigured=true markerConfigured=true defaultMarker=false";

        return HostCheck.Result(manifest.Succeeded
            && expectation.Succeeded
            && query.IsInitialized
            && query.Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success
            && query.ExpectationStatus == FetchAppHttpCompletionExpectationStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && Encoding.ASCII.GetString(destination) == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionExpectationDiagnosticFormatResult>());
    }

    [HostTest("fetch.http completion expectation formatter reports short destination", HostLane.Fast, 27)]
    private static bool TestExpectationFormatterReportsShortDestination()
    {
        byte[] manifestBytes = NativeManifest();
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes);

        FetchAppHttpCompletionExpectationDiagnosticFormatResult query =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.QueryRequiredBytes(expectation);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        FetchAppHttpCompletionExpectationDiagnosticFormatResult format =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.FormatResult(expectation, destination);

        return HostCheck.Result(manifest.Succeeded
            && expectation.Succeeded
            && query.IsInitialized
            && query.Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.DestinationTooSmall
            && format.ExpectationStatus == FetchAppHttpCompletionExpectationStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("fetch.http completion expectation formatter rejects default result", HostLane.Fast, 28)]
    private static bool TestExpectationFormatterRejectsDefaultResult()
    {
        FetchAppHttpCompletionExpectationResult defaultResult = default;
        byte[] destination = new byte[128];

        FetchAppHttpCompletionExpectationDiagnosticFormatResult format =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.FormatResult(defaultResult, destination);
        FetchAppHttpCompletionExpectationDiagnosticFormatResult query =
            FetchAppHttpCompletionExpectationDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        FetchAppHttpCompletionExpectationDiagnosticFormatResult defaultFormat = default;

        return HostCheck.Result(format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.InvalidResult
            && format.ExpectationStatus == FetchAppHttpCompletionExpectationStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded);
    }

    [HostTest("fetch.http completion formatters use shared ascii text builder", HostLane.Fast, 29)]
    private static bool TestCompletionFormattersUseSharedAsciiTextBuilder()
    {
        return HostCheck.Result(FormatterSourceUsesSharedAsciiTextBuilder(
                "FetchAppHttpCompletionPlanDiagnosticFormatter.cs")
            && FormatterSourceUsesSharedAsciiTextBuilder(
                "FetchAppHttpCompletionExpectationDiagnosticFormatter.cs"));
    }

    [HostTest("fetch.http completion formatter formats success marker", HostLane.Fast, 30)]
    private static bool TestFormatterFormatsSuccessMarker()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 27\r\nConnection: close\r\n\r\nprefix FETCH_OK suffix tail");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");
        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        FetchAppHttpCompletionPlanDiagnosticFormatResult query =
            FetchAppHttpCompletionPlanDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes];
        FetchAppHttpCompletionPlanDiagnosticFormatResult format =
            FetchAppHttpCompletionPlanDiagnosticFormatter.FormatResult(result, destination);

        string expected = "FETCH_HTTP_COMPLETION status=Success httpPlan=Success httpStatus=200 expectedHttpStatus=200 responseBytes=" +
            response.Length +
            " bodyOffset=" +
            httpPlan.HttpBodyOffset +
            " bodyBytes=" +
            httpPlan.HttpBodyLength +
            " expectedMarkerBytes=" +
            expectedMarker.Length +
            " matchOffset=" +
            result.MatchOffset;

        return HostCheck.Result(result.Succeeded
            && query.IsInitialized
            && query.Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success
            && query.CompletionStatus == FetchAppHttpCompletionPlanStatus.Success
            && query.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && Encoding.ASCII.GetString(destination) == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpCompletionPlanDiagnosticFormatResult>());
    }

    [HostTest("fetch.http completion formatter reports short destination", HostLane.Fast, 31)]
    private static bool TestFormatterReportsShortDestination()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 27\r\nConnection: close\r\n\r\nprefix FETCH_OK suffix tail");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        byte[] expectedMarker = Encoding.ASCII.GetBytes("FETCH_OK");
        FetchAppHttpCompletionPlanResult result = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            expectedMarker);

        FetchAppHttpCompletionPlanDiagnosticFormatResult query =
            FetchAppHttpCompletionPlanDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        FetchAppHttpCompletionPlanDiagnosticFormatResult format =
            FetchAppHttpCompletionPlanDiagnosticFormatter.FormatResult(result, destination);

        return HostCheck.Result(query.IsInitialized
            && query.Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.DestinationTooSmall
            && format.CompletionStatus == FetchAppHttpCompletionPlanStatus.Success
            && format.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("fetch.http completion formatter rejects default result", HostLane.Fast, 32)]
    private static bool TestFormatterRejectsDefaultResult()
    {
        FetchAppHttpCompletionPlanResult defaultResult = default;
        byte[] destination = new byte[128];

        FetchAppHttpCompletionPlanDiagnosticFormatResult format =
            FetchAppHttpCompletionPlanDiagnosticFormatter.FormatResult(defaultResult, destination);
        FetchAppHttpCompletionPlanDiagnosticFormatResult query =
            FetchAppHttpCompletionPlanDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        FetchAppHttpCompletionPlanDiagnosticFormatResult defaultFormat = default;

        return HostCheck.Result(format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.InvalidResult
            && format.CompletionStatus == FetchAppHttpCompletionPlanStatus.Success
            && format.HttpPlanStatus == HttpTcpClientGetPlanStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded);
    }

    [HostTest("boot.sequence fetch http completion ready", HostLane.Fast, 33)]
    private static bool TestBootSequenceFetchHttpCompletionReady()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 27\r\nConnection: close\r\n\r\nprefix FETCH_OK suffix tail");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        FetchAppHttpCompletionPlanResult completion = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            Encoding.ASCII.GetBytes("FETCH_OK"));
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpCompletionReadiness(
            completion,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpCompletionReadyMarker(completion);

        return HostCheck.Result(completion.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "FETCH_APP_HTTP_COMPLETION_READY status=Ready initialized=true succeeded=true completion=Success httpPlan=Success httpStatus=200 expectedStatus=200 responseBytes=85 bodyOffset=58 bodyLength=27 markerLength=8 matchOffset=65");
    }

    [HostTest("boot.sequence fetch http completion rejects failed result", HostLane.Fast, 34)]
    private static bool TestBootSequenceFetchHttpCompletionRejectsFailedResult()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 11\r\nConnection: close\r\n\r\nNO_MARKER!!");
        HttpTcpClientGetPlanResult httpPlan = CreateSuccessfulPlan(response);
        FetchAppHttpCompletionPlanResult completion = FetchAppHttpCompletionPlan.Evaluate(
            httpPlan,
            response,
            expectedHttpStatusCode: 200,
            Encoding.ASCII.GetBytes("FETCH_OK"));
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpCompletionReadiness(
            completion,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpCompletionNotReadyMarker(
            FetchAppHttpCompletionReadinessStatus.CompletionFailed,
            completion);

        return HostCheck.Result(completion.IsInitialized
            && !completion.Succeeded
            && completion.Status == FetchAppHttpCompletionPlanStatus.MissingExpectedMarker
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(FetchAppHttpCompletionReadinessStatus.CompletionFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "FETCH_APP_HTTP_COMPLETION_NOT_READY status=CompletionFailed initialized=true succeeded=false completion=MissingExpectedMarker httpPlan=Success httpStatus=200 expectedStatus=200 responseBytes=69 bodyOffset=58 bodyLength=11 markerLength=8 matchOffset=-1");
    }

    [HostTest("boot.sequence fetch http completion preserves default markers", HostLane.Fast, 35)]
    private static bool TestBootSequenceFetchHttpCompletionPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        for (int index = 0; index < serial.Count; index++)
        {
            string line = serial.LineAt(index);
            if (line.StartsWith(BootSequence.FetchAppHttpCompletionReadyPrefix, StringComparison.Ordinal) ||
                line.StartsWith(BootSequence.FetchAppHttpCompletionNotReadyPrefix, StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    private static HttpTcpClientGetPlanResult CreateSuccessfulPlan(byte[] response)
    {
        if (HttpResponseParser.TryParse(response, out HttpResponseParseResult parsed) !=
            HttpResponseParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to parse synthetic HTTP response.");
        }

        return CreateSyntheticSuccessPlan(
            parsed.StatusCode,
            response.Length,
            parsed.BodyOffset,
            parsed.BodyLength);
    }

    private static HttpTcpClientGetPlanResult CreateSyntheticSuccessPlan(
        ushort httpStatusCode,
        int responseBytesRead,
        int bodyOffset,
        int bodyLength)
    {
        HttpResponseParseResult parsed = HttpResponseParseResult.Success(
            protocolMajor: 1,
            protocolMinor: 1,
            httpStatusCode,
            reasonOffset: 13,
            reasonLength: 2,
            headerLength: bodyOffset,
            hasContentLength: true,
            contentLength: bodyLength,
            connectionClose: true,
            bodyOffset,
            bodyLength);
        HttpClientGetTransactionResult http = HttpClientGetTransactionResult.ResponseSuccess(
            HttpClientGetTransactionState.ResponseReceived,
            parsed);
        TcpEstablishedStreamReceiveResult receive = TcpEstablishedStreamReceiveResult.Success(
            payloadBytesAccepted: responseBytesRead,
            unreadBytes: responseBytesRead,
            localNextSequenceNumber: 0x1200_0001,
            remoteNextSequenceNumber: 0x3400_0001 + (uint)responseBytesRead);
        TcpEstablishedStreamReadResult read = TcpEstablishedStreamReadResult.Success(
            responseBytesRead,
            unreadBytes: 0,
            acknowledgementNumber: 0x3400_0001 + (uint)responseBytesRead);

        return HttpTcpClientGetPlanResult.ResponseSuccess(
            HttpTcpClientGetPlannerState.ResponseReceived,
            receive,
            read,
            http,
            default,
            default,
            default,
            tcpSegmentOffset: HttpTcpClientGetPlanner.TcpSegmentOffset,
            tcpSegmentLength: TcpHeader.MinHeaderLength + responseBytesRead);
    }

    private static HttpTcpClientGetPlanResult CreateFailedHttpPlan()
    {
        return HttpTcpClientGetPlanResult.Fail(
            HttpTcpClientGetPlanStatus.HttpResponseFailed,
            HttpTcpClientGetPlannerState.Failed,
            TcpIpv4FramePlanStatus.Success,
            TcpActiveConnectStatus.Success,
            TcpEstablishedStreamStatus.Success,
            HttpClientGetTransactionStatus.MalformedResponse,
            HttpRequestBuilderStatus.Success,
            HttpResponseParseStatus.InvalidProtocol,
            PacketParseStatus.Success,
            default,
            default,
            default,
            tcpSegmentOffset: 0,
            tcpSegmentLength: 0,
            requestBytesWritten: 0,
            responseBytesRead: 0,
            localNextSequenceNumber: 0,
            remoteNextSequenceNumber: 0);
    }

    private static bool ConfigStatusIs(string environmentLine, FetchAppHttpCompletionExpectationStatus status)
    {
        byte[] manifestBytes = NativeManifest(environmentLine);
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(manifest, manifestBytes);

        return manifest.Succeeded
            && expectation.IsInitialized
            && !expectation.Succeeded
            && expectation.Status == status;
    }

    private static bool EndpointStatusIs(string environmentLine, FetchAppHttpRequestEndpointStatus status)
    {
        byte[] manifestBytes = NativeManifest(environmentLine);
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        Span<byte> host = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumHostLength];
        Span<byte> path = stackalloc byte[FetchAppHttpRequestEndpoint.MaximumPathLength];

        FetchAppHttpRequestEndpointResult result =
            FetchAppHttpRequestEndpoint.FromManifest(manifest, manifestBytes, host, path);

        return manifest.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == status
            && result.HostBytesWritten == 0
            && result.PathBytesWritten == 0;
    }

    private static byte[] NativeManifest(string extraEnvironment = "")
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=fetch.app\n" +
            "app.path=apps/fetch.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            extraEnvironment);
    }

    private static bool MarkerEquals(ReadOnlySpan<byte> marker, string expected)
    {
        if (marker.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < marker.Length; index++)
        {
            if (marker[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(byte[] bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool FormatterSourceUsesSharedAsciiTextBuilder(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            fileName));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        return HostToolProcess.FindRepositoryRoot();
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
    }
}
