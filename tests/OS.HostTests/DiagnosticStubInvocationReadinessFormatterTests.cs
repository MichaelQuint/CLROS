using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class DiagnosticStubInvocationReadinessFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.stub_invocation formatter formats ready marker", TestFormatsReadyMarker, ref failures);
        Run("diagnostics.stub_invocation formatter formats not-ready marker", TestFormatsNotReadyMarker, ref failures);
        Run("diagnostics.stub_invocation formatter reports short destination", TestReportsShortDestination, ref failures);
        Run("diagnostics.stub_invocation formatter rejects invalid status", TestRejectsInvalidStatus, ref failures);
        Run("diagnostics.stub_invocation formatter uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);
        Run("diagnostics.stub_invocation formatter default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Diagnostic stub invocation readiness formatter tests failed: " + failures);
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

    private static bool TestFormatsReadyMarker()
    {
        Span<byte> destination = stackalloc byte[DiagnosticStubInvocationReadinessFormatter.MaximumMarkerBytes];

        DiagnosticStubInvocationMarkerFormatResult result =
            DiagnosticStubInvocationReadinessFormatter.TryFormatReadyMarker(destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticStubInvocationMarkerFormatStatus.Success
            && result.BytesWritten == BootSequence.DiagnosticStubInvocationReady.Length
            && result.RequiredBytes == BootSequence.DiagnosticStubInvocationReady.Length
            && marker == BootSequence.DiagnosticStubInvocationReady;
    }

    private static bool TestFormatsNotReadyMarker()
    {
        DiagnosticStubInvocationReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[DiagnosticStubInvocationReadinessFormatter.MaximumMarkerBytes];

        DiagnosticStubInvocationMarkerFormatResult result =
            DiagnosticStubInvocationReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = BootSequence.CreateDiagnosticStubInvocationNotReadyMarker(readiness);

        return !readiness.IsInitialized
            && !readiness.Succeeded
            && result.Succeeded
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(BootSequence.DiagnosticStubInvocationNotReadyPrefix, StringComparison.Ordinal)
            && marker.Contains("initialized=false", StringComparison.Ordinal)
            && marker.EndsWith("in8=0", StringComparison.Ordinal);
    }

    private static bool TestReportsShortDestination()
    {
        DiagnosticStubInvocationReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xA5);

        DiagnosticStubInvocationMarkerFormatResult query =
            DiagnosticStubInvocationReadinessFormatter.QueryNotReadyMarkerRequiredBytes(readiness);
        DiagnosticStubInvocationMarkerFormatResult result =
            DiagnosticStubInvocationReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return query.IsInitialized
            && query.Status == DiagnosticStubInvocationMarkerFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && destination[0] == 0xA5
            && destination[^1] == 0xA5;
    }

    private static bool TestRejectsInvalidStatus()
    {
        ExecutableStubPageTransitionPlanResult pageTransition =
            ExecutableStubPageTransitionPlanResult.Fail(
                (ExecutableStubPageTransitionPlanStatus)999,
                ExecutableStubMemoryPlanStatus.Success,
                ExecutableStubBundleMaterializerStatus.Success);
        Com1PortIoBackendContractResult backendContract =
            Com1PortIoBackendContractResult.Success(
                SerialPortConfiguration.Com1BasePort,
                out8StubOffset: 0,
                in8StubOffset: 16,
                out8StubAddress: 0x1000,
                in8StubAddress: 0x1010);
        DiagnosticStubInvocationReadinessResult readiness =
            DiagnosticStubInvocationReadinessResult.Fail(
                DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady,
                pageTransition,
                backendContract);
        Span<byte> destination = stackalloc byte[128];
        destination.Fill(0xCC);

        DiagnosticStubInvocationMarkerFormatResult result =
            DiagnosticStubInvocationReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubInvocationMarkerFormatStatus.InvalidStatus
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && destination[0] == 0xCC
            && destination[^1] == 0xCC;
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Diagnostics",
                "DiagnosticStubInvocationReadinessFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.PayloadBaseAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.PayloadEndAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.BytesMaterialized)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.Out8StubAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.In8StubAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, source)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("private static int CountUnsignedDigits(", StringComparison.Ordinal)
            && !source.Contains("private static void WriteUnsigned(", StringComparison.Ordinal)
            && !source.Contains("private static bool WriteUnsigned(", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(", StringComparison.Ordinal)
            && !source.Contains("destination.Slice(offset)", StringComparison.Ordinal)
            && !source.Contains("ReadyMarker.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains("source.CopyTo(destination", StringComparison.Ordinal)
            && !source.Contains("AsciiDecimalFormatter.FormatUInt64(destination.Slice(offset), value)", StringComparison.Ordinal)
            && !source.Contains("destination[cursor] = (byte)('0' +", StringComparison.Ordinal);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DiagnosticStubInvocationMarkerFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubInvocationMarkerFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DiagnosticStubInvocationMarkerFormatResult>();
    }
}
