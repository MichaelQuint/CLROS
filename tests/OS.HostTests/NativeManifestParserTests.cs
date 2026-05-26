using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;

internal static class NativeManifestParserTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("manifest kernel parses v0 native manifest", TestParsesV0NativeManifest, ref failures);
        Run("manifest kernel parses package app fields", TestParsesPackageAppFields, ref failures);
        Run("manifest kernel parses environment fields", TestParsesEnvironmentFields, ref failures);
        Run("manifest env diagnostic formats environment metadata", TestEnvironmentDiagnosticFormatsMetadata, ref failures);
        Run("manifest env diagnostic formats empty environment", TestEnvironmentDiagnosticFormatsEmptyEnvironment, ref failures);
        Run("manifest env diagnostic reports short destination", TestEnvironmentDiagnosticReportsShortDestination, ref failures);
        Run("manifest env diagnostic rejects default result", TestEnvironmentDiagnosticRejectsDefaultResult, ref failures);
        Run("manifest env diagnostic rejects invalid source range", TestEnvironmentDiagnosticRejectsInvalidSourceRange, ref failures);
        Run("manifest env formatter uses shared ascii text builder", TestEnvironmentDiagnosticFormatterUsesSharedAsciiTextBuilder, ref failures);
        Run("boot.sequence manifest env ready", TestBootSequenceManifestEnvironmentReady, ref failures);
        Run("boot.sequence manifest env rejects parse failure", TestBootSequenceManifestEnvironmentRejectsParseFailure, ref failures);
        Run("boot.sequence manifest env rejects invalid source range", TestBootSequenceManifestEnvironmentRejectsInvalidSourceRange, ref failures);
        Run("boot.sequence manifest env rejects default result", TestBootSequenceManifestEnvironmentRejectsDefaultResult, ref failures);
        Run("boot.sequence manifest env formats diagnostic failure marker", TestBootSequenceManifestEnvironmentFormatsDiagnosticFailureMarker, ref failures);
        Run("boot.sequence manifest env preserves default markers", TestBootSequenceManifestEnvironmentPreservesDefaultMarkers, ref failures);
        Run("manifest kernel parses listen endpoint", TestParsesListenEndpoint, ref failures);
        Run("manifest kernel rejects invalid listen endpoint", TestRejectsInvalidListenEndpoint, ref failures);
        Run("manifest kernel rejects duplicate environment keys", TestRejectsDuplicateEnvironmentKeys, ref failures);
        Run("manifest kernel rejects invalid environment key", TestRejectsInvalidEnvironmentKey, ref failures);
        Run("manifest kernel rejects too many environment fields", TestRejectsTooManyEnvironmentFields, ref failures);
        Run("manifest kernel accepts maximum string field lengths", TestAcceptsMaximumStringFieldLengths, ref failures);
        Run("manifest kernel rejects oversized string fields", TestRejectsOversizedStringFields, ref failures);
        Run("manifest kernel rejects unsupported runtime kind", TestRejectsUnsupportedRuntimeKind, ref failures);
        Run("manifest kernel rejects missing app path", TestRejectsMissingAppPath, ref failures);
        Run("manifest kernel rejects malformed network mode", TestRejectsMalformedNetworkMode, ref failures);
        Run("manifest kernel rejects unknown format", TestRejectsUnknownFormat, ref failures);
        Run("manifest kernel rejects unknown version", TestRejectsUnknownVersion, ref failures);
        Run("manifest kernel rejects duplicate keys", TestRejectsDuplicateKeys, ref failures);
        Run("manifest kernel rejects oversized input", TestRejectsOversizedInput, ref failures);
        Run("manifest kernel rejects non-ascii input", TestRejectsNonAsciiInput, ref failures);
        Run("manifest kernel rejects control characters", TestRejectsControlCharacters, ref failures);
        Run("manifest kernel rejects unknown keys", TestRejectsUnknownKeys, ref failures);
        Run("manifest kernel rejects malformed log mode", TestRejectsMalformedLogMode, ref failures);
        Run("manifest kernel preserves token splitter semantics", TestPreservesTokenSplitterSemantics, ref failures);
        Run("manifest kernel preserves equals inside values", TestPreservesEqualsInsideValues, ref failures);
        Run("manifest kernel uses shared key value splitter", TestUsesSharedKeyValueSplitter, ref failures);
        Run("manifest kernel uses shared ascii equality helper", TestUsesSharedAsciiEqualityHelper, ref failures);
        Run("manifest kernel uses shared memory env helpers", TestUsesSharedMemoryEnvironmentHelpers, ref failures);
        Run("manifest kernel uses shared ascii classification helpers", TestUsesSharedAsciiClassificationHelpers, ref failures);
        Run("manifest fetchapp config preflight valid config", TestFetchAppConfigPreflightValidConfig, ref failures);
        Run("manifest fetchapp config preflight defaults", TestFetchAppConfigPreflightDefaults, ref failures);
        Run("manifest fetchapp config preflight invalid status", TestFetchAppConfigPreflightInvalidStatus, ref failures);
        Run("manifest fetchapp config preflight empty marker", TestFetchAppConfigPreflightEmptyMarker, ref failures);
        Run("manifest fetchapp config preflight oversized marker", TestFetchAppConfigPreflightOversizedMarker, ref failures);
        Run("manifest fetchapp config preflight manifest not ready", TestFetchAppConfigPreflightManifestNotReady, ref failures);
        Run("boot.sequence fetchapp config preflight ready", TestBootSequenceFetchAppConfigPreflightReady, ref failures);
        Run("boot.sequence fetchapp config preflight rejects failed result", TestBootSequenceFetchAppConfigPreflightRejectsFailedResult, ref failures);
        Run("boot.sequence fetchapp config preflight rejects default result", TestBootSequenceFetchAppConfigPreflightRejectsDefaultResult, ref failures);
        Run("boot.sequence fetchapp config preflight preserves default markers", TestBootSequenceFetchAppConfigPreflightPreservesDefaultMarkers, ref failures);
        Run("manifest kernel parse default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native manifest parser host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestParsesV0NativeManifest()
    {
        byte[] source = ValidManifest();
        NativeManifestParseResult result = NativeManifestParser.Parse(source);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestParseStatus.Success
            && result.Manifest.Version == NativeManifest.Version0
            && FieldEquals(source, result.Manifest.AppName, "hello.app")
            && FieldEquals(source, result.Manifest.AppPath, "apps/hello.bin")
            && result.Manifest.Args.IsPresent
            && result.Manifest.Args.Length == 0
            && result.Manifest.AppKind == NativeManifestAppKind.Package
            && result.Manifest.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && result.Manifest.NetworkMode == NativeManifestNetworkMode.Dhcp
            && result.Manifest.LogMode == NativeManifestLogMode.Serial;
    }

    private static bool TestParsesPackageAppFields()
    {
        byte[] source = ValidManifest();
        NativeManifestParseResult result = NativeManifestParser.Parse(source);

        return result.Succeeded
            && result.Manifest.AppKind == NativeManifestAppKind.Package
            && result.Manifest.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && FieldEquals(source, result.Manifest.AppName, "hello.app")
            && FieldEquals(source, result.Manifest.AppPath, "apps/hello.bin");
    }

    private static bool TestParsesEnvironmentFields()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.ASPNETCORE_ENVIRONMENT=Production\n" +
            "env.PORT=8080\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);

        return result.Succeeded
            && result.Manifest.EnvironmentCount == 2
            && EnvironmentEntryEquals(source, result.Manifest.GetEnvironmentEntry(0), "ASPNETCORE_ENVIRONMENT", "Production")
            && EnvironmentEntryEquals(source, result.Manifest.GetEnvironmentEntry(1), "PORT", "8080");
    }

    private static bool TestEnvironmentDiagnosticFormatsMetadata()
    {
        byte[] source = ManifestWithEnvironment(
            "env.ASPNETCORE_ENVIRONMENT=Production\n" +
            "env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentEntry first = parsed.Manifest.GetEnvironmentEntry(0);

        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, source);
        byte[] destination = new byte[query.RequiredBytes];
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, source, destination);
        string marker = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);

        string expected =
            "NATIVE_MANIFEST_ENV status=Success initialized=true environmentCount=2 maxEnvironmentCount=16" +
            " firstKeyPresent=true firstKeyOffset=" + first.Key.Offset +
            " firstKeyLength=" + first.Key.Length +
            " firstValueOffset=" + first.Value.Offset +
            " firstValueLength=" + first.Value.Length +
            " processEnvironmentMaterialized=false hostedDotNetStarted=false kestrelStarted=false";

        return parsed.Succeeded
            && query.Status == NativeManifestEnvironmentDiagnosticFormatStatus.Success
            && query.ParseStatus == NativeManifestParseStatus.Success
            && query.RequiredBytes == expected.Length
            && query.BytesWritten == 0
            && format.Succeeded
            && format.BytesWritten == expected.Length
            && marker == expected
            && marker.Contains("processEnvironmentMaterialized=false", StringComparison.Ordinal)
            && marker.Contains("hostedDotNetStarted=false", StringComparison.Ordinal)
            && marker.Contains("kestrelStarted=false", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeManifestEnvironmentDiagnosticFormatResult>();
    }

    private static bool TestEnvironmentDiagnosticFormatsEmptyEnvironment()
    {
        byte[] source = ValidManifest();
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);

        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, source);
        byte[] destination = new byte[query.RequiredBytes];
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, source, destination);
        string marker = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);

        string expected =
            "NATIVE_MANIFEST_ENV status=Success initialized=true environmentCount=0 maxEnvironmentCount=16" +
            " firstKeyPresent=false firstKeyOffset=-1 firstKeyLength=0 firstValueOffset=-1 firstValueLength=0" +
            " processEnvironmentMaterialized=false hostedDotNetStarted=false kestrelStarted=false";

        return parsed.Succeeded
            && parsed.Manifest.EnvironmentCount == 0
            && query.Status == NativeManifestEnvironmentDiagnosticFormatStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && marker == expected;
    }

    private static bool TestEnvironmentDiagnosticReportsShortDestination()
    {
        byte[] source = ManifestWithEnvironment("env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, source);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, source, destination);

        return parsed.Succeeded
            && query.RequiredBytes > 1
            && format.Status == NativeManifestEnvironmentDiagnosticFormatStatus.DestinationTooSmall
            && format.ParseStatus == NativeManifestParseStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC);
    }

    private static bool TestEnvironmentDiagnosticRejectsDefaultResult()
    {
        NativeManifestParseResult parsed = default;
        byte[] source = ValidManifest();

        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, source);
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, source, stackalloc byte[64]);

        return !parsed.IsInitialized
            && query.Status == NativeManifestEnvironmentDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && format.Status == NativeManifestEnvironmentDiagnosticFormatStatus.InvalidResult
            && format.BytesWritten == 0;
    }

    private static bool TestEnvironmentDiagnosticRejectsInvalidSourceRange()
    {
        byte[] source = ManifestWithEnvironment("env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentEntry first = parsed.Manifest.GetEnvironmentEntry(0);
        ReadOnlySpan<byte> truncatedSource = source.AsSpan(0, first.Value.Offset + 1);

        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, truncatedSource);
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, truncatedSource, stackalloc byte[256]);

        return parsed.Succeeded
            && first.Value.Length > 1
            && query.Status == NativeManifestEnvironmentDiagnosticFormatStatus.SourceRangeInvalid
            && query.RequiredBytes == 0
            && format.Status == NativeManifestEnvironmentDiagnosticFormatStatus.SourceRangeInvalid
            && format.BytesWritten == 0;
    }

    private static bool TestEnvironmentDiagnosticFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Manifest",
                "NativeManifestEnvironmentDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.Manifest.EnvironmentCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(NativeManifestParser.MaximumEnvironmentCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(firstKeyOffset)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(firstValueLength)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceManifestEnvironmentReady()
    {
        byte[] source = ManifestWithEnvironment(
            "env.ASPNETCORE_ENVIRONMENT=Production\n" +
            "env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, source);
        byte[] destination = new byte[query.RequiredBytes];
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(parsed, source, destination);
        string payload = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
        string expectedMarker = BootSequence.NativeManifestEnvironmentReadyPrefix + payload;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestEnvironmentReadiness(
            parsed,
            source,
            serial,
            events);

        return parsed.Succeeded
            && format.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.StartsWith(
                "NATIVE_MANIFEST_ENV_READY NATIVE_MANIFEST_ENV status=Success initialized=true environmentCount=2",
                StringComparison.Ordinal)
            && expectedMarker.Contains("processEnvironmentMaterialized=false", StringComparison.Ordinal)
            && expectedMarker.Contains("hostedDotNetStarted=false", StringComparison.Ordinal)
            && expectedMarker.Contains("kestrelStarted=false", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceManifestEnvironmentRejectsParseFailure()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=1\n" +
            "app.name=fetch.app\n" +
            "app.path=apps/fetch.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestEnvironmentReadiness(
            parsed,
            source,
            serial,
            events);
        string expectedMarker =
            "NATIVE_MANIFEST_ENV_NOT_READY status=ParseFailure initialized=true parse=UnsupportedVersion" +
            " format=InvalidResult environmentCount=0 errorOffset=34 bytesWritten=0 requiredBytes=0" +
            " availableBytes=512 processEnvironmentMaterialized=false hostedDotNetStarted=false" +
            " kestrelStarted=false";

        return !parsed.Succeeded
            && parsed.IsInitialized
            && parsed.Status == NativeManifestParseStatus.UnsupportedVersion
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestEnvironmentReadinessStatus.ParseFailure),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestBootSequenceManifestEnvironmentRejectsInvalidSourceRange()
    {
        byte[] source = ManifestWithEnvironment("env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentEntry first = parsed.Manifest.GetEnvironmentEntry(0);
        ReadOnlySpan<byte> truncatedSource = source.AsSpan(0, first.Value.Offset + 1);
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(parsed, truncatedSource);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestEnvironmentReadiness(
            parsed,
            truncatedSource,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeManifestEnvironmentNotReadyMarker(
            NativeManifestEnvironmentReadinessStatus.SourceRangeInvalid,
            parsed,
            format);

        return parsed.Succeeded
            && format.Status == NativeManifestEnvironmentDiagnosticFormatStatus.SourceRangeInvalid
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestEnvironmentReadinessStatus.SourceRangeInvalid),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVE_MANIFEST_ENV_NOT_READY status=SourceRangeInvalid initialized=true parse=Success" +
                " format=SourceRangeInvalid environmentCount=1 errorOffset=-1 bytesWritten=0 requiredBytes=0" +
                " availableBytes=0 processEnvironmentMaterialized=false hostedDotNetStarted=false" +
                " kestrelStarted=false";
    }

    private static bool TestBootSequenceManifestEnvironmentRejectsDefaultResult()
    {
        NativeManifestParseResult parsed = default;
        byte[] source = ValidManifest();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestEnvironmentReadiness(
            parsed,
            source,
            serial,
            events);
        string expectedMarker =
            "NATIVE_MANIFEST_ENV_NOT_READY status=NotInitialized initialized=false parse=Success" +
            " format=InvalidResult environmentCount=0 errorOffset=0 bytesWritten=0 requiredBytes=0" +
            " availableBytes=512 processEnvironmentMaterialized=false hostedDotNetStarted=false" +
            " kestrelStarted=false";

        return !parsed.IsInitialized
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestEnvironmentReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestBootSequenceManifestEnvironmentFormatsDiagnosticFailureMarker()
    {
        byte[] source = ManifestWithEnvironment("env.PORT=8080\n");
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatResult.Fail(
                NativeManifestEnvironmentDiagnosticFormatStatus.DestinationTooSmall,
                parsed.Status,
                requiredBytes: 256,
                availableBytes: 8);

        string marker = BootSequence.CreateNativeManifestEnvironmentNotReadyMarker(
            NativeManifestEnvironmentReadinessStatus.DiagnosticFormattingFailure,
            parsed,
            format);

        return parsed.Succeeded
            && marker ==
                "NATIVE_MANIFEST_ENV_NOT_READY status=DiagnosticFormattingFailure initialized=true parse=Success" +
                " format=DestinationTooSmall environmentCount=1 errorOffset=-1 bytesWritten=0 requiredBytes=256" +
                " availableBytes=8 processEnvironmentMaterialized=false hostedDotNetStarted=false" +
                " kestrelStarted=false";
    }

    private static bool TestBootSequenceManifestEnvironmentPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        return !serial.ContainsPrefix(BootSequence.NativeManifestEnvironmentReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.NativeManifestEnvironmentNotReadyPrefix);
    }

    private static bool TestParsesListenEndpoint()
    {
        return ParsesListenEndpoint("0.0.0.0:8080", "0.0.0.0", 8080)
            && ParsesListenEndpoint("api.local:65535", "api.local", 65535);
    }

    private static bool TestRejectsInvalidListenEndpoint()
    {
        return RejectsListenEndpoint("0.0.0.0", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint(":8080", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("bad host:8080", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("0.0.0.0:0", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("0.0.0.0:65536", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("0.0.0.0:4294967296", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("0.0.0.0:http", "dhcp", NativeManifestParseStatus.InvalidListenEndpoint)
            && RejectsListenEndpoint("0.0.0.0:8080", "none", NativeManifestParseStatus.ListenRequiresNetwork);
    }

    private static bool TestRejectsDuplicateEnvironmentKeys()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.PORT=8080\n" +
            "env.PORT=9090\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestParseStatus.DuplicateEnvironmentKey;
    }

    private static bool TestRejectsInvalidEnvironmentKey()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.port=8080\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestParseStatus.InvalidEnvironmentKey;
    }

    private static bool TestRejectsTooManyEnvironmentFields()
    {
        StringBuilder builder = new StringBuilder()
            .Append("format=os-native-manifest\n")
            .Append("version=0\n")
            .Append("app.name=hello.app\n")
            .Append("app.path=apps/hello.bin\n")
            .Append("args=\n")
            .Append("app.kind=package\n")
            .Append("runtime.kind=native-aot\n")
            .Append("network=dhcp\n")
            .Append("log=serial\n");

        for (int index = 0; index <= NativeManifestParser.MaximumEnvironmentCount; index++)
        {
            builder.Append("env.KEY")
                .Append(index)
                .Append("=value\n");
        }

        NativeManifestParseResult result = NativeManifestParser.Parse(Bytes(builder.ToString()));
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestParseStatus.TooManyEnvironmentFields;
    }

    private static bool TestAcceptsMaximumStringFieldLengths()
    {
        string appName = new string('a', NativeManifestParser.MaximumAppNameLength);
        string appPath = "apps/" + new string('b', NativeManifestParser.MaximumAppPathLength - 5);
        string args = new string('c', NativeManifestParser.MaximumArgsLength);
        string environmentKey = new string('D', NativeManifestParser.MaximumEnvironmentKeyLength);
        string environmentValue = new string('e', NativeManifestParser.MaximumEnvironmentValueLength);
        string listenHost = new string('f', NativeManifestParser.MaximumListenHostLength);
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=" + appName + "\n" +
            "app.path=" + appPath + "\n" +
            "args=" + args + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "listen=" + listenHost + ":65535\n" +
            "log=serial\n" +
            "env." + environmentKey + "=" + environmentValue + "\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return result.Succeeded
            && FieldEquals(source, result.Manifest.AppName, appName)
            && FieldEquals(source, result.Manifest.AppPath, appPath)
            && FieldEquals(source, result.Manifest.Args, args)
            && result.Manifest.HasListenEndpoint
            && result.Manifest.ListenEndpoint.Port == 65535
            && FieldEquals(source, result.Manifest.ListenEndpoint.Host, listenHost)
            && result.Manifest.EnvironmentCount == 1
            && EnvironmentEntryEquals(
                source,
                result.Manifest.GetEnvironmentEntry(0),
                environmentKey,
                environmentValue);
    }

    private static bool TestRejectsOversizedStringFields()
    {
        return RejectsNativeManifest(
                "app.name=" + new string('a', NativeManifestParser.MaximumAppNameLength + 1),
                NativeManifestParseStatus.AppNameTooLong)
            && RejectsNativeManifest(
                "app.path=apps/" + new string('b', NativeManifestParser.MaximumAppPathLength - 4),
                NativeManifestParseStatus.AppPathTooLong)
            && RejectsNativeManifest(
                "args=" + new string('c', NativeManifestParser.MaximumArgsLength + 1),
                NativeManifestParseStatus.ArgsTooLong)
            && RejectsNativeManifest(
                "env." + new string('D', NativeManifestParser.MaximumEnvironmentKeyLength + 1) + "=value",
                NativeManifestParseStatus.EnvironmentKeyTooLong)
            && RejectsNativeManifest(
                "env.KEY=" + new string('e', NativeManifestParser.MaximumEnvironmentValueLength + 1),
                NativeManifestParseStatus.EnvironmentValueTooLong)
            && RejectsNativeManifest(
                "listen=" + new string('f', NativeManifestParser.MaximumListenHostLength + 1) + ":8080",
                NativeManifestParseStatus.InvalidListenEndpoint);
    }

    private static bool TestRejectsUnsupportedRuntimeKind()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=dotnet10\n" +
            "network=dhcp\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.UnsupportedRuntimeKind;
    }

    private static bool TestRejectsMissingAppPath()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.MissingAppPath;
    }

    private static bool TestRejectsMalformedNetworkMode()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=automatic\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.InvalidNetworkMode;
    }

    private static bool TestRejectsUnknownVersion()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=1\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.UnsupportedVersion;
    }

    private static bool TestRejectsUnknownFormat()
    {
        byte[] source = Bytes(
            "format=json\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.UnsupportedFormat;
    }

    private static bool TestRejectsDuplicateKeys()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.name=other.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.DuplicateKey;
    }

    private static bool TestRejectsOversizedInput()
    {
        byte[] source = new byte[NativeManifestParser.MaximumInputLength + 1];
        for (int index = 0; index < source.Length; index++)
        {
            source[index] = (byte)'a';
        }

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.InputTooLarge;
    }

    private static bool TestRejectsNonAsciiInput()
    {
        byte[] source = ValidManifest();
        source[10] = 0x80;

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.NonAsciiOrControlCharacter;
    }

    private static bool TestRejectsControlCharacters()
    {
        byte[] source = ValidManifest();
        source[10] = (byte)'\t';

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.NonAsciiOrControlCharacter;
    }

    private static bool TestRejectsUnknownKeys()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "runtime=native-aot\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.UnknownKey;
    }

    private static bool TestRejectsMalformedLogMode()
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=file\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == NativeManifestParseStatus.InvalidLogMode;
    }

    private static bool TestPreservesTokenSplitterSemantics()
    {
        byte[] crlfSource = Bytes(
            "format=os-native-manifest\r\n" +
            "version=0\r\n" +
            "app.name=hello.app\r\n" +
            "app.path=apps/hello.bin\r\n" +
            "args=\r\n" +
            "app.kind=package\r\n" +
            "runtime.kind=native-aot\r\n" +
            "network=dhcp\r\n" +
            "log=serial\r\n");
        NativeManifestParseResult crlfResult = NativeManifestParser.Parse(crlfSource);

        byte[] equalsValueSource = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.CONFIG=a=b=c\n");
        NativeManifestParseResult equalsValueResult = NativeManifestParser.Parse(equalsValueSource);

        byte[] tabSource = ValidManifest();
        tabSource[10] = (byte)'\t';
        NativeManifestParseResult tabResult = NativeManifestParser.Parse(tabSource);

        byte[] bareCrSource = ValidManifest();
        bareCrSource[10] = (byte)'\r';
        NativeManifestParseResult bareCrResult = NativeManifestParser.Parse(bareCrSource);

        return crlfResult.Succeeded
            && equalsValueResult.Succeeded
            && equalsValueResult.Manifest.EnvironmentCount == 1
            && EnvironmentEntryEquals(
                equalsValueSource,
                equalsValueResult.Manifest.GetEnvironmentEntry(0),
                "CONFIG",
                "a=b=c")
            && !tabResult.Succeeded
            && tabResult.Status == NativeManifestParseStatus.NonAsciiOrControlCharacter
            && tabResult.ErrorOffset == 10
            && !bareCrResult.Succeeded
            && bareCrResult.Status == NativeManifestParseStatus.MalformedLine
            && bareCrResult.ErrorOffset == 10;
    }

    private static bool TestPreservesEqualsInsideValues()
    {
        byte[] source = ManifestWithEnvironment(
            "env.CONFIG=a=b=c\n" +
            "env.FETCH_HTTP_EXPECTED_MARKER=left=right=tail\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);

        return result.Succeeded
            && result.Manifest.EnvironmentCount == 2
            && EnvironmentEntryEquals(
                source,
                result.Manifest.GetEnvironmentEntry(0),
                "CONFIG",
                "a=b=c")
            && EnvironmentEntryEquals(
                source,
                result.Manifest.GetEnvironmentEntry(1),
                "FETCH_HTTP_EXPECTED_MARKER",
                "left=right=tail");
    }

    private static bool TestUsesSharedKeyValueSplitter()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeManifestParser.cs"));

        return parserSource.Contains("AsciiKeyValueSplitter.SplitRange", StringComparison.Ordinal)
            && !parserSource.Contains("Span<AsciiTokenRange> keyValueTokens", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiEqualityHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeManifestParser.cs"));

        return parserSource.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(key, \"format\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(key, \"app.kind\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(key, \"runtime.kind\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"os-native-manifest\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"0\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"package\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"native-aot\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"dhcp\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(value, \"serial\"u8)", StringComparison.Ordinal)
            && !parserSource.Contains("AsciiEquals(", StringComparison.Ordinal)
            && !parserSource.Contains(
                "private static bool AsciiEquals(ReadOnlySpan<byte>",
                StringComparison.Ordinal);
    }

    private static bool TestUsesSharedMemoryEnvironmentHelpers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeManifestParser.cs"));

        return parserSource.Contains(
                "OS.Std.MemoryExtensions.StartsWith(key, \"env.\"u8)",
                StringComparison.Ordinal)
            && parserSource.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(existingKey, key)",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "private static bool HasPrefix(ReadOnlySpan<byte>",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "private static bool ByteSequenceEqual(ReadOnlySpan<byte>",
                StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiClassificationHelpers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeManifestParser.cs"));

        return parserSource.Contains("OS.Std.Ascii.IsLetterOrDigit(value)", StringComparison.Ordinal)
            && parserSource.Contains("OS.Std.Ascii.IsUpperLetter", StringComparison.Ordinal)
            && parserSource.Contains("OS.Std.Ascii.IsDigit", StringComparison.Ordinal)
            && !parserSource.Contains("IsAsciiUpperAlpha", StringComparison.Ordinal)
            && !parserSource.Contains("IsAsciiLowerAlpha", StringComparison.Ordinal)
            && !parserSource.Contains("IsAsciiDigit", StringComparison.Ordinal);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeManifestParseResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeManifestParseStatus.Success
            && result.ErrorOffset == 0
            && !result.Manifest.AppName.IsPresent
            && !result.Manifest.AppPath.IsPresent
            && !result.Manifest.Args.IsPresent
            && result.Manifest.AppKind == NativeManifestAppKind.None
            && result.Manifest.RuntimeKind == NativeManifestRuntimeKind.None
            && result.Manifest.NetworkMode == NativeManifestNetworkMode.None
            && result.Manifest.LogMode == NativeManifestLogMode.None
            && result.Manifest.EnvironmentCount == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeManifestParseResult>();
    }

    private static bool TestFetchAppConfigPreflightValidConfig()
    {
        byte[] source = ManifestWithEnvironment(
            "env.FETCH_HTTP_EXPECTED_STATUS=204\n" +
            "env.FETCH_HTTP_EXPECTED_MARKER=CONFIG_OK\n");
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];

        NativeManifestFetchAppConfigPreflightResult result =
            NativeManifestFetchAppConfigPreflight.Validate(
                load,
                source,
                scratch,
                requireConfiguredMarker: true);

        return result.Succeeded
            && result.Status == NativeManifestFetchAppConfigPreflightStatus.Success
            && result.ManifestLoadStatus == NativeManifestLoadStatus.Success
            && result.ManifestParseStatus == NativeManifestParseStatus.Success
            && result.ExpectedHttpStatusCode == 204
            && result.ExpectedMarkerLength == "CONFIG_OK".Length
            && result.MarkerBytesWritten == "CONFIG_OK".Length
            && result.MarkerRequiredBytes == "CONFIG_OK".Length
            && result.HasConfiguredStatus
            && result.HasConfiguredMarker
            && !result.UsesDefaultMarker
            && ScratchEquals(scratch, result.MarkerBytesWritten, "CONFIG_OK")
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeManifestFetchAppConfigPreflightResult>();
    }

    private static bool TestFetchAppConfigPreflightDefaults()
    {
        byte[] source = ValidManifest();
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];

        NativeManifestFetchAppConfigPreflightResult result =
            NativeManifestFetchAppConfigPreflight.Validate(load, source, scratch);

        return result.Succeeded
            && result.Status == NativeManifestFetchAppConfigPreflightStatus.Success
            && result.ExpectedHttpStatusCode == 200
            && result.ExpectedMarkerLength == "FETCH_OK".Length
            && result.MarkerBytesWritten == "FETCH_OK".Length
            && result.UsesDefaultMarker
            && !result.HasConfiguredStatus
            && !result.HasConfiguredMarker
            && ScratchEquals(scratch, result.MarkerBytesWritten, "FETCH_OK");
    }

    private static bool TestFetchAppConfigPreflightInvalidStatus()
    {
        return FetchAppPreflightStatusIs(
                "env.FETCH_HTTP_EXPECTED_STATUS=99\n",
                NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode)
            && FetchAppPreflightStatusIs(
                "env.FETCH_HTTP_EXPECTED_STATUS=600\n",
                NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode)
            && FetchAppPreflightStatusIs(
                "env.FETCH_HTTP_EXPECTED_STATUS=OK\n",
                NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode);
    }

    private static bool TestFetchAppConfigPreflightEmptyMarker()
    {
        byte[] source = ManifestWithEnvironment("env.FETCH_HTTP_EXPECTED_MARKER=\n");
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];

        NativeManifestFetchAppConfigPreflightResult result =
            NativeManifestFetchAppConfigPreflight.Validate(load, source, scratch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestFetchAppConfigPreflightStatus.EmptyMarker
            && result.ExpectationStatus == OS.Net.FetchAppHttpCompletionExpectationStatus.EmptyMarker
            && result.ExpectedMarkerLength == 0
            && result.MarkerBytesWritten == 0
            && result.HasConfiguredMarker
            && !result.UsesDefaultMarker;
    }

    private static bool TestFetchAppConfigPreflightOversizedMarker()
    {
        string marker = new string('A', OS.Net.FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength + 1);
        byte[] source = ManifestWithEnvironment("env.FETCH_HTTP_EXPECTED_BODY=" + marker + "\n");
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];

        NativeManifestFetchAppConfigPreflightResult result =
            NativeManifestFetchAppConfigPreflight.Validate(load, source, scratch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestFetchAppConfigPreflightStatus.MarkerTooLarge
            && result.ExpectedMarkerLength == marker.Length
            && result.MarkerRequiredBytes == marker.Length
            && result.HasConfiguredMarker
            && !result.UsesDefaultMarker;
    }

    private static bool TestFetchAppConfigPreflightManifestNotReady()
    {
        Span<byte> scratch = stackalloc byte[64];
        NativeManifestFetchAppConfigPreflightResult defaultResult =
            NativeManifestFetchAppConfigPreflight.Validate(default, ReadOnlySpan<byte>.Empty, scratch);
        NativeManifestFetchAppConfigPreflightResult parseFailureResult =
            NativeManifestFetchAppConfigPreflight.Validate(
                NativeManifestLoadResult.ManifestFail(NativeManifestParseStatus.UnsupportedVersion, 32),
                ReadOnlySpan<byte>.Empty,
                scratch);

        return !defaultResult.Succeeded
            && defaultResult.IsInitialized
            && defaultResult.Status == NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady
            && defaultResult.ManifestLoadStatus == NativeManifestLoadStatus.Success
            && !parseFailureResult.Succeeded
            && parseFailureResult.IsInitialized
            && parseFailureResult.Status == NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady
            && parseFailureResult.ManifestLoadStatus == NativeManifestLoadStatus.ManifestParseFailed
            && parseFailureResult.ManifestParseStatus == NativeManifestParseStatus.UnsupportedVersion;
    }

    private static bool TestBootSequenceFetchAppConfigPreflightReady()
    {
        byte[] source = ManifestWithEnvironment(
            "env.FETCH_HTTP_EXPECTED_STATUS=204\n" +
            "env.FETCH_HTTP_EXPECTED_MARKER=CONFIG_OK\n");
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];
        NativeManifestFetchAppConfigPreflightResult preflight =
            NativeManifestFetchAppConfigPreflight.Validate(
                load,
                source,
                scratch,
                requireConfiguredMarker: true);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeManifestFetchAppConfigPreflightReadyMarker(preflight);

        return preflight.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_CONFIG_PREFLIGHT_READY status=Ready initialized=true preflight=Success manifestLoad=Success manifestParse=Success expectation=Success expectedStatus=204 markerLength=9 markerBytesWritten=9 markerRequiredBytes=9 usesDefaultMarker=false configuredStatus=true configuredMarker=true manifestBytesRead=" + source.Length;
    }

    private static bool TestBootSequenceFetchAppConfigPreflightRejectsFailedResult()
    {
        byte[] source = ManifestWithEnvironment("env.FETCH_HTTP_EXPECTED_STATUS=OK\n");
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];
        NativeManifestFetchAppConfigPreflightResult preflight =
            NativeManifestFetchAppConfigPreflight.Validate(load, source, scratch);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeManifestFetchAppConfigPreflightNotReadyMarker(
                NativeManifestFetchAppConfigPreflightReadinessStatus.PreflightFailed,
                preflight);

        return preflight.IsInitialized
            && !preflight.Succeeded
            && preflight.Status == NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestFetchAppConfigPreflightReadinessStatus.PreflightFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_CONFIG_PREFLIGHT_NOT_READY status=PreflightFailed initialized=true preflight=InvalidStatusCode manifestLoad=Success manifestParse=Success expectation=InvalidStatusCode expectedStatus=0 markerLength=8 markerBytesWritten=0 markerRequiredBytes=8 usesDefaultMarker=true configuredStatus=true configuredMarker=false manifestBytesRead=" + source.Length;
    }

    private static bool TestBootSequenceFetchAppConfigPreflightRejectsDefaultResult()
    {
        NativeManifestFetchAppConfigPreflightResult preflight = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeManifestFetchAppConfigPreflightNotReadyMarker(
                NativeManifestFetchAppConfigPreflightReadinessStatus.NotInitialized,
                preflight);

        return !preflight.IsInitialized
            && !preflight.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestFetchAppConfigPreflightReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCHAPP_CONFIG_PREFLIGHT_NOT_READY status=NotInitialized initialized=false preflight=Success manifestLoad=Success manifestParse=Success expectation=Success expectedStatus=0 markerLength=0 markerBytesWritten=0 markerRequiredBytes=0 usesDefaultMarker=false configuredStatus=false configuredMarker=false manifestBytesRead=0";
    }

    private static bool TestBootSequenceFetchAppConfigPreflightPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        return !serial.ContainsPrefix(BootSequence.NativeManifestFetchAppConfigPreflightReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.NativeManifestFetchAppConfigPreflightNotReadyPrefix);
    }

    private static bool RejectsListenEndpoint(
        string listenEndpoint,
        string networkMode,
        NativeManifestParseStatus expectedStatus)
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=" + networkMode + "\n" +
            "listen=" + listenEndpoint + "\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.Status == expectedStatus;
    }

    private static bool RejectsNativeManifest(string replacementLine, NativeManifestParseStatus expectedStatus)
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            (replacementLine.StartsWith("app.name=", StringComparison.Ordinal) ? replacementLine : "app.name=hello.app") + "\n" +
            (replacementLine.StartsWith("app.path=", StringComparison.Ordinal) ? replacementLine : "app.path=apps/hello.bin") + "\n" +
            (replacementLine.StartsWith("args=", StringComparison.Ordinal) ? replacementLine : "args=") + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            (replacementLine.StartsWith("listen=", StringComparison.Ordinal) ? replacementLine + "\n" : string.Empty) +
            "log=serial\n" +
            (replacementLine.StartsWith("env.", StringComparison.Ordinal) ? replacementLine + "\n" : string.Empty));

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == expectedStatus;
    }

    private static bool ParsesListenEndpoint(
        string listenEndpoint,
        string expectedHost,
        ushort expectedPort)
    {
        byte[] source = Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=minimal.api\n" +
            "app.path=apps/minimal-api/app.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "listen=" + listenEndpoint + "\n" +
            "log=serial\n");

        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        return result.Succeeded
            && result.Manifest.HasListenEndpoint
            && result.Manifest.ListenEndpoint.IsPresent
            && result.Manifest.ListenEndpoint.Port == expectedPort
            && FieldEquals(source, result.Manifest.ListenEndpoint.Host, expectedHost);
    }

    private static byte[] ValidManifest()
    {
        return Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static byte[] ManifestWithEnvironment(string environmentLines)
    {
        return Bytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=fetch.app\n" +
            "app.path=apps/fetch.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            environmentLines);
    }

    private static NativeManifestLoadResult CreateManifestLoad(byte[] source)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("test manifest did not parse: " + parsed.Status);
        }

        return NativeManifestLoadResult.Success(parsed.Manifest, checked((uint)source.Length));
    }

    private static bool FetchAppPreflightStatusIs(
        string environmentLine,
        NativeManifestFetchAppConfigPreflightStatus expectedStatus)
    {
        byte[] source = ManifestWithEnvironment(environmentLine);
        NativeManifestLoadResult load = CreateManifestLoad(source);
        Span<byte> scratch = stackalloc byte[64];

        NativeManifestFetchAppConfigPreflightResult result =
            NativeManifestFetchAppConfigPreflight.Validate(load, source, scratch);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == expectedStatus
            && result.MarkerBytesWritten == 0;
    }

    private static bool FieldEquals(byte[] source, NativeManifestField field, string expected)
    {
        if (!field.TryGetSlice(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool EnvironmentEntryEquals(
        byte[] source,
        NativeManifestEnvironmentEntry entry,
        string expectedKey,
        string expectedValue)
    {
        return entry.IsPresent
            && FieldEquals(source, entry.Key, expectedKey)
            && FieldEquals(source, entry.Value, expectedValue);
    }

    private static bool ScratchEquals(Span<byte> scratch, int length, string expected)
    {
        if (length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < length; index++)
        {
            if (scratch[index] != (byte)expected[index])
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

    private static byte[] Bytes(string value)
    {
        return Encoding.ASCII.GetBytes(value);
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
