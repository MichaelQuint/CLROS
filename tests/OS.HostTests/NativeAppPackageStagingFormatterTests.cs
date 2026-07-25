using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class NativeAppPackageStagingFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel package staging formatter formats not-ready marker", TestFormatsNotReadyMarker, ref failures);
        Run("appmodel package staging formatter reports short destination", TestReportsShortDestination, ref failures);
        Run("appmodel package staging formatter formats default readiness", TestFormatsDefaultReadiness, ref failures);
        Run("appmodel package staging formatter rejects invalid status", TestRejectsInvalidStatus, ref failures);
        Run("appmodel package staging formatter default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel package staging formatter uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app package staging formatter host tests failed: " + failures);
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

        return result.Succeeded
            && result.IsInitialized
            && result.BytesWritten == result.RequiredBytes
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(BootSequence.AppPackageStagingNotReadyPrefix, StringComparison.Ordinal)
            && marker.Contains("payloadStaging=DestinationTooSmall", StringComparison.Ordinal)
            && marker.Contains("executableBytes=0", StringComparison.Ordinal);
    }

    private static bool TestReportsShortDestination()
    {
        NativeAppPackageStagingReadinessResult readiness = CreatePayloadStagingFailure();
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xCC);

        NativeAppPackageStagingMarkerFormatResult query =
            NativeAppPackageStagingReadinessFormatter.QueryNotReadyMarkerRequiredBytes(readiness);
        NativeAppPackageStagingMarkerFormatResult result =
            NativeAppPackageStagingReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return query.IsInitialized
            && query.Status == NativeAppPackageStagingMarkerFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && destination[0] == 0xCC
            && destination[^1] == 0xCC;
    }

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

        return !readiness.IsInitialized
            && !readiness.Succeeded
            && result.Succeeded
            && marker == expected
            && marker.Contains("initialized=false", StringComparison.Ordinal)
            && marker.Contains("readiness=Success", StringComparison.Ordinal)
            && marker.EndsWith("entryOffset=0", StringComparison.Ordinal);
    }

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

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.InvalidStatus
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && destination[0] == 0xAA
            && destination[^1] == 0xAA;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPackageStagingMarkerFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingMarkerFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingMarkerFormatResult>();
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Runtime",
                "NativeAppPackageStagingReadinessFormatter.cs"));

        return source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(readiness.AppBinaryFileIndex)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, source)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int CountUnsignedDigits",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void WriteUnsigned",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[cursor--] = (byte)('0' +",
                StringComparison.Ordinal);
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
