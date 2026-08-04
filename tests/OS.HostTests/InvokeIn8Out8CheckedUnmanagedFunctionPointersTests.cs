using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static unsafe class InvokeIn8Out8CheckedUnmanagedFunctionPointersTests
{
    private const string ReadyMarker = "CLROS_M02_003_READY";

    private static int _out8CallCount;
    private static ushort _lastOut8Port;
    private static byte _lastOut8Value;
    private static int _in8CallCount;
    private static ushort _lastIn8Port;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.003");

        int failures = 0;

        Run(
            "m02.003 binds and invokes checked unmanaged out8 and in8 targets",
            TestBindsAndInvokesCheckedUnmanagedTargets,
            ref failures);
        Run(
            "m02.003 rejects an unready binding plan before invocation",
            TestRejectsUnreadyBindingPlan,
            ref failures);
        Run(
            "m02.003 rejects an out8 target whose checked offset mismatches",
            TestRejectsMismatchedOut8Target,
            ref failures);
        Run(
            "m02.003 rejects a callable range outside the checked payload",
            TestRejectsCallableOutsidePayload,
            ref failures);
        Run(
            "m02.003 rejects overlapping checked callable ranges",
            TestRejectsOverlappingCallableRanges,
            ref failures);
        Run(
            "m02.003 rejects ports outside the configured com1 register window",
            TestRejectsPortsOutsideConfiguredRange,
            ref failures);
        Run(
            "m02.003 default results cannot invoke unmanaged targets",
            TestDefaultResultsCannotInvoke,
            ref failures);
        Run(
            "m02.003 results contain no managed references",
            TestResultsContainNoManagedReferences,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.003 host-tests failures=" + failures);
            throw new InvalidOperationException(
                "Checked unmanaged In8/Out8 function pointer tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS m02.003");
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
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static bool TestBindsAndInvokesCheckedUnmanagedTargets()
    {
        ResetCapturedCalls();

        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult bindingPlan = CreateReadyBindingPlan(
            (nuint)out8Target,
            (nuint)in8Target,
            out _,
            out _,
            out _);

        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(bindingPlan);

        ushort out8Port = SerialPortConfiguration.Com1BasePort;
        byte out8Value = 0xa5;
        ushort in8Port =
            (ushort)(SerialPortConfiguration.Com1BasePort + Com1SerialWriter.LineStatusRegisterOffset);
        byte expectedIn8Value = CreateIn8Value(in8Port);

        CheckedPortIoFunctionPointerInvocationResult out8Result =
            binding.InvokeOut8(out8Port, out8Value);
        CheckedPortIoFunctionPointerInvocationResult in8Result =
            binding.InvokeIn8(in8Port);

        return bindingPlan.Succeeded &&
            binding.Succeeded &&
            binding.IsInitialized &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.Ready &&
            binding.ConfiguredBasePort == SerialPortConfiguration.Com1BasePort &&
            binding.ConfiguredEndPort ==
                SerialPortConfiguration.Com1BasePort + SerialPortConfiguration.MaximumRegisterOffset &&
            binding.Out8StubAddress == (nuint)out8Target &&
            binding.In8StubAddress == (nuint)in8Target &&
            !binding.CreatesManagedDelegate &&
            out8Result.Succeeded &&
            out8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.Success &&
            out8Result.OperationKind == PortIoOperationKind.Out8 &&
            out8Result.Port == out8Port &&
            out8Result.Value == out8Value &&
            out8Result.WasInvoked &&
            in8Result.Succeeded &&
            in8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.Success &&
            in8Result.OperationKind == PortIoOperationKind.In8 &&
            in8Result.Port == in8Port &&
            in8Result.Value == expectedIn8Value &&
            in8Result.WasInvoked &&
            _out8CallCount == 1 &&
            _lastOut8Port == out8Port &&
            _lastOut8Value == out8Value &&
            _in8CallCount == 1 &&
            _lastIn8Port == in8Port;
    }

    private static bool TestRejectsUnreadyBindingPlan()
    {
        ResetCapturedCalls();

        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(
                default(DiagnosticStubCallableBindingPlanResult));
        CheckedPortIoFunctionPointerInvocationResult out8Result =
            binding.InvokeOut8(SerialPortConfiguration.Com1BasePort, 0x11);
        CheckedPortIoFunctionPointerInvocationResult in8Result =
            binding.InvokeIn8(SerialPortConfiguration.Com1BasePort);

        return binding.IsInitialized &&
            !binding.Succeeded &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady &&
            !out8Result.Succeeded &&
            out8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady &&
            !out8Result.WasInvoked &&
            !in8Result.Succeeded &&
            in8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady &&
            !in8Result.WasInvoked &&
            NoCallsCaptured();
    }

    private static bool TestRejectsMismatchedOut8Target()
    {
        ResetCapturedCalls();

        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult readyPlan = CreateReadyBindingPlan(
            (nuint)out8Target,
            (nuint)in8Target,
            out DiagnosticStubInvocationReadinessResult readiness,
            out Com1PortIoBackendContractResult backend,
            out ExecutableStubBundleMaterializerResult materializer);
        Com1PortIoBackendContractResult mismatchedBackend =
            Com1PortIoBackendContractResult.Success(
                backend.ConfiguredBasePort,
                backend.Out8StubOffset + 1,
                backend.In8StubOffset,
                backend.Out8StubAddress,
                backend.In8StubAddress);
        DiagnosticStubCallableBindingPlanResult mismatchedPlan =
            DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.Ready,
                readiness,
                mismatchedBackend,
                materializer);

        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(mismatchedPlan);

        return readyPlan.Succeeded &&
            mismatchedPlan.Succeeded &&
            !binding.Succeeded &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.Out8TargetMismatch &&
            NoCallsCaptured();
    }

    private static bool TestRejectsCallableOutsidePayload()
    {
        ResetCapturedCalls();

        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult readyPlan = CreateReadyBindingPlan(
            (nuint)out8Target,
            (nuint)in8Target,
            out DiagnosticStubInvocationReadinessResult readiness,
            out Com1PortIoBackendContractResult backend,
            out ExecutableStubBundleMaterializerResult materializer);
        nuint outsideOut8Address = readyPlan.PayloadEndAddress - 1;
        Com1PortIoBackendContractResult outsideBackend =
            Com1PortIoBackendContractResult.Success(
                backend.ConfiguredBasePort,
                outsideOut8Address - readyPlan.PayloadBaseAddress,
                backend.In8StubOffset,
                outsideOut8Address,
                backend.In8StubAddress);
        DiagnosticStubCallableBindingPlanResult outsidePlan =
            DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.Ready,
                readiness,
                outsideBackend,
                materializer);

        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(outsidePlan);

        return readyPlan.Succeeded &&
            outsidePlan.Succeeded &&
            !binding.Succeeded &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.Out8TargetOutsidePayload &&
            NoCallsCaptured();
    }

    private static bool TestRejectsOverlappingCallableRanges()
    {
        ResetCapturedCalls();

        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult readyPlan = CreateReadyBindingPlan(
            (nuint)out8Target,
            (nuint)in8Target,
            out DiagnosticStubInvocationReadinessResult readiness,
            out Com1PortIoBackendContractResult backend,
            out ExecutableStubBundleMaterializerResult materializer);
        nuint overlappingIn8Address = backend.Out8StubAddress + 1;
        Com1PortIoBackendContractResult overlappingBackend =
            Com1PortIoBackendContractResult.Success(
                backend.ConfiguredBasePort,
                backend.Out8StubOffset,
                overlappingIn8Address - readyPlan.PayloadBaseAddress,
                backend.Out8StubAddress,
                overlappingIn8Address);
        DiagnosticStubCallableBindingPlanResult overlappingPlan =
            DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.Ready,
                readiness,
                overlappingBackend,
                materializer);

        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(overlappingPlan);

        return readyPlan.Succeeded &&
            overlappingPlan.Succeeded &&
            !binding.Succeeded &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.CallableRangesOverlap &&
            NoCallsCaptured();
    }

    private static bool TestRejectsPortsOutsideConfiguredRange()
    {
        ResetCapturedCalls();

        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult bindingPlan = CreateReadyBindingPlan(
            (nuint)out8Target,
            (nuint)in8Target,
            out _,
            out _,
            out _);
        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(bindingPlan);

        ushort belowRange = (ushort)(SerialPortConfiguration.Com1BasePort - 1);
        ushort aboveRange =
            (ushort)(SerialPortConfiguration.Com1BasePort + SerialPortConfiguration.MaximumRegisterOffset + 1);
        CheckedPortIoFunctionPointerInvocationResult out8Result =
            binding.InvokeOut8(belowRange, 0x22);
        CheckedPortIoFunctionPointerInvocationResult in8Result =
            binding.InvokeIn8(aboveRange);

        return binding.Succeeded &&
            !out8Result.Succeeded &&
            out8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange &&
            !out8Result.WasInvoked &&
            !in8Result.Succeeded &&
            in8Result.Status == CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange &&
            !in8Result.WasInvoked &&
            NoCallsCaptured();
    }

    private static bool TestDefaultResultsCannotInvoke()
    {
        CheckedPortIoFunctionPointerBindingRequest request = default;
        CheckedPortIoFunctionPointerBindingResult binding = default;
        CheckedPortIoFunctionPointerInvocationResult invocation = default;
        CheckedPortIoFunctionPointerBindingResult requestBinding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request);
        CheckedPortIoFunctionPointerInvocationResult attemptedInvocation =
            binding.InvokeOut8(SerialPortConfiguration.Com1BasePort, 0x33);

        return !request.IsInitialized &&
            !request.SourcePlanSucceeded &&
            requestBinding.IsInitialized &&
            !requestBinding.Succeeded &&
            requestBinding.Status == CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady &&
            !binding.IsInitialized &&
            !binding.Succeeded &&
            binding.Status == CheckedPortIoFunctionPointerBindingStatus.Ready &&
            !invocation.IsInitialized &&
            !invocation.Succeeded &&
            invocation.Status == CheckedPortIoFunctionPointerInvocationStatus.Success &&
            !invocation.WasInvoked &&
            attemptedInvocation.IsInitialized &&
            !attemptedInvocation.Succeeded &&
            attemptedInvocation.Status == CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady &&
            !attemptedInvocation.WasInvoked;
    }

    private static bool TestResultsContainNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerBindingRequest>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerBindingResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerInvocationResult>();
    }

    private static DiagnosticStubCallableBindingPlanResult CreateReadyBindingPlan(
        nuint out8Address,
        nuint in8Address,
        out DiagnosticStubInvocationReadinessResult readiness,
        out Com1PortIoBackendContractResult backend,
        out ExecutableStubBundleMaterializerResult materializer)
    {
        nuint lowestTarget = out8Address < in8Address ? out8Address : in8Address;
        nuint regionCandidate = lowestTarget - ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint pageMask = (nuint)ExecutableStubMemoryPlan.DefaultPageSize - 1;
        nuint regionBaseAddress = regionCandidate & ~pageMask;
        nuint payloadBaseAddress =
            regionBaseAddress + ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint out8EndAddress = out8Address + PortIoStubPlan.Out8RequiredByteLength;
        nuint in8EndAddress = in8Address + PortIoStubPlan.In8RequiredByteLength;
        nuint highestTargetEnd = out8EndAddress > in8EndAddress ? out8EndAddress : in8EndAddress;

        materializer = ExecutableStubBundleMaterializerResult.Success();
        nuint payloadLength = highestTargetEnd - payloadBaseAddress;
        if (payloadLength < materializer.BytesCopied)
        {
            payloadLength = materializer.BytesCopied;
        }

        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create(payloadLength);
        ExecutableStubPageTransitionPlanResult pageTransition =
            ExecutableStubPageTransitionPlan.Create(
                memoryPlan,
                materializer,
                regionBaseAddress,
                memoryPlan.RequiredByteLength);
        backend = Com1PortIoBackendContractResult.Success(
            SerialPortConfiguration.Com1BasePort,
            out8Address - pageTransition.PayloadBaseAddress,
            in8Address - pageTransition.PayloadBaseAddress,
            out8Address,
            in8Address);
        readiness = DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backend);

        return DiagnosticStubCallableBindingPlan.Create(readiness, backend, materializer);
    }

    [UnmanagedCallersOnly]
    private static void CaptureOut8(ushort port, byte value)
    {
        _out8CallCount++;
        _lastOut8Port = port;
        _lastOut8Value = value;
    }

    [UnmanagedCallersOnly]
    private static byte CaptureIn8(ushort port)
    {
        _in8CallCount++;
        _lastIn8Port = port;
        return CreateIn8Value(port);
    }

    private static byte CreateIn8Value(ushort port)
    {
        return (byte)(port ^ 0x5a);
    }

    private static void ResetCapturedCalls()
    {
        _out8CallCount = 0;
        _lastOut8Port = 0;
        _lastOut8Value = 0;
        _in8CallCount = 0;
        _lastIn8Port = 0;
    }

    private static bool NoCallsCaptured()
    {
        return _out8CallCount == 0 &&
            _lastOut8Port == 0 &&
            _lastOut8Value == 0 &&
            _in8CallCount == 0 &&
            _lastIn8Port == 0;
    }
}
