using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class DiagnosticStubCallableBindingPlanDiagnosticFormatterTests
{
    private const nuint RegionBaseAddress = 0x0010_0000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.stub_callable formatter formats success marker", TestFormatsSuccessMarker, ref failures);
        Run("diagnostics.stub_callable formatter formats failure marker", TestFormatsFailureMarker, ref failures);
        Run("diagnostics.stub_callable formatter reports short destination", TestReportsShortDestination, ref failures);
        Run("diagnostics.stub_callable formatter rejects default result", TestRejectsDefaultResult, ref failures);
        Run("diagnostics.stub_callable formatter rejects invalid status", TestRejectsInvalidStatus, ref failures);
        Run("diagnostics.stub_callable formatter uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);
        Run("diagnostics.stub_callable formatter result contains no managed references", TestResultContainsNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Diagnostic stub callable binding plan diagnostic formatter tests failed: " + failures);
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

    private static bool TestFormatsSuccessMarker()
    {
        DiagnosticStubCallableBindingPlanResult plan = CreateReadyPlan();
        Span<byte> destination = stackalloc byte[512];

        DiagnosticStubCallableBindingPlanDiagnosticFormatResult query =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.QueryRequiredBytes(plan);
        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.FormatResult(plan, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = CreateExpectedMarker(plan);

        return plan.Succeeded
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success
            && query.RequiredBytes == expected.Length
            && result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && result.AvailableBytes == destination.Length
            && marker == expected
            && marker.StartsWith("DIAGNOSTIC_STUB_CALLABLE_BINDING status=Ready ", StringComparison.Ordinal)
            && marker.Contains("createsDelegate=false", StringComparison.Ordinal)
            && marker.EndsWith("writesSerial=false", StringComparison.Ordinal);
    }

    private static bool TestFormatsFailureMarker()
    {
        DiagnosticStubCallableBindingPlanResult plan = CreateFailedPlan();
        Span<byte> destination = stackalloc byte[512];

        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.FormatResult(plan, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = CreateExpectedMarker(plan);

        return !plan.Succeeded
            && plan.IsInitialized
            && result.Succeeded
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(
                "DIAGNOSTIC_STUB_CALLABLE_BINDING status=InvocationReadinessNotReady ",
                StringComparison.Ordinal)
            && marker.Contains("initialized=true", StringComparison.Ordinal)
            && marker.Contains("invocation=Ready", StringComparison.Ordinal)
            && marker.Contains("out8Bytes=7 in8Bytes=5", StringComparison.Ordinal);
    }

    private static bool TestReportsShortDestination()
    {
        DiagnosticStubCallableBindingPlanResult plan = CreateReadyPlan();
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xA5);

        DiagnosticStubCallableBindingPlanDiagnosticFormatResult query =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.QueryRequiredBytes(plan);
        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.FormatResult(plan, destination);

        return query.IsInitialized
            && query.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && result.AvailableBytes == destination.Length
            && destination[0] == 0xA5
            && destination[^1] == 0xA5;
    }

    private static bool TestRejectsDefaultResult()
    {
        DiagnosticStubCallableBindingPlanResult plan = default;
        Span<byte> destination = stackalloc byte[128];
        destination.Fill(0xCC);

        DiagnosticStubCallableBindingPlanDiagnosticFormatResult query =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.QueryRequiredBytes(plan);
        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.FormatResult(plan, destination);

        return !plan.IsInitialized
            && !plan.Succeeded
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.InvalidResult
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.AvailableBytes == destination.Length
            && destination[0] == 0xCC
            && destination[^1] == 0xCC;
    }

    private static bool TestRejectsInvalidStatus()
    {
        DiagnosticStubCallableBindingPlanResult plan =
            DiagnosticStubCallableBindingPlanResult.Create(
                (DiagnosticStubCallableBindingPlanStatus)999,
                CreateReadyInvocationReadiness(),
                CreateReadyBackendContract(CreateReadyPageTransition()),
                ExecutableStubBundleMaterializerResult.Success());
        Span<byte> destination = stackalloc byte[256];
        destination.Fill(0xDD);

        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.FormatResult(plan, destination);
        DiagnosticStubCallableBindingPlanDiagnosticFormatResult query =
            DiagnosticStubCallableBindingPlanDiagnosticFormatter.QueryRequiredBytes(plan);

        return plan.IsInitialized
            && !plan.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.InvalidResult
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && query.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && destination[0] == 0xDD
            && destination[^1] == 0xDD;
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
                "DiagnosticStubCallableBindingPlanDiagnosticFormatter.cs"));

        return DiagnosticStubCallableBindingPlanDiagnosticFormatter.UsesSharedAsciiTextBuilder
            && DiagnosticStubCallableBindingPlanDiagnosticFormatter.UsesCallerProvidedDestination
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.AllocatesManagedStorage
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.CreatesManagedDelegate
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.InvokesCallable
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.PerformsPortIo
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.WritesSerial
            && !DiagnosticStubCallableBindingPlanDiagnosticFormatter.StartsKestrel
            && source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.PayloadBaseAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.PayloadEndAddress)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.BytesMaterialized)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt64Digits(plan.Out8BytesCopied)", StringComparison.Ordinal)
            && !source.Contains("private static int CountUnsignedDigits(", StringComparison.Ordinal)
            && !source.Contains("private static void WriteUnsigned(", StringComparison.Ordinal)
            && !source.Contains("private static bool WriteUnsigned(", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(", StringComparison.Ordinal)
            && !source.Contains("destination.Slice(offset)", StringComparison.Ordinal)
            && !source.Contains("destination[cursor] = (byte)('0' +", StringComparison.Ordinal);
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        DiagnosticStubCallableBindingPlanDiagnosticFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.AvailableBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DiagnosticStubCallableBindingPlanDiagnosticFormatResult>();
    }

    private static string CreateExpectedMarker(DiagnosticStubCallableBindingPlanResult plan)
    {
        return "DIAGNOSTIC_STUB_CALLABLE_BINDING status=" + plan.Status
            + " initialized=" + (plan.IsInitialized ? "true" : "false")
            + " invocation=" + plan.InvocationStatus
            + " pageTransition=" + plan.PageTransitionStatus
            + " backend=" + plan.BackendContractStatus
            + " materializer=" + plan.MaterializerStatus
            + " basePort=" + plan.ConfiguredBasePort
            + " payloadBase=" + plan.PayloadBaseAddress
            + " payloadEnd=" + plan.PayloadEndAddress
            + " payloadBytes=" + plan.PayloadByteLength
            + " bytes=" + plan.BytesMaterialized
            + " out8Offset=" + plan.Out8StubOffset
            + " in8Offset=" + plan.In8StubOffset
            + " out8=" + plan.Out8StubAddress
            + " in8=" + plan.In8StubAddress
            + " out8Bytes=" + plan.Out8BytesCopied
            + " in8Bytes=" + plan.In8BytesCopied
            + " createsDelegate=" + (plan.CreatesManagedDelegate ? "true" : "false")
            + " invokesCallable=" + (plan.InvokesCallable ? "true" : "false")
            + " performsPortIo=" + (plan.PerformsPortIo ? "true" : "false")
            + " writesSerial=" + (plan.WritesSerial ? "true" : "false");
    }

    private static DiagnosticStubCallableBindingPlanResult CreateReadyPlan()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return DiagnosticStubCallableBindingPlan.Create(
            invocationReadiness,
            backendContract,
            ExecutableStubBundleMaterializerResult.Success());
    }

    private static DiagnosticStubCallableBindingPlanResult CreateFailedPlan()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);

        return DiagnosticStubCallableBindingPlan.Create(
            default,
            backendContract,
            ExecutableStubBundleMaterializerResult.Success());
    }

    private static DiagnosticStubInvocationReadinessResult CreateReadyInvocationReadiness()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);

        return DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);
    }

    private static ExecutableStubPageTransitionPlanResult CreateReadyPageTransition()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        return ExecutableStubPageTransitionPlan.Create(
            memoryPlan,
            ExecutableStubBundleMaterializerResult.Success(),
            RegionBaseAddress,
            memoryPlan.RequiredByteLength);
    }

    private static Com1PortIoBackendContractResult CreateReadyBackendContract(
        ExecutableStubPageTransitionPlanResult pageTransition)
    {
        Com1PortIoBackendPlanResult backendReadiness =
            Com1PortIoBackendPlan.CreateReady().Validate();
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength)
        };
        ExecutableStubBundlePlanResult bundlePlan = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(64));

        return Com1PortIoBackendContract.Resolve(
            backendReadiness,
            bundlePlan,
            pageTransition.PayloadBaseAddress);
    }
}
