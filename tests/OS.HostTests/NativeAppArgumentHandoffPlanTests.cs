using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class NativeAppArgumentHandoffPlanTests
{
    private const string AppName = "hello.app";
    private const string AppPath = "apps/hello.bin";
    private const string Arguments = "--port=8080 --mode cloud";

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel invocation args handoff composes vector", TestComposesVector, ref failures);
        Run("appmodel invocation args handoff rejects invocation failure", TestRejectsInvocationFailure, ref failures);
        Run(
            "appmodel invocation args handoff rejects argument-vector failure",
            TestRejectsArgumentVectorFailure,
            ref failures);
        Run(
            "appmodel invocation args handoff rejects parsed-byte mismatch",
            TestRejectsParsedByteMismatch,
            ref failures);
        Run(
            "appmodel invocation args handoff rejects default dependency results",
            TestRejectsDefaultDependencyResults,
            ref failures);
        Run(
            "appmodel invocation args handoff rejects impossible counts",
            TestRejectsImpossibleCounts,
            ref failures);
        Run(
            "appmodel invocation args handoff default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "appmodel invocation args handoff layout contains no managed references",
            TestLayoutContainsNoManagedReferences,
            ref failures);
        Run("boot.sequence app args handoff ready", TestBootSequenceAppArgsHandoffReady, ref failures);
        Run(
            "boot.sequence app args handoff rejects failed result",
            TestBootSequenceAppArgsHandoffRejectsFailedResult,
            ref failures);
        Run(
            "boot.sequence app args handoff rejects default result",
            TestBootSequenceAppArgsHandoffRejectsDefaultResult,
            ref failures);
        Run(
            "boot.sequence app args handoff preserves default markers",
            TestBootSequenceAppArgsHandoffPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app argument handoff plan host tests failed: " + failures);
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

    private static bool TestComposesVector()
    {
        byte[] source = ValidManifest(Arguments);
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        NativeAppArgumentVectorPlanResult vector = NativeAppArgumentVectorPlanner.Create(source, launch);
        NativeAppInvocationPlanResult invocation = CreateInvocation(launch);

        NativeAppArgumentHandoffPlanResult result =
            NativeAppArgumentHandoffPlanner.Compose(invocation, vector);

        int expectedParsedBytes = "--port=8080".Length + "--mode".Length + "cloud".Length;

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentHandoffPlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success
            && result.ArgumentCount == 3
            && result.ParsedArgumentBytes == expectedParsedBytes
            && result.RawLaunchArgumentByteLength == Arguments.Length
            && !result.ExecutesApp
            && !result.DefinesFinalAbi
            && !NativeAppArgumentHandoffPlanner.ExecutesApp
            && !NativeAppArgumentHandoffPlanner.DefinesFinalAbi
            && !NativeAppArgumentHandoffPlanner.AllocatesStack
            && !NativeAppArgumentHandoffPlanner.StartsHostedDotNet;
    }

    private static bool TestRejectsInvocationFailure()
    {
        NativeAppInvocationPlanResult invocation = NativeAppInvocationPlanResult.Fail(
            NativeAppInvocationPlanStatus.InvalidStackSize,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success);
        NativeAppArgumentVectorPlanResult vector = CreateVector("arg");

        NativeAppArgumentHandoffPlanResult result =
            NativeAppArgumentHandoffPlanner.Compose(invocation, vector);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentHandoffPlanStatus.InvocationPlanFailed
            && result.InvocationStatus == NativeAppInvocationPlanStatus.InvalidStackSize
            && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success
            && result.ArgumentCount == 1;
    }

    private static bool TestRejectsArgumentVectorFailure()
    {
        NativeAppArgumentVectorPlanResult vector = NativeAppArgumentVectorPlanResult.Fail(
            NativeAppArgumentVectorStatus.UnterminatedQuote,
            NativeAppLaunchStatus.Success,
            errorOffset: 4);
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 12);

        NativeAppArgumentHandoffPlanResult result =
            NativeAppArgumentHandoffPlanner.Compose(invocation, vector);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.UnterminatedQuote;
    }

    private static bool TestRejectsParsedByteMismatch()
    {
        NativeAppArgumentVectorPlanResult vector = CreateVector("abcd");
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 3);

        NativeAppArgumentHandoffPlanResult result =
            NativeAppArgumentHandoffPlanner.Compose(invocation, vector);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentHandoffPlanStatus.ParsedArgumentBytesMismatch
            && result.ArgumentCount == 1
            && result.ParsedArgumentBytes == 4
            && result.RawLaunchArgumentByteLength == 3;
    }

    private static bool TestRejectsDefaultDependencyResults()
    {
        NativeAppArgumentVectorPlanResult readyVector = CreateVector("arg");
        NativeAppInvocationPlanResult readyInvocation = CreateInvocation(argumentByteLength: 3);

        NativeAppArgumentHandoffPlanResult defaultInvocation =
            NativeAppArgumentHandoffPlanner.Compose(default, readyVector);
        NativeAppArgumentHandoffPlanResult defaultVector =
            NativeAppArgumentHandoffPlanner.Compose(readyInvocation, default);

        return !defaultInvocation.Succeeded
            && defaultInvocation.IsInitialized
            && defaultInvocation.Status == NativeAppArgumentHandoffPlanStatus.InvocationPlanNotInitialized
            && !defaultVector.Succeeded
            && defaultVector.IsInitialized
            && defaultVector.Status == NativeAppArgumentHandoffPlanStatus.ArgumentVectorNotInitialized;
    }

    private static bool TestRejectsImpossibleCounts()
    {
        NativeAppArgumentVector vector = default;
        if (!vector.TryAdd(default))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult argumentVector =
            NativeAppArgumentVectorPlanResult.Success(vector);
        NativeAppInvocationPlanResult invocation = CreateInvocation(argumentByteLength: 1);

        NativeAppArgumentHandoffPlanResult result =
            NativeAppArgumentHandoffPlanner.Compose(invocation, argumentVector);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentHandoffPlanStatus.ImpossibleArgumentCount
            && result.ArgumentCount == 1
            && result.ParsedArgumentBytes == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppArgumentHandoffPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppArgumentHandoffPlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.ArgumentVectorStatus == NativeAppArgumentVectorStatus.Success
            && result.ArgumentCount == 0
            && result.ParsedArgumentBytes == 0
            && result.RawLaunchArgumentByteLength == 0
            && !result.ExecutesApp
            && !result.DefinesFinalAbi;
    }

    private static bool TestLayoutContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppArgumentHandoffPlanResult>();
    }

    private static bool TestBootSequenceAppArgsHandoffReady()
    {
        NativeAppArgumentHandoffPlanResult handoff = CreateHandoff(Arguments);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(
            handoff,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffReadyMarker(handoff);

        return handoff.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_HANDOFF_READY status=Ready initialized=true handoff=Success invocation=Success argumentVector=Success argumentCount=3 argumentBytes=22 parsedArgumentBytes=22 rawArgumentBytes=24 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false"
            && expectedMarker.Contains("stackBytesAvailable=false", StringComparison.Ordinal)
            && expectedMarker.Contains("executesApp=false", StringComparison.Ordinal)
            && expectedMarker.Contains("definesFinalAbi=false", StringComparison.Ordinal)
            && expectedMarker.Contains("startsKestrel=false", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceAppArgsHandoffRejectsFailedResult()
    {
        NativeAppArgumentHandoffPlanResult handoff = NativeAppArgumentHandoffPlanResult.Fail(
            NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed,
            NativeAppInvocationPlanStatus.Success,
            NativeAppArgumentVectorStatus.UnterminatedQuote,
            argumentCount: 0,
            parsedArgumentBytes: 0,
            rawLaunchArgumentByteLength: 12);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(
            handoff,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffNotReadyMarker(
            NativeAppArgumentHandoffReadinessStatus.HandoffFailed,
            handoff);

        return handoff.IsInitialized
            && !handoff.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppArgumentHandoffReadinessStatus.HandoffFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppArgumentVectorStatus.UnterminatedQuote),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_HANDOFF_NOT_READY status=HandoffFailed initialized=true handoff=ArgumentVectorFailed invocation=Success argumentVector=UnterminatedQuote argumentCount=0 argumentBytes=0 parsedArgumentBytes=0 rawArgumentBytes=12 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false";
    }

    private static bool TestBootSequenceAppArgsHandoffRejectsDefaultResult()
    {
        NativeAppArgumentHandoffPlanResult handoff = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentHandoffReadiness(
            handoff,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentHandoffNotReadyMarker(
            NativeAppArgumentHandoffReadinessStatus.NotInitialized,
            handoff);

        return !handoff.IsInitialized
            && !handoff.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppArgumentHandoffReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_HANDOFF_NOT_READY status=NotInitialized initialized=false handoff=Success invocation=Success argumentVector=Success argumentCount=0 argumentBytes=0 parsedArgumentBytes=0 rawArgumentBytes=0 stackBytes=0 stackBytesAvailable=false executesApp=false definesFinalAbi=false startsHostedDotNet=false startsKestrel=false";
    }

    private static bool TestBootSequenceAppArgsHandoffPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

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

        return !serial.ContainsPrefix(BootSequence.NativeAppArgumentHandoffReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.NativeAppArgumentHandoffNotReadyPrefix);
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
        return NativeAppInvocationPlanResult.Success(
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success,
            CreateInvocationPlan(argumentByteLength));
    }

    private static NativeAppInvocationPlan CreateInvocationPlan(int argumentByteLength)
    {
        return new NativeAppInvocationPlan(
            NativeAppInvocationPlanner.DefaultStackBytes,
            requiredWritableStagingBytes: 4096,
            executableMapOffsetInPackage: 4096,
            executableMapByteLength: 4096,
            executableMapEndOffsetInPackage: 8192,
            executableContentOffsetInMapping: 32,
            executableBytes: 64,
            entryOffsetInExecutableContent: 0,
            entryOffsetInExecutableMapping: 32,
            entryOffsetInPackage: 4128,
            manifestMetadataByteCount: AppName.Length + AppPath.Length + argumentByteLength,
            manifestVersion: NativeManifest.Version0,
            appKind: NativeManifestAppKind.Package,
            runtimeKind: NativeManifestRuntimeKind.NativeAot,
            networkMode: NativeManifestNetworkMode.None,
            logMode: NativeManifestLogMode.Serial,
            argumentByteLength: argumentByteLength,
            environmentCount: 0,
            requiredBootCapabilities: BootCapabilities.TextOutput,
            requiredRuntimeCapabilities: NativeAppLaunchRuntimeCapabilities.PackageStorage);
    }

    private static NativeAppExecutableMemoryPlanResult CreateExecutableMemoryPlan()
    {
        return NativeAppExecutableMemoryPlan.Create(
            new NativeAppPayloadMappingPlan(
                payloadOffsetInPackage: 4096,
                payloadLength: 96,
                headerLength: 32,
                executableOffsetInPayload: 32,
                executableOffsetInPackage: 4128,
                executableLength: 64,
                entryOffsetInPayload: 32,
                entryOffsetInExecutable: 0,
                entryOffsetInPackage: 4128));
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan(byte[] manifestSource)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeAppLaunchPlan.Create(
            manifestSource,
            parsed.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static byte[] ValidManifest(string arguments)
    {
        string source =
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=" + AppName + "\n" +
            "app.path=" + AppPath + "\n" +
            "args=" + arguments + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n";

        return Encoding.ASCII.GetBytes(source);
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
