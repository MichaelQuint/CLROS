using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class BootSequenceAppExecutableMappingTests
{

    [HostTest("boot.sequence app executable mapping ready", HostLane.Fast, 0)]
    private static bool TestExecutableMappingReady()
    {
        NativeAppPayloadMappingPlanResult mapping = NativeAppPayloadMappingPlanResult.Success(
            new NativeAppPayloadMappingPlan(
                payloadOffsetInPackage: 128,
                payloadLength: 64,
                headerLength: NativeAppPayloadHeader.HeaderSize,
                executableOffsetInPayload: NativeAppPayloadHeader.HeaderSize,
                executableOffsetInPackage: 160,
                executableLength: 32,
                entryOffsetInPayload: NativeAppPayloadHeader.HeaderSize,
                entryOffsetInExecutable: 0,
                entryOffsetInPackage: 160));
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppExecutableMappingReadiness(mapping, serial, events);

        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppExecutableMappingReady
            && events.Snapshot()[0] == BootSequence.AppExecutableMappingReady);
    }

    [HostTest("boot.sequence app executable mapping rejects mapping failure", HostLane.Fast, 1)]
    private static bool TestExecutableMappingRejectsMappingFailure()
    {
        NativeAppPayloadMappingPlanResult mapping = NativeAppPayloadMappingPlanResult.Fail(
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppExecutableMappingNotReadyMarker(
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);

        BootResult result = BootSequence.RunAppExecutableMappingReadiness(mapping, serial, events);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppPayloadMappingPlanStatus.DescriptorFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppPayloadHeaderStatus.InvalidMagic),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app executable mapping preserves default markers", HostLane.Fast, 2)]
    private static bool TestExecutableMappingPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.AppExecutableMappingReady)
            && !serial.ContainsPrefix(BootSequence.AppExecutableMappingNotReadyPrefix));
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
