using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static partial class BootSequenceAppExecutableMemoryTests
{
    [HostTest("boot.sequence app executable memory ready", HostLane.Fast, 0)]
    private static bool TestExecutableMemoryReady()
    {
        NativeAppExecutableMemoryPlanResult memoryPlan = NativeAppExecutableMemoryPlan.Create(CreateMappingPlanResult());
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppExecutableMemoryReadiness(memoryPlan, serial, events);
        return HostCheck.Result(memoryPlan.Succeeded && memoryPlan.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success && memoryPlan.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success && memoryPlan.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success && result.Succeeded && serial.Count == 1 && serial.LineAt(0) == BootSequence.AppExecutableMemoryReady && events.Snapshot()[0] == BootSequence.AppExecutableMemoryReady);
    }

    [HostTest("boot.sequence app executable memory rejects plan failure", HostLane.Fast, 1)]
    private static bool TestExecutableMemoryRejectsPlanFailure()
    {
        NativeAppPayloadMappingPlanResult mappingPlan = NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.DescriptorFailed, NativeAppLaunchDescriptorStatus.InvalidPayloadHeader, NativeAppPayloadHeaderStatus.InvalidMagic);
        NativeAppExecutableMemoryPlanResult memoryPlan = NativeAppExecutableMemoryPlan.Create(mappingPlan);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppExecutableMemoryNotReadyMarker(NativeAppExecutableMemoryPlanStatus.MappingPlanFailed, NativeAppPayloadMappingPlanStatus.DescriptorFailed, NativeAppLaunchDescriptorStatus.InvalidPayloadHeader, NativeAppPayloadHeaderStatus.InvalidMagic);
        BootResult result = BootSequence.RunAppExecutableMemoryReadiness(memoryPlan, serial, events);
        return HostCheck.Result(!memoryPlan.Succeeded && memoryPlan.Status == NativeAppExecutableMemoryPlanStatus.MappingPlanFailed && memoryPlan.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed && memoryPlan.DescriptorStatus == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader && memoryPlan.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic && !result.Succeeded && result.Reason.Contains(nameof(NativeAppExecutableMemoryPlanStatus.MappingPlanFailed), StringComparison.Ordinal) && result.Reason.Contains(nameof(NativeAppPayloadHeaderStatus.InvalidMagic), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app executable memory preserves default markers", HostLane.Fast, 2)]
    private static bool TestExecutableMemoryPreservesDefaultMarkers()
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

        return HostCheck.Result(!serial.Contains(BootSequence.AppExecutableMemoryReady) && !serial.ContainsPrefix(BootSequence.AppExecutableMemoryNotReadyPrefix));
    }

    private static NativeAppPayloadMappingPlanResult CreateMappingPlanResult()
    {
        return NativeAppPayloadMappingPlanResult.Success(new NativeAppPayloadMappingPlan(payloadOffsetInPackage: 4096, payloadLength: 80, headerLength: NativeAppPayloadHeader.HeaderSize, executableOffsetInPayload: NativeAppPayloadHeader.HeaderSize, executableOffsetInPackage: 4128, executableLength: 48, entryOffsetInPayload: NativeAppPayloadHeader.HeaderSize, entryOffsetInExecutable: 0, entryOffsetInPackage: 4128));
    }
}
