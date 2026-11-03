using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static partial class NativeAppArgumentHandoffPlanTests
{
    private const string AppName = "hello.app";
    private const string AppPath = "apps/hello.bin";
    private const string Arguments = "--port=8080 --mode cloud";
    [HostTest("appmodel invocation args handoff composes vector", HostLane.Fast, 0)]
    private static bool TestComposesVector()
    {
        byte[] source = ValidManifest(Arguments);
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        NativeAppArgumentVectorPlanResult vector = NativeAppArgumentVectorPlanner.Create(source, launch);
        NativeAppInvocationPlanResult invocation = CreateInvocation(launch);
        NativeAppArgumentHandoffPlanResult result = NativeAppArgumentHandoffPlanner.Compose(invocation, vector);
        int expectedParsedBytes = "--port=8080".Length + "--mode".Length + "cloud".Length;
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == NativeAppArgumentHandoffPlanStatus.Success && result.InvocationStatus == NativeAppInvocationPlanStatus.Success && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success && result.ArgumentCount == 3 && result.ParsedArgumentBytes == expectedParsedBytes && result.RawLaunchArgumentByteLength == Arguments.Length && !result.ExecutesApp && !result.DefinesFinalAbi && !NativeAppArgumentHandoffPlanner.ExecutesApp && !NativeAppArgumentHandoffPlanner.DefinesFinalAbi && !NativeAppArgumentHandoffPlanner.AllocatesStack && !NativeAppArgumentHandoffPlanner.StartsHostedDotNet);
    }

    [HostTest("appmodel invocation args handoff rejects invocation failure", HostLane.Fast, 1)]
    private static bool TestRejectsInvocationFailure()
    {
        NativeAppInvocationPlanResult invocation = NativeAppInvocationPlanResult.Fail(NativeAppInvocationPlanStatus.InvalidStackSize, NativeAppExecutableMemoryPlanStatus.Success, NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, NativeAppLaunchStatus.Success);
        NativeAppArgumentVectorPlanResult vector = CreateVector("arg");
        NativeAppArgumentHandoffPlanResult result = NativeAppArgumentHandoffPlanner.Compose(invocation, vector);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == NativeAppArgumentHandoffPlanStatus.InvocationPlanFailed && result.InvocationStatus == NativeAppInvocationPlanStatus.InvalidStackSize && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success && result.ArgumentCount == 1);
    }

    [HostTest("appmodel invocation args handoff rejects argument-vector failure", HostLane.Fast, 2)]
    private static bool TestRejectsArgumentVectorFailure()
    {
        NativeAppArgumentVectorPlanResult vector = NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.UnterminatedQuote, NativeAppLaunchStatus.Success, errorOffset: 4);
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 12);
        NativeAppArgumentHandoffPlanResult result = NativeAppArgumentHandoffPlanner.Compose(invocation, vector);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed && result.InvocationStatus == NativeAppInvocationPlanStatus.Success && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.UnterminatedQuote);
    }

    [HostTest("appmodel invocation args handoff rejects parsed-byte mismatch", HostLane.Fast, 3)]
    private static bool TestRejectsParsedByteMismatch()
    {
        NativeAppArgumentVectorPlanResult vector = CreateVector("abcd");
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 3);
        NativeAppArgumentHandoffPlanResult result = NativeAppArgumentHandoffPlanner.Compose(invocation, vector);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == NativeAppArgumentHandoffPlanStatus.ParsedArgumentBytesMismatch && result.ArgumentCount == 1 && result.ParsedArgumentBytes == 4 && result.RawLaunchArgumentByteLength == 3);
    }

    [HostTest("appmodel invocation args handoff rejects default dependency results", HostLane.Fast, 4)]
    private static bool TestRejectsDefaultDependencyResults()
    {
        NativeAppArgumentVectorPlanResult readyVector = CreateVector("arg");
        NativeAppInvocationPlanResult readyInvocation = CreateInvocation(argumentByteLength: 3);
        NativeAppArgumentHandoffPlanResult defaultInvocation = NativeAppArgumentHandoffPlanner.Compose(default, readyVector);
        NativeAppArgumentHandoffPlanResult defaultVector = NativeAppArgumentHandoffPlanner.Compose(readyInvocation, default);
        return HostCheck.Result(!defaultInvocation.Succeeded && defaultInvocation.IsInitialized && defaultInvocation.Status == NativeAppArgumentHandoffPlanStatus.InvocationPlanNotInitialized && !defaultVector.Succeeded && defaultVector.IsInitialized && defaultVector.Status == NativeAppArgumentHandoffPlanStatus.ArgumentVectorNotInitialized);
    }

    [HostTest("appmodel invocation args handoff rejects impossible counts", HostLane.Fast, 5)]
    private static bool TestRejectsImpossibleCounts()
    {
        NativeAppArgumentVector vector = default;
        if (!vector.TryAdd(default))
        {
            return HostCheck.Result(false);
        }

        NativeAppArgumentVectorPlanResult argumentVector = NativeAppArgumentVectorPlanResult.Success(vector);
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 1);
        NativeAppArgumentHandoffPlanResult result = NativeAppArgumentHandoffPlanner.Compose(invocation, argumentVector);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == NativeAppArgumentHandoffPlanStatus.ImpossibleArgumentCount && result.ArgumentCount == 1 && result.ParsedArgumentBytes == 0);
    }

    [HostTest("appmodel invocation args handoff default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppArgumentHandoffPlanResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == NativeAppArgumentHandoffPlanStatus.Success && result.InvocationStatus == NativeAppInvocationPlanStatus.Success && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success && result.ArgumentCount == 0 && result.ParsedArgumentBytes == 0 && result.RawLaunchArgumentByteLength == 0 && !result.ExecutesApp && !result.DefinesFinalAbi);
    }

    [HostTest("appmodel invocation args handoff layout contains no managed references", HostLane.Fast, 7)]
    private static bool TestLayoutContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppArgumentHandoffPlanResult>());
    }

    [HostTest("boot.sequence app args handoff ready", HostLane.Fast, 8)]
    private static bool TestBootSequenceAppArgsHandoffReady()
    {
        NativeAppArgumentHandoffPlanResult handoff = CreateHandoff(Arguments);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(handoff, serial, events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffReadyMarker(handoff);
        return HostCheck.Result(handoff.Succeeded && result.Succeeded && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "APP_ARGS_HANDOFF_READY status=Ready initialized=true handoff=Success invocation=Success argumentVector=Success argumentCount=3 argumentBytes=22 parsedArgumentBytes=22 rawArgumentBytes=24 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false" && expectedMarker.Contains("stackBytesAvailable=false", StringComparison.Ordinal) && expectedMarker.Contains("executesApp=false", StringComparison.Ordinal) && expectedMarker.Contains("definesFinalAbi=false", StringComparison.Ordinal) && expectedMarker.Contains("startsKestrel=false", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence app args handoff rejects failed result", HostLane.Fast, 9)]
    private static bool TestBootSequenceAppArgsHandoffRejectsFailedResult()
    {
        NativeAppArgumentHandoffPlanResult handoff = NativeAppArgumentHandoffPlanResult.Fail(NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed, NativeAppInvocationPlanStatus.Success, NativeAppArgumentVectorStatus.UnterminatedQuote, argumentCount: 0, parsedArgumentBytes: 0, rawLaunchArgumentByteLength: 12);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(handoff, serial, events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffNotReadyMarker(NativeAppArgumentHandoffReadinessStatus.HandoffFailed, handoff);
        return HostCheck.Result(handoff.IsInitialized && !handoff.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(NativeAppArgumentHandoffReadinessStatus.HandoffFailed), StringComparison.Ordinal) && result.Reason.Contains(nameof(NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed), StringComparison.Ordinal) && result.Reason.Contains(nameof(NativeAppArgumentVectorStatus.UnterminatedQuote), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "APP_ARGS_HANDOFF_NOT_READY status=HandoffFailed initialized=true handoff=ArgumentVectorFailed invocation=Success argumentVector=UnterminatedQuote argumentCount=0 argumentBytes=0 parsedArgumentBytes=0 rawArgumentBytes=12 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false");
    }

    [HostTest("boot.sequence app args handoff rejects default result", HostLane.Fast, 10)]
    private static bool TestBootSequenceAppArgsHandoffRejectsDefaultResult()
    {
        NativeAppArgumentHandoffPlanResult handoff = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(handoff, serial, events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffNotReadyMarker(NativeAppArgumentHandoffReadinessStatus.NotInitialized, handoff);
        return HostCheck.Result(!handoff.IsInitialized && !handoff.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(NativeAppArgumentHandoffReadinessStatus.NotInitialized), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "APP_ARGS_HANDOFF_NOT_READY status=NotInitialized initialized=false handoff=Success invocation=Success argumentVector=Success argumentCount=0 argumentBytes=0 parsedArgumentBytes=0 rawArgumentBytes=0 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false");
    }

    [HostTest("boot.sequence app args handoff preserves default markers", HostLane.Fast, 11)]
    private static bool TestBootSequenceAppArgsHandoffPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);
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

        return HostCheck.Result(!serial.ContainsPrefix(BootSequence.NativeAppArgumentHandoffReadyPrefix) && !serial.ContainsPrefix(BootSequence.NativeAppArgumentHandoffNotReadyPrefix));
    }

    private static NativeAppArgumentVectorPlanResult CreateVector(string arguments)
    {
        byte[] source = ValidManifest(arguments);
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        return NativeAppArgumentVectorPlanner.Create(source, launch);
    }

    private static NativeAppInvocationPlanResult CreateInvocation(NativeAppLaunchPlanResult launch)
    {
        return NativeAppInvocationPlanner.Create(CreateExecutableMemoryPlan(), launch);
    }

    private static NativeAppArgumentHandoffPlanResult CreateHandoff(string arguments)
    {
        byte[] source = ValidManifest(arguments);
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        NativeAppArgumentVectorPlanResult vector = NativeAppArgumentVectorPlanner.Create(source, launch);
        NativeAppInvocationPlanResult invocation = CreateInvocation(launch);
        return NativeAppArgumentHandoffPlanner.Compose(invocation, vector);
    }

    private static NativeAppInvocationPlanResult CreateInvocation(int argumentByteLength)
    {
        return NativeAppInvocationPlanResult.Success(NativeAppExecutableMemoryPlanStatus.Success, NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, NativeAppLaunchStatus.Success, CreateInvocationPlan(argumentByteLength));
    }

    private static NativeAppInvocationPlan CreateInvocationPlan(int argumentByteLength)
    {
        return new NativeAppInvocationPlan(NativeAppInvocationPlanner.DefaultStackBytes, requiredWritableStagingBytes: 4096, executableMapOffsetInPackage: 4096, executableMapByteLength: 4096, executableMapEndOffsetInPackage: 8192, executableContentOffsetInMapping: 32, executableBytes: 64, entryOffsetInExecutableContent: 0, entryOffsetInExecutableMapping: 32, entryOffsetInPackage: 4128, manifestMetadataByteCount: AppName.Length + AppPath.Length + argumentByteLength, manifestVersion: NativeManifest.Version0, appKind: NativeManifestAppKind.Package, runtimeKind: NativeManifestRuntimeKind.NativeAot, networkMode: NativeManifestNetworkMode.None, logMode: NativeManifestLogMode.Serial, argumentByteLength: argumentByteLength, environmentCount: 0, requiredBootCapabilities: BootCapabilities.TextOutput, requiredRuntimeCapabilities: NativeAppLaunchRuntimeCapabilities.PackageStorage);
    }

    private static NativeAppExecutableMemoryPlanResult CreateExecutableMemoryPlan()
    {
        return NativeAppExecutableMemoryPlan.Create(new NativeAppPayloadMappingPlan(payloadOffsetInPackage: 4096, payloadLength: 96, headerLength: 32, executableOffsetInPayload: 32, executableOffsetInPackage: 4128, executableLength: 64, entryOffsetInPayload: 32, entryOffsetInExecutable: 0, entryOffsetInPackage: 4128));
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan(byte[] manifestSource)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeAppLaunchPlan.Create(manifestSource, parsed.Manifest, BootInfo.CreateHost(BootCapabilities.TextOutput), NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static byte[] ValidManifest(string arguments)
    {
        string source = "format=os-native-manifest\n" + "version=0\n" + "app.name=" + AppName + "\n" + "app.path=" + AppPath + "\n" + "args=" + arguments + "\n" + "app.kind=package\n" + "runtime.kind=native-aot\n" + "network=dhcp\n" + "log=serial\n";
        return Encoding.ASCII.GetBytes(source);
    }
}
