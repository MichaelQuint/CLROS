using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class BootSequenceAppPackageStagingReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("boot.sequence app package staging ready", TestAppPackageStagingReady, ref failures);
        Run(
            "boot.sequence app package staging rejects readiness failure",
            TestAppPackageStagingRejectsReadinessFailure,
            ref failures);
        Run(
            "boot.sequence app package staging blocks default result",
            TestAppPackageStagingBlocksDefaultResult,
            ref failures);
        Run(
            "boot.sequence app package staging preserves default markers",
            TestAppPackageStagingPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence app package staging tests failed: " + failures);
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

    private static bool TestAppPackageStagingReady()
    {
        NativeAppPackageStagingReadinessResult readiness = CreateReadyReadiness();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);

        return readiness.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppPackageStagingReady
            && events.Snapshot()[0] == BootSequence.AppPackageStagingReady;
    }

    private static bool TestAppPackageStagingRejectsReadinessFailure()
    {
        NativeAppPackageStagingReadinessResult readiness = NativeAppPackageStagingReadinessResult.Fail(
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
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppPackageStagingNotReadyMarker(
            AppPackageStagingReadinessStatus.StagingReadinessFailed,
            readiness);

        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);

        return !readiness.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppPackageStagingReadinessStatus.StagingReadinessFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppPackageStagingReadinessStatus.PayloadStagingFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestAppPackageStagingBlocksDefaultResult()
    {
        NativeAppPackageStagingReadinessResult readiness = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppPackageStagingNotReadyMarker(
            AppPackageStagingReadinessStatus.StagingReadinessFailed,
            readiness);

        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);

        return !readiness.IsInitialized
            && !readiness.Succeeded
            && !result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && expectedMarker.Contains("initialized=false", StringComparison.Ordinal)
            && expectedMarker.Contains("readiness=Success", StringComparison.Ordinal);
    }

    private static bool TestAppPackageStagingPreservesDefaultMarkers()
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

        return !serial.Contains(BootSequence.AppPackageStagingReady)
            && !serial.ContainsPrefix(BootSequence.AppPackageStagingNotReadyPrefix)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingReadinessResult>();
    }

    private static NativeAppPackageStagingReadinessResult CreateReadyReadiness()
    {
        return NativeAppPackageStagingReadinessResult.Success(
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
