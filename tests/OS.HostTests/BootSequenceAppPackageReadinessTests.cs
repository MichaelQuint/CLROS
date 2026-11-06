using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Storage;

internal static partial class BootSequenceAppPackageReadinessTests
{
    [HostTest("boot.sequence app package readiness emits ready marker", HostLane.Fast, 0)]
    private static bool TestEmitsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPackageReadiness(CreateReadyManifestLoad(), CreateReadyPackageParse(), CreateReadyLaunchPlan(), serial, events);
        string[] snapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded && serial.Count == 1 && serial.LineAt(0) == BootSequence.AppPackageReady && snapshot.Length == 1 && snapshot[0] == BootSequence.AppPackageReady);
    }

    [HostTest("boot.sequence app package readiness rejects manifest failure", HostLane.Fast, 1)]
    private static bool TestRejectsManifestFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        NativeManifestLoadResult manifestLoad = NativeManifestLoadResult.ManifestFail(NativeManifestParseStatus.InvalidNetworkMode, 96);
        BootResult result = BootSequence.RunAppPackageReadiness(manifestLoad, CreateReadyPackageParse(), CreateReadyLaunchPlan(), serial, events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(NativeManifestLoadStatus.ManifestParseFailed, OsPackageStatus.Success, NativeAppLaunchStatus.Success);
        return HostCheck.Result(!result.Succeeded && result.Reason.Contains(nameof(NativeManifestLoadStatus.ManifestParseFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app package readiness rejects package failure", HostLane.Fast, 2)]
    private static bool TestRejectsPackageFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPackageReadiness(CreateReadyManifestLoad(), OsPackageParseResult.Fail(OsPackageStatus.InvalidMagic), CreateReadyLaunchPlan(), serial, events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(NativeManifestLoadStatus.Success, OsPackageStatus.InvalidMagic, NativeAppLaunchStatus.Success);
        return HostCheck.Result(!result.Succeeded && result.Reason.Contains(nameof(OsPackageStatus.InvalidMagic), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app package readiness rejects launch-plan failure", HostLane.Fast, 3)]
    private static bool TestRejectsLaunchPlanFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPackageReadiness(CreateReadyManifestLoad(), CreateReadyPackageParse(), CreateLaunchPlanFailure(), serial, events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(NativeManifestLoadStatus.Success, OsPackageStatus.Success, NativeAppLaunchStatus.MissingRuntimeCapabilities);
        return HostCheck.Result(!result.Succeeded && result.Reason.Contains(nameof(NativeAppLaunchStatus.MissingRuntimeCapabilities), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    private static NativeManifestLoadResult CreateReadyManifestLoad()
    {
        byte[] source = ValidManifest();
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeManifestLoadResult.Success(parsed.Manifest, (uint)source.Length);
    }

    private static OsPackageParseResult CreateReadyPackageParse()
    {
        OsPackageHeader header = new OsPackageHeader(OsPackageParser.SupportedVersion, OsPackageParser.HeaderSize, 1, OsPackageParser.FileTableEntrySize, OsPackageParser.HeaderSize, OsPackageParser.FileTableEntrySize);
        return OsPackageParseResult.Success(header);
    }

    private static NativeAppLaunchPlanResult CreateReadyLaunchPlan()
    {
        byte[] source = ValidManifest();
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeAppLaunchPlan.Create(source, parsed.Manifest, BootInfo.CreateHost(BootCapabilities.TextOutput), NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlanFailure()
    {
        return NativeAppLaunchPlanResult.Fail(NativeAppLaunchStatus.MissingRuntimeCapabilities, BootCapabilities.TextOutput, BootCapabilities.None, BootCapabilities.None, NativeAppLaunchRuntimeCapabilities.PackageStorage, NativeAppLaunchRuntimeCapabilities.PackageStorage);
    }

    private static byte[] ValidManifest()
    {
        return Encoding.ASCII.GetBytes("format=os-native-manifest\n" + "version=0\n" + "app.name=hello.app\n" + "app.path=apps/hello.bin\n" + "args=--port=8080\n" + "network=dhcp\n" + "log=serial\n");
    }
}
