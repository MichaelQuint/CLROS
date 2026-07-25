using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class BootSequenceAppEntryReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("boot.sequence app entry ready", TestAppEntryReady, ref failures);
        Run(
            "boot.sequence app entry rejects dependency failure",
            TestAppEntryRejectsDependencyFailure,
            ref failures);
        Run(
            "boot.sequence app entry rejects staging preflight failure",
            TestAppEntryRejectsStagingPreflightFailure,
            ref failures);
        Run(
            "boot.sequence app entry blocks default staging preflight",
            TestAppEntryBlocksDefaultStagingPreflight,
            ref failures);
        Run(
            "boot.sequence app entry preserves default markers",
            TestAppEntryPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence app entry readiness host tests failed: " + failures);
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

    private static bool TestAppEntryReady()
    {
        AppEntryInputs inputs = CreateReadyInputs();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppEntryReadiness(
            inputs.ManifestLoad,
            inputs.PackageParse,
            inputs.LaunchPlan,
            inputs.Descriptor,
            inputs.StagingPreflight,
            inputs.MappingPlan,
            inputs.MemoryPlan,
            inputs.InvocationPlan,
            serial,
            events);

        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppEntryReady
            && events.Snapshot()[0] == BootSequence.AppEntryReady;
    }

    private static bool TestAppEntryRejectsDependencyFailure()
    {
        AppEntryInputs inputs = CreateReadyInputs();
        NativeAppPayloadMappingPlanResult mappingPlan = NativeAppPayloadMappingPlanResult.Fail(
            NativeAppPayloadMappingPlanStatus.PayloadTooSmall,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppEntryReadiness(
            inputs.ManifestLoad,
            inputs.PackageParse,
            inputs.LaunchPlan,
            inputs.Descriptor,
            inputs.StagingPreflight,
            mappingPlan,
            inputs.MemoryPlan,
            inputs.InvocationPlan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppEntryNotReadyMarker(
            AppEntryReadinessStatus.PayloadMappingFailed,
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppEntryStagingPreflightStatus.Success,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadStagingStatus.Success,
            NativeAppPayloadMappingPlanStatus.PayloadTooSmall,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(AppEntryReadinessStatus.PayloadMappingFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppPayloadMappingPlanStatus.PayloadTooSmall),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestAppEntryRejectsStagingPreflightFailure()
    {
        AppEntryInputs inputs = CreateReadyInputs();
        NativeAppEntryStagingPreflightResult stagingPreflight =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(
                NativeAppPackageStagingReadinessResult.Fail(
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
                    entryOffsetInPackage: 4136));
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppEntryReadiness(
            inputs.ManifestLoad,
            inputs.PackageParse,
            inputs.LaunchPlan,
            inputs.Descriptor,
            stagingPreflight,
            inputs.MappingPlan,
            inputs.MemoryPlan,
            inputs.InvocationPlan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppEntryNotReadyMarker(
            AppEntryReadinessStatus.StagingPreflightFailed,
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppEntryStagingPreflightStatus.PayloadStagingFailed,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadStagingStatus.DestinationTooSmall,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success);

        return !stagingPreflight.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppEntryReadinessStatus.StagingPreflightFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppEntryStagingPreflightStatus.PayloadStagingFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestAppEntryBlocksDefaultStagingPreflight()
    {
        AppEntryInputs inputs = CreateReadyInputs();
        NativeAppEntryStagingPreflightResult stagingPreflight = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppEntryReadiness(
            inputs.ManifestLoad,
            inputs.PackageParse,
            inputs.LaunchPlan,
            inputs.Descriptor,
            stagingPreflight,
            inputs.MappingPlan,
            inputs.MemoryPlan,
            inputs.InvocationPlan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppEntryNotReadyMarker(
            AppEntryReadinessStatus.StagingPreflightFailed,
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppEntryStagingPreflightStatus.Success,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadStagingStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success);

        return !stagingPreflight.IsInitialized
            && !stagingPreflight.Succeeded
            && !result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker;
    }

    private static bool TestAppEntryPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.AppEntryReady)
            && !serial.ContainsPrefix(BootSequence.AppEntryNotReadyPrefix);
    }

    private static AppEntryInputs CreateReadyInputs()
    {
        NativeAppExecutableMemoryPlanResult memoryPlan = NativeAppExecutableMemoryPlanResult.Success(
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            pageSize: NativeAppExecutableMemoryPlan.DefaultPageSize,
            executableAlignment: NativeAppExecutableMemoryPlan.DefaultExecutableAlignment,
            requiredWritableStagingBytes: 4096,
            executableBytes: 64,
            executableMapOffsetInPackage: 4096,
            executableMapByteLength: 4096,
            executableMapEndOffsetInPackage: 8192,
            executableContentOffsetInMapping: NativeAppPayloadHeader.HeaderSize,
            entryOffsetInExecutableContent: 0,
            entryOffsetInExecutableMapping: NativeAppPayloadHeader.HeaderSize,
            writableStagingProtection: NativeAppExecutableMemoryProtection.Read |
                NativeAppExecutableMemoryProtection.Write,
            executableProtection: NativeAppExecutableMemoryProtection.Read |
                NativeAppExecutableMemoryProtection.Execute,
            finalProtection: NativeAppExecutableMemoryProtection.Read |
                NativeAppExecutableMemoryProtection.Execute);
        NativeAppInvocationPlanResult invocationPlan = NativeAppInvocationPlanResult.Success(
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success,
            default);

        return new AppEntryInputs(
            NativeManifestLoadResult.Success(default, 0),
            OsPackageParseResult.Success(default),
            NativeAppLaunchPlanResult.Success(
                default,
                BootCapabilities.TextOutput,
                NativeAppLaunchRuntimeCapabilities.PackageStorage),
            NativeAppLaunchDescriptorResult.Success(default),
            CreateReadyStagingPreflight(),
            NativeAppPayloadMappingPlanResult.Success(default),
            memoryPlan,
            invocationPlan);
    }

    private static NativeAppEntryStagingPreflightResult CreateReadyStagingPreflight()
    {
        NativeAppPackageStagingReadinessResult stagingReadiness =
            NativeAppPackageStagingReadinessResult.Success(
                NativeManifestLoadStatus.Success,
                OsPackageStatus.Success,
                NativeAppLaunchStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                OsPackageFileStagingStatus.Success,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppPayloadStagingStatus.Success,
                NativeAppExecutableMemoryPlanStatus.Success,
                NativeAppInvocationPlanStatus.Success,
                appBinaryFileIndex: 2,
                packageFileBytesStaged: 512,
                payloadOffsetInPackage: 4096,
                executableOffsetInPackage: 4128,
                payloadExecutableBytesStaged: 480,
                requiredWritableStagingBytes: 4096,
                requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes,
                entryOffsetInPackage: 4136);

        return NativeAppEntryStagingPreflight.FromPackageStagingReadiness(stagingReadiness);
    }

    private readonly struct AppEntryInputs
    {
        public AppEntryInputs(
            NativeManifestLoadResult manifestLoad,
            OsPackageParseResult packageParse,
            NativeAppLaunchPlanResult launchPlan,
            NativeAppLaunchDescriptorResult descriptor,
            NativeAppEntryStagingPreflightResult stagingPreflight,
            NativeAppPayloadMappingPlanResult mappingPlan,
            NativeAppExecutableMemoryPlanResult memoryPlan,
            NativeAppInvocationPlanResult invocationPlan)
        {
            ManifestLoad = manifestLoad;
            PackageParse = packageParse;
            LaunchPlan = launchPlan;
            Descriptor = descriptor;
            StagingPreflight = stagingPreflight;
            MappingPlan = mappingPlan;
            MemoryPlan = memoryPlan;
            InvocationPlan = invocationPlan;
        }

        public NativeManifestLoadResult ManifestLoad { get; }

        public OsPackageParseResult PackageParse { get; }

        public NativeAppLaunchPlanResult LaunchPlan { get; }

        public NativeAppLaunchDescriptorResult Descriptor { get; }

        public NativeAppEntryStagingPreflightResult StagingPreflight { get; }

        public NativeAppPayloadMappingPlanResult MappingPlan { get; }

        public NativeAppExecutableMemoryPlanResult MemoryPlan { get; }

        public NativeAppInvocationPlanResult InvocationPlan { get; }
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

        public bool Contains(string value)
        {
            return _lines.Contains(value);
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
