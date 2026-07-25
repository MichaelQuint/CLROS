using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class BootSequenceAppExecutableMemoryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "boot.sequence app executable memory ready",
            TestExecutableMemoryReady,
            ref failures);
        Run(
            "boot.sequence app executable memory rejects plan failure",
            TestExecutableMemoryRejectsPlanFailure,
            ref failures);
        Run(
            "boot.sequence app executable memory preserves default markers",
            TestExecutableMemoryPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Boot sequence app executable memory host tests failed: " + failures);
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

    private static bool TestExecutableMemoryReady()
    {
        NativeAppExecutableMemoryPlanResult memoryPlan =
            NativeAppExecutableMemoryPlan.Create(CreateMappingPlanResult());
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppExecutableMemoryReadiness(memoryPlan, serial, events);

        return memoryPlan.Succeeded
            && memoryPlan.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && memoryPlan.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && memoryPlan.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppExecutableMemoryReady
            && events.Snapshot()[0] == BootSequence.AppExecutableMemoryReady;
    }

    private static bool TestExecutableMemoryRejectsPlanFailure()
    {
        NativeAppPayloadMappingPlanResult mappingPlan = NativeAppPayloadMappingPlanResult.Fail(
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);
        NativeAppExecutableMemoryPlanResult memoryPlan =
            NativeAppExecutableMemoryPlan.Create(mappingPlan);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppExecutableMemoryNotReadyMarker(
            NativeAppExecutableMemoryPlanStatus.MappingPlanFailed,
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);

        BootResult result = BootSequence.RunAppExecutableMemoryReadiness(memoryPlan, serial, events);

        return !memoryPlan.Succeeded
            && memoryPlan.Status == NativeAppExecutableMemoryPlanStatus.MappingPlanFailed
            && memoryPlan.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed
            && memoryPlan.DescriptorStatus == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && memoryPlan.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppExecutableMemoryPlanStatus.MappingPlanFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppPayloadHeaderStatus.InvalidMagic),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestExecutableMemoryPreservesDefaultMarkers()
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

        return !serial.Contains(BootSequence.AppExecutableMemoryReady)
            && !serial.ContainsPrefix(BootSequence.AppExecutableMemoryNotReadyPrefix);
    }

    private static NativeAppPayloadMappingPlanResult CreateMappingPlanResult()
    {
        return NativeAppPayloadMappingPlanResult.Success(
            new NativeAppPayloadMappingPlan(
                payloadOffsetInPackage: 4096,
                payloadLength: 80,
                headerLength: NativeAppPayloadHeader.HeaderSize,
                executableOffsetInPayload: NativeAppPayloadHeader.HeaderSize,
                executableOffsetInPackage: 4128,
                executableLength: 48,
                entryOffsetInPayload: NativeAppPayloadHeader.HeaderSize,
                entryOffsetInExecutable: 0,
                entryOffsetInPackage: 4128));
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
