using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;

internal static class NativeAppArgumentVectorPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel args vector splits manifest args", TestSplitsManifestArgs, ref failures);
        Run("appmodel args vector preserves quoted spaces", TestPreservesQuotedSpaces, ref failures);
        Run("appmodel args vector accepts empty args", TestAcceptsEmptyArgs, ref failures);
        Run("appmodel args vector rejects too many args", TestRejectsTooManyArgs, ref failures);
        Run("appmodel args vector rejects malformed quotes", TestRejectsMalformedQuotes, ref failures);
        Run("appmodel args vector rejects failed launch plan", TestRejectsFailedLaunchPlan, ref failures);
        Run("appmodel args vector default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel args vector formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("appmodel args vector formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("appmodel args vector formatter rejects default result", TestFormatterRejectsDefaultResult, ref failures);
        Run("appmodel args vector formatter uses shared ascii text builder", TestFormatterUsesSharedAsciiTextBuilder, ref failures);
        Run("boot.sequence app args vector ready", TestBootSequenceAppArgsVectorReady, ref failures);
        Run(
            "boot.sequence app args vector rejects failed result",
            TestBootSequenceAppArgsVectorRejectsFailedResult,
            ref failures);
        Run(
            "boot.sequence app args vector rejects default result",
            TestBootSequenceAppArgsVectorRejectsDefaultResult,
            ref failures);
        Run(
            "boot.sequence app args vector preserves default markers",
            TestBootSequenceAppArgsVectorPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app argument vector host tests failed: " + failures);
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

    private static bool TestSplitsManifestArgs()
    {
        byte[] source = ManifestBytes("--port=8080 --mode cloud");
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentVectorStatus.Success
            && result.ArgumentCount == 3
            && ArgumentEquals(source, result.Arguments.GetAt(0), "--port=8080")
            && ArgumentEquals(source, result.Arguments.GetAt(1), "--mode")
            && ArgumentEquals(source, result.Arguments.GetAt(2), "cloud");
    }

    private static bool TestPreservesQuotedSpaces()
    {
        byte[] source = ManifestBytes("--name \"hello cloud\" \"\"");
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return result.Succeeded
            && result.ArgumentCount == 3
            && ArgumentEquals(source, result.Arguments.GetAt(0), "--name")
            && ArgumentEquals(source, result.Arguments.GetAt(1), "hello cloud")
            && ArgumentEquals(source, result.Arguments.GetAt(2), string.Empty);
    }

    private static bool TestAcceptsEmptyArgs()
    {
        byte[] source = ManifestBytes(string.Empty);
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return result.Succeeded
            && result.ArgumentCount == 0;
    }

    private static bool TestRejectsTooManyArgs()
    {
        StringBuilder builder = new StringBuilder();
        for (int index = 0; index <= NativeAppArgumentVectorPlanner.MaximumArgumentCount; index++)
        {
            if (index != 0)
            {
                builder.Append(' ');
            }

            builder.Append('a').Append(index);
        }

        byte[] source = ManifestBytes(builder.ToString());
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentVectorStatus.TooManyArguments
            && result.ArgumentCount == 0;
    }

    private static bool TestRejectsMalformedQuotes()
    {
        return Rejects("\"unterminated", NativeAppArgumentVectorStatus.UnterminatedQuote)
            && Rejects("\"quoted\"suffix", NativeAppArgumentVectorStatus.QuoteNotDelimited)
            && Rejects("prefix\"suffix", NativeAppArgumentVectorStatus.QuoteNotDelimited);
    }

    private static bool TestRejectsFailedLaunchPlan()
    {
        byte[] source = ManifestBytes("--port=8080");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult launch = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppArgumentVectorStatus.LaunchPlanFailed
            && result.LaunchStatus == NativeAppLaunchStatus.MissingBootCapabilities;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppArgumentVectorPlanResult result = default;
        NativeAppArgument argument = default;

        return !result.Succeeded
            && !result.IsInitialized
            && result.Status == NativeAppArgumentVectorStatus.Success
            && result.ArgumentCount == 0
            && !argument.IsPresent;
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        byte[] source = ManifestBytes("--port=8080 --mode cloud");
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);
        NativeAppArgument first = result.Arguments.GetAt(0);
        NativeAppArgumentVectorDiagnosticFormatResult query =
            NativeAppArgumentVectorDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes];
        NativeAppArgumentVectorDiagnosticFormatResult format =
            NativeAppArgumentVectorDiagnosticFormatter.FormatResult(result, destination);

        string expected =
            "APP_ARGS_VECTOR status=Success initialized=true argumentCount=3 maxArgumentCount=16 maxArgumentLength=128 firstArgumentPresent=true firstArgumentOffset=" +
            first.Value.Offset +
            " firstArgumentLength=" +
            first.Value.Length +
            " appExecuted=false kestrelStarted=false";

        return result.Succeeded
            && first.IsPresent
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppArgumentVectorDiagnosticFormatStatus.Success
            && query.PlannerStatus == NativeAppArgumentVectorStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.Status == NativeAppArgumentVectorDiagnosticFormatStatus.Success
            && format.PlannerStatus == NativeAppArgumentVectorStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && Encoding.ASCII.GetString(destination) == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                NativeAppArgumentVectorDiagnosticFormatResult>();
    }

    private static bool TestFormatterReportsShortDestination()
    {
        byte[] source = ManifestBytes("\"hello cloud\"");
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);
        NativeAppArgumentVectorDiagnosticFormatResult query =
            NativeAppArgumentVectorDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        NativeAppArgumentVectorDiagnosticFormatResult format =
            NativeAppArgumentVectorDiagnosticFormatter.FormatResult(result, destination);

        return result.Succeeded
            && query.IsInitialized
            && query.Status == NativeAppArgumentVectorDiagnosticFormatStatus.Success
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppArgumentVectorDiagnosticFormatStatus.DestinationTooSmall
            && format.PlannerStatus == NativeAppArgumentVectorStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC);
    }

    private static bool TestFormatterRejectsDefaultResult()
    {
        NativeAppArgumentVectorPlanResult defaultResult = default;
        byte[] destination = new byte[128];
        Array.Fill(destination, (byte)0xA5);

        NativeAppArgumentVectorDiagnosticFormatResult format =
            NativeAppArgumentVectorDiagnosticFormatter.FormatResult(defaultResult, destination);
        NativeAppArgumentVectorDiagnosticFormatResult query =
            NativeAppArgumentVectorDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        NativeAppArgumentVectorDiagnosticFormatResult defaultFormat = default;

        return format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppArgumentVectorDiagnosticFormatStatus.InvalidResult
            && format.PlannerStatus == NativeAppArgumentVectorStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppArgumentVectorDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && AllBytesEqual(destination, 0xA5);
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Manifest",
                "NativeAppArgumentVectorDiagnosticFormatter.cs"));

        return source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.ArgumentCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal);
    }

    private static bool TestBootSequenceAppArgsVectorReady()
    {
        byte[] source = ManifestBytes("--port=8080 --mode cloud");
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult argumentVector =
            NativeAppArgumentVectorPlanner.Create(source, launch);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentVectorReadiness(
            argumentVector,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentVectorReadyMarker(argumentVector);

        return argumentVector.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_VECTOR_READY status=Ready initialized=true vector=Success launch=Success argumentCount=3 maxArgumentCount=16 maxArgumentLength=128 errorOffset=-1 executesApp=false startsKestrel=false"
            && expectedMarker.Contains("executesApp=false", StringComparison.Ordinal)
            && expectedMarker.Contains("startsKestrel=false", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppArgumentVectorPlanResult>();
    }

    private static bool TestBootSequenceAppArgsVectorRejectsFailedResult()
    {
        NativeAppArgumentVectorPlanResult argumentVector = NativeAppArgumentVectorPlanResult.Fail(
            NativeAppArgumentVectorStatus.ArgumentTooLong,
            NativeAppLaunchStatus.Success,
            42);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentVectorReadiness(
            argumentVector,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentVectorNotReadyMarker(
            NativeAppArgumentVectorReadinessStatus.ArgumentVectorFailed,
            argumentVector);

        return argumentVector.IsInitialized
            && !argumentVector.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppArgumentVectorReadinessStatus.ArgumentVectorFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppArgumentVectorStatus.ArgumentTooLong),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_VECTOR_NOT_READY status=ArgumentVectorFailed initialized=true vector=ArgumentTooLong launch=Success argumentCount=0 maxArgumentCount=16 maxArgumentLength=128 errorOffset=42 executesApp=false startsKestrel=false";
    }

    private static bool TestBootSequenceAppArgsVectorRejectsDefaultResult()
    {
        NativeAppArgumentVectorPlanResult argumentVector = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppArgumentVectorReadiness(
            argumentVector,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAppArgumentVectorNotReadyMarker(
            NativeAppArgumentVectorReadinessStatus.NotInitialized,
            argumentVector);

        return !argumentVector.IsInitialized
            && !argumentVector.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppArgumentVectorReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "APP_ARGS_VECTOR_NOT_READY status=NotInitialized initialized=false vector=Success launch=Success argumentCount=0 maxArgumentCount=16 maxArgumentLength=128 errorOffset=0 executesApp=false startsKestrel=false";
    }

    private static bool TestBootSequenceAppArgsVectorPreservesDefaultMarkers()
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

        return !serial.ContainsPrefix(BootSequence.NativeAppArgumentVectorReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.NativeAppArgumentVectorNotReadyPrefix);
    }

    private static bool Rejects(string args, NativeAppArgumentVectorStatus status)
    {
        byte[] source = ManifestBytes(args);
        if (!TryCreateLaunchPlan(source, out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppArgumentVectorPlanResult result =
            NativeAppArgumentVectorPlanner.Create(source, launch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == status
            && result.ErrorOffset >= 0;
    }

    private static bool TryCreateLaunchPlan(byte[] source, out NativeAppLaunchPlanResult launch)
    {
        launch = default;
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        launch = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        return launch.Succeeded;
    }

    private static bool TryParse(byte[] source, out NativeManifest manifest)
    {
        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        manifest = result.Manifest;
        return result.Succeeded;
    }

    private static bool ArgumentEquals(byte[] source, NativeAppArgument argument, string expected)
    {
        if (!argument.IsPresent || !argument.TryGetValue(source, out ReadOnlySpan<byte> value))
        {
            return false;
        }

        if (value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(byte[] bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] ManifestBytes(string args)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=" + args + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
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
