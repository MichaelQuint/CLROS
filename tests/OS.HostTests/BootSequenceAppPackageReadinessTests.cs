using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Storage;

internal static class BootSequenceAppPackageReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "boot.sequence app package readiness emits ready marker",
            TestEmitsReadyMarker,
            ref failures);
        Run(
            "boot.sequence app package readiness rejects manifest failure",
            TestRejectsManifestFailure,
            ref failures);
        Run(
            "boot.sequence app package readiness rejects package failure",
            TestRejectsPackageFailure,
            ref failures);
        Run(
            "boot.sequence app package readiness rejects launch-plan failure",
            TestRejectsLaunchPlanFailure,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence app package readiness host tests failed: " + failures);
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

    private static bool TestEmitsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageReadiness(
            CreateReadyManifestLoad(),
            CreateReadyPackageParse(),
            CreateReadyLaunchPlan(),
            serial,
            events);
        string[] snapshot = events.Snapshot();

        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppPackageReady
            && snapshot.Length == 1
            && snapshot[0] == BootSequence.AppPackageReady;
    }

    private static bool TestRejectsManifestFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        NativeManifestLoadResult manifestLoad = NativeManifestLoadResult.ManifestFail(
            NativeManifestParseStatus.InvalidNetworkMode,
            96);

        BootResult result = BootSequence.RunAppPackageReadiness(
            manifestLoad,
            CreateReadyPackageParse(),
            CreateReadyLaunchPlan(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(
            NativeManifestLoadStatus.ManifestParseFailed,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeManifestLoadStatus.ManifestParseFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestRejectsPackageFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageReadiness(
            CreateReadyManifestLoad(),
            OsPackageParseResult.Fail(OsPackageStatus.InvalidMagic),
            CreateReadyLaunchPlan(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(
            NativeManifestLoadStatus.Success,
            OsPackageStatus.InvalidMagic,
            NativeAppLaunchStatus.Success);

        return !result.Succeeded
            && result.Reason.Contains(nameof(OsPackageStatus.InvalidMagic), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestRejectsLaunchPlanFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageReadiness(
            CreateReadyManifestLoad(),
            CreateReadyPackageParse(),
            CreateLaunchPlanFailure(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppPackageNotReadyMarker(
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.MissingRuntimeCapabilities);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppLaunchStatus.MissingRuntimeCapabilities),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
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
        OsPackageHeader header = new OsPackageHeader(
            OsPackageParser.SupportedVersion,
            OsPackageParser.HeaderSize,
            1,
            OsPackageParser.FileTableEntrySize,
            OsPackageParser.HeaderSize,
            OsPackageParser.FileTableEntrySize);
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

        return NativeAppLaunchPlan.Create(
            source,
            parsed.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlanFailure()
    {
        return NativeAppLaunchPlanResult.Fail(
            NativeAppLaunchStatus.MissingRuntimeCapabilities,
            BootCapabilities.TextOutput,
            BootCapabilities.None,
            BootCapabilities.None,
            NativeAppLaunchRuntimeCapabilities.PackageStorage,
            NativeAppLaunchRuntimeCapabilities.PackageStorage);
    }

    private static byte[] ValidManifest()
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=--port=8080\n" +
            "network=dhcp\n" +
            "log=serial\n");
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
