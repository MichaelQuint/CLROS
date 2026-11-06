using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class NativeAppPackageStagingFormatterTests
{

    [HostTest("appmodel package staging formatter formats not-ready marker", HostLane.Fast, 0)]
    private static bool TestFormatsNotReadyMarker()
    {
        NativeAppPackageStagingReadinessResult readiness = CreatePayloadStagingFailure();
        Span<byte> destination = stackalloc byte[NativeAppPackageStagingReadinessFormatter.MaximumMarkerBytes];

        NativeAppPackageStagingMarkerFormatResult result =
            NativeAppPackageStagingReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = BootSequence.CreateAppPackageStagingNotReadyMarker(
            AppPackageStagingReadinessStatus.StagingReadinessFailed,
            readiness);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.BytesWritten == result.RequiredBytes
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(BootSequence.AppPackageStagingNotReadyPrefix, StringComparison.Ordinal)
            && marker.Contains("payloadStaging=DestinationTooSmall", StringComparison.Ordinal)
            && marker.Contains("executableBytes=0", StringComparison.Ordinal));
    }

    [HostTest("appmodel package staging formatter reports short destination", HostLane.Fast, 1)]
    private static bool TestReportsShortDestination()
    {
        NativeAppPackageStagingReadinessResult readiness = CreatePayloadStagingFailure();
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xCC);

        NativeAppPackageStagingMarkerFormatResult query =
            NativeAppPackageStagingReadinessFormatter.QueryNotReadyMarkerRequiredBytes(readiness);
        NativeAppPackageStagingMarkerFormatResult result =
            NativeAppPackageStagingReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return HostCheck.Result(query.IsInitialized
            && query.Status == NativeAppPackageStagingMarkerFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && destination[0] == 0xCC
            && destination[^1] == 0xCC);
    }

    [HostTest("appmodel package staging formatter formats default readiness", HostLane.Fast, 2)]
    private static bool TestFormatsDefaultReadiness()
    {
        NativeAppPackageStagingReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[NativeAppPackageStagingReadinessFormatter.MaximumMarkerBytes];

        NativeAppPackageStagingMarkerFormatResult result =
            NativeAppPackageStagingReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = BootSequence.CreateAppPackageStagingNotReadyMarker(
            AppPackageStagingReadinessStatus.StagingReadinessFailed,
            readiness);

        return HostCheck.Result(!readiness.IsInitialized
            && !readiness.Succeeded
            && result.Succeeded
            && marker == expected
            && marker.Contains("initialized=false", StringComparison.Ordinal)
            && marker.Contains("readiness=Success", StringComparison.Ordinal)
            && marker.EndsWith("entryOffset=0", StringComparison.Ordinal));
    }

    [HostTest("appmodel package staging formatter rejects invalid status", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidStatus()
    {
        NativeAppPackageStagingReadinessResult invalid = NativeAppPackageStagingReadinessResult.Fail(
            (NativeAppPackageStagingReadinessStatus)999,
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppPayloadStagingStatus.Success,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success,
            appBinaryFileIndex: 0,
            packageFileBytesStaged: 1,
            payloadOffsetInPackage: 2,
            executableOffsetInPackage: 3,
            payloadExecutableBytesStaged: 4,
            requiredWritableStagingBytes: 5,
            requiredStackBytes: 6,
            entryOffsetInPackage: 7);
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        NativeAppPackageStagingMarkerFormatResult result =
            NativeAppPackageStagingReadinessFormatter.TryFormatNotReadyMarker(invalid, destination);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.InvalidStatus
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && destination[0] == 0xAA
            && destination[^1] == 0xAA);
    }

    [HostTest("appmodel package staging formatter default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPackageStagingMarkerFormatResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingMarkerFormatResult>());
    }

    [HostTest("appmodel package staging formatter uses shared ascii text builder", HostLane.Fast, 5)]
    private static bool TestUsesSharedAsciiTextBuilder()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        string owner = typeof(NativeAppPackageStagingReadinessFormatter).FullName!;
        string[] counters = ["AppBinaryFileIndex", "PackageFileBytesStaged", "PayloadOffsetInPackage", "ExecutableOffsetInPackage",
            "PayloadExecutableBytesStaged", "RequiredWritableStagingBytes", "RequiredStackBytes", "EntryOffsetInPackage"];
        return HostCheck.Result(catalog.Type(owner).GetMembers("CountUnsignedDigits").Length == 0
            && catalog.Type(owner).GetMembers("WriteUnsigned").Length == 0
            && HostFormatterSourceGuard.CountFields(catalog, owner, "CountNotReadyMarkerBytes",
                counters.Select(field => (field, "CountUInt64Digits")).ToArray())
            && HostFormatterSourceGuard.Append(catalog, owner, "TryWrite", "AppendBytes", tryStyle: true)
            && HostFormatterSourceGuard.Append(catalog, owner, "TryWriteUnsigned", "AppendUInt64Decimal", tryStyle: true));
    }

    private static NativeAppPackageStagingReadinessResult CreatePayloadStagingFailure()
    {
        return NativeAppPackageStagingReadinessResult.Fail(
            NativeAppPackageStagingReadinessStatus.PayloadStagingFailed,
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppPayloadStagingStatus.DestinationTooSmall,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success,
            appBinaryFileIndex: 2,
            packageFileBytesStaged: 512,
            payloadOffsetInPackage: 4096,
            executableOffsetInPackage: 4128,
            payloadExecutableBytesStaged: 0,
            requiredWritableStagingBytes: 4096,
            requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes,
            entryOffsetInPackage: 4136);
    }
}
