using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static unsafe class PanicSafeFormattingSerialReportingOwnedCOM1Tests
{
    private const string ExactReadyMarker =
        "CLROS_M02_006_READY panicFormatter=bounded ownedCom1Transport=true " +
        "callerScratch=true statusCoded=true firmwareFallback=false\r\n";

    private const string HostReadyMarker =
        "CLROS_M02_006_READY panicFormatting=hostValidated " +
        "ownedCom1Transport=hostCallbacks guestPortIo=false allocationFree=true";

    private static readonly ushort[] CapturedWritePorts = new ushort[4096];
    private static readonly byte[] CapturedWriteValues = new byte[4096];
    private static int _capturedWriteCount;
    private static int _capturedReadCount;
    private static int _notReadyReadCount;
    private static ushort _lastReadPort;

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.006");

        int failures = 0;
        Run("m02.006 formats an exact bounded panic record", TestFormatsExactRecord, ref failures);
        Run("m02.006 formats through the no-stdlib pointer core", TestFormatsThroughPointerCore, ref failures);
        Run("m02.006 reports the exact panic record through owned com1", TestReportsThroughOwnedCom1, ref failures);
        Run("m02.006 emits its readiness marker through owned com1", TestEmitsReadinessMarker, ref failures);
        Run("m02.006 readiness byte catalog is exact and bounded", TestReadinessByteCatalog, ref failures);
        Run("m02.006 polls until the owned transmitter is ready", TestPollsUntilReady, ref failures);
        Run("m02.006 fails closed on owned transmitter timeout", TestFailsClosedOnTimeout, ref failures);
        Run("m02.006 rejects invalid inputs before port io", TestRejectsInvalidInputsBeforeIo, ref failures);
        Run("m02.006 rejects small scratch without mutation or io", TestRejectsSmallScratchWithoutMutation, ref failures);
        Run("m02.006 hot path allocates no managed bytes", TestHotPathAllocatesNoManagedBytes, ref failures);
        Run("m02.006 defaults fail closed and source has no fallback", TestDefaultsAndSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.006 host-tests failures=" + failures);
            throw new InvalidOperationException(
                "Panic-safe owned COM1 formatting/reporting tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS m02.006");
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestFormatsExactRecord()
    {
        Span<byte> destination = stackalloc byte[1024];
        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();

        PanicSafeOwnedCom1FormatResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.FormatFaultSummary(
                destination,
                "idt"u8,
                "null-dereference-probe"u8,
                fault);

        string expected = ExpectedFaultSummary();
        string actual = Encoding.ASCII.GetString(destination.Slice(0, result.BytesWritten));

        return result.Succeeded &&
            result.Status == PanicSafeOwnedCom1FormatStatus.Success &&
            result.RequiredBytes == expected.Length &&
            result.BytesWritten == expected.Length &&
            actual == expected;
    }

    private static bool TestFormatsThroughPointerCore()
    {
        byte* destination = stackalloc byte[1024];
        byte* subsystem = stackalloc byte[3];
        subsystem[0] = (byte)'i';
        subsystem[1] = (byte)'d';
        subsystem[2] = (byte)'t';
        byte* reason = stackalloc byte[22];
        ReadOnlySpan<byte> reasonBytes = "null-dereference-probe"u8;
        for (int index = 0; index < reasonBytes.Length; index++)
        {
            reason[index] = reasonBytes[index];
        }

        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();
        PanicSafeOwnedCom1FormatResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.FormatFaultSummary(
                destination,
                1024,
                subsystem,
                3,
                reason,
                reasonBytes.Length,
                fault);

        string expected = ExpectedFaultSummary();
        if (!result.Succeeded ||
            result.Status != PanicSafeOwnedCom1FormatStatus.Success ||
            result.RequiredBytes != expected.Length ||
            result.BytesWritten != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (destination[index] != (byte)expected[index])
            {
                return false;
            }
        }

        PanicSafeOwnedCom1FormatResult invalidDestination =
            PanicSafeFormattingSerialReportingOwnedCom1.FormatFaultSummary(
                null,
                1024,
                subsystem,
                3,
                reason,
                reasonBytes.Length,
                fault);

        return !invalidDestination.Succeeded &&
            invalidDestination.Status ==
                PanicSafeOwnedCom1FormatStatus.InvalidDestination &&
            invalidDestination.RequiredBytes == expected.Length &&
            invalidDestination.BytesWritten == 0;
    }

    private static bool TestReportsThroughOwnedCom1()
    {
        ResetCapturedIo(notReadyReadCount: 0);
        Span<byte> scratch = stackalloc byte[1024];
        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();

        PanicSafeOwnedCom1ReportResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                CreateReadyBackend(),
                scratch,
                "idt"u8,
                "null-dereference-probe"u8,
                fault,
                transmitReadyPollLimit: 4);

        string expected = ExpectedFaultSummary();
        if (!result.Succeeded ||
            result.Status != PanicSafeOwnedCom1ReportStatus.Success ||
            !result.Format.Succeeded ||
            result.BytesFormatted != expected.Length ||
            result.BytesWritten != expected.Length ||
            result.LineStatusPolls != expected.Length ||
            result.FailedByteIndex != -1 ||
            !result.OwnedBackendUsed ||
            result.FirmwareFallbackUsed ||
            _capturedWriteCount !=
                Com1OwnedPortIoBackend.RequiredConfigurationWriteCount + expected.Length ||
            _capturedReadCount != expected.Length ||
            _lastReadPort != Com1OwnedPortIoBackend.LineStatusRegisterPort)
        {
            return false;
        }

        return PayloadMatches(expected);
    }

    private static bool TestEmitsReadinessMarker()
    {
        ResetCapturedIo(notReadyReadCount: 0);

        PanicSafeOwnedCom1ReportResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.EmitReadinessMarker(
                CreateReadyBackend(),
                transmitReadyPollLimit: 4);

        return result.Succeeded &&
            result.BytesFormatted == ExactReadyMarker.Length &&
            result.BytesWritten == ExactReadyMarker.Length &&
            result.LineStatusPolls == ExactReadyMarker.Length &&
            PanicSafeFormattingSerialReportingOwnedCom1.ReadinessSerialByteCount ==
                ExactReadyMarker.Length &&
            PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount ==
                ExactReadyMarker.Length - 2 &&
            _capturedWriteCount ==
                Com1OwnedPortIoBackend.RequiredConfigurationWriteCount +
                ExactReadyMarker.Length &&
            _capturedReadCount == ExactReadyMarker.Length &&
            PayloadMatches(ExactReadyMarker);
    }

    private static bool TestReadinessByteCatalog()
    {
        if (PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount !=
                ExactReadyMarker.Length - 2 ||
            PanicSafeFormattingSerialReportingOwnedCom1.ReadinessSerialByteCount !=
                ExactReadyMarker.Length)
        {
            return false;
        }

        for (int index = 0;
            index < PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount;
            index++)
        {
            if (!PanicSafeFormattingSerialReportingOwnedCom1.TryGetReadinessMarkerByte(
                    index,
                    out byte value) ||
                value != (byte)ExactReadyMarker[index])
            {
                return false;
            }
        }

        return !PanicSafeFormattingSerialReportingOwnedCom1.TryGetReadinessMarkerByte(
                -1,
                out byte beforeValue) &&
            beforeValue == 0 &&
            !PanicSafeFormattingSerialReportingOwnedCom1.TryGetReadinessMarkerByte(
                PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount,
                out byte afterValue) &&
            afterValue == 0;
    }

    private static bool TestPollsUntilReady()
    {
        ResetCapturedIo(notReadyReadCount: 3);

        PanicSafeOwnedCom1ReportResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.EmitReadinessMarker(
                CreateReadyBackend(),
                transmitReadyPollLimit: 5);

        return result.Succeeded &&
            result.LineStatusPolls == ExactReadyMarker.Length + 3 &&
            _capturedReadCount == result.LineStatusPolls &&
            _capturedWriteCount ==
                Com1OwnedPortIoBackend.RequiredConfigurationWriteCount +
                ExactReadyMarker.Length;
    }

    private static bool TestFailsClosedOnTimeout()
    {
        ResetCapturedIo(notReadyReadCount: int.MaxValue);

        PanicSafeOwnedCom1ReportResult result =
            PanicSafeFormattingSerialReportingOwnedCom1.EmitReadinessMarker(
                CreateReadyBackend(),
                transmitReadyPollLimit: 3);

        return !result.Succeeded &&
            result.IsInitialized &&
            result.Status == PanicSafeOwnedCom1ReportStatus.TransmitReadyTimeout &&
            result.Format.Succeeded &&
            result.Configuration.Succeeded &&
            result.BytesWritten == 0 &&
            result.LineStatusPolls == 3 &&
            result.FailedByteIndex == 0 &&
            result.OwnedBackendUsed &&
            !result.FirmwareFallbackUsed &&
            _capturedWriteCount == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount &&
            _capturedReadCount == 3;
    }

    private static bool TestRejectsInvalidInputsBeforeIo()
    {
        Span<byte> scratch = stackalloc byte[1024];
        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1ReportResult invalidLimit =
            PanicSafeFormattingSerialReportingOwnedCom1.EmitReadinessMarker(
                CreateReadyBackend(),
                transmitReadyPollLimit: 0);
        if (invalidLimit.Status !=
                PanicSafeOwnedCom1ReportStatus.InvalidTransmitReadyPollLimit ||
            !NoIoCaptured())
        {
            return false;
        }

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1ReportResult unready =
            PanicSafeFormattingSerialReportingOwnedCom1.EmitReadinessMarker(
                CreateUnreadyBackend());
        if (unready.Status != PanicSafeOwnedCom1ReportStatus.BackendNotReady ||
            !NoIoCaptured())
        {
            return false;
        }

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1ReportResult invalidSubsystem =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                CreateReadyBackend(),
                scratch,
                "bad subsystem"u8,
                "reason"u8,
                fault);
        if (invalidSubsystem.Status != PanicSafeOwnedCom1ReportStatus.InvalidSubsystem ||
            !NoIoCaptured())
        {
            return false;
        }

        ResetCapturedIo(notReadyReadCount: 0);
        Span<byte> invalidReasonBytes = stackalloc byte[] { (byte)'b', 0x80 };
        PanicSafeOwnedCom1ReportResult invalidReason =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                CreateReadyBackend(),
                scratch,
                "idt"u8,
                invalidReasonBytes,
                fault);
        if (invalidReason.Status != PanicSafeOwnedCom1ReportStatus.InvalidReason ||
            !NoIoCaptured())
        {
            return false;
        }

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1FaultRecord invalidVectorFault = CreateFaultRecord(vector: 256);
        PanicSafeOwnedCom1ReportResult invalidVector =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                CreateReadyBackend(),
                scratch,
                "idt"u8,
                "reason"u8,
                invalidVectorFault);

        return invalidVector.Status == PanicSafeOwnedCom1ReportStatus.InvalidVector &&
            !invalidVector.OwnedBackendUsed &&
            !invalidVector.FirmwareFallbackUsed &&
            NoIoCaptured();
    }

    private static bool TestRejectsSmallScratchWithoutMutation()
    {
        Span<byte> scratch = stackalloc byte[32];
        scratch.Fill(0xa5);
        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();

        PanicSafeOwnedCom1FormatResult format =
            PanicSafeFormattingSerialReportingOwnedCom1.FormatFaultSummary(
                scratch,
                "idt"u8,
                "null-dereference-probe"u8,
                fault);

        if (format.Succeeded ||
            format.Status != PanicSafeOwnedCom1FormatStatus.DestinationTooSmall ||
            format.RequiredBytes <= scratch.Length ||
            format.BytesWritten != 0)
        {
            return false;
        }

        for (int index = 0; index < scratch.Length; index++)
        {
            if (scratch[index] != 0xa5)
            {
                return false;
            }
        }

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1ReportResult report =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                CreateReadyBackend(),
                scratch,
                "idt"u8,
                "null-dereference-probe"u8,
                fault);

        return !report.Succeeded &&
            report.Status == PanicSafeOwnedCom1ReportStatus.DestinationTooSmall &&
            !report.OwnedBackendUsed &&
            NoIoCaptured();
    }

    private static bool TestHotPathAllocatesNoManagedBytes()
    {
        Span<byte> scratch = stackalloc byte[1024];
        PanicSafeOwnedCom1FaultRecord fault = CreateFaultRecord();
        Com1OwnedPortIoBackend backend = CreateReadyBackend();

        ResetCapturedIo(notReadyReadCount: 0);
        PanicSafeOwnedCom1ReportResult warmup =
            PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                backend,
                scratch,
                "idt"u8,
                "null-dereference-probe"u8,
                fault);
        if (!warmup.Succeeded)
        {
            return false;
        }

        ResetCapturedIo(notReadyReadCount: 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 4; iteration++)
        {
            PanicSafeOwnedCom1ReportResult result =
                PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
                    backend,
                    scratch,
                    "idt"u8,
                    "null-dereference-probe"u8,
                    fault);
            if (!result.Succeeded)
            {
                return false;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return after == before;
    }

    private static bool TestDefaultsAndSourceBoundary()
    {
        PanicSafeOwnedCom1FormatResult format = default;
        PanicSafeOwnedCom1ReportResult report = default;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "PanicSafeFormattingSerialReportingOwnedCOM1.cs"));

        return !format.IsInitialized &&
            !format.Succeeded &&
            format.Status == PanicSafeOwnedCom1FormatStatus.Success &&
            !report.IsInitialized &&
            !report.Succeeded &&
            report.Status == PanicSafeOwnedCom1ReportStatus.Success &&
            !report.Format.IsInitialized &&
            !report.Configuration.IsInitialized &&
            !report.LastOperation.IsInitialized &&
            !report.OwnedBackendUsed &&
            !report.FirmwareFallbackUsed &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<PanicSafeOwnedCom1FaultRecord>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<PanicSafeOwnedCom1FormatResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<PanicSafeOwnedCom1ReportResult>() &&
            source.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal) &&
            source.Contains("Span<byte>", StringComparison.Ordinal) &&
            source.Contains("byte* destination", StringComparison.Ordinal) &&
            source.Contains("TryGetReadinessMarkerByte", StringComparison.Ordinal) &&
            source.Contains("Com1OwnedPortIoBackend", StringComparison.Ordinal) &&
            source.Contains("LineStatusRegisterPort", StringComparison.Ordinal) &&
            source.Contains("DataRegisterPort", StringComparison.Ordinal) &&
            !source.Contains("IPanicSerialByteSink", StringComparison.Ordinal) &&
            !source.Contains("IPlatformSerial", StringComparison.Ordinal) &&
            !source.Contains("GuardedPlatformSerial", StringComparison.Ordinal) &&
            !source.Contains("FirmwareBacked", StringComparison.Ordinal) &&
            !source.Contains("Console", StringComparison.Ordinal) &&
            !source.Contains("SharpOS", StringComparison.Ordinal) &&
            !source.Contains("throw", StringComparison.Ordinal) &&
            !source.Contains("new byte[", StringComparison.Ordinal);
    }

    private static bool PayloadMatches(string expected)
    {
        int payloadOffset = Com1OwnedPortIoBackend.RequiredConfigurationWriteCount;
        for (int index = 0; index < expected.Length; index++)
        {
            if (CapturedWritePorts[payloadOffset + index] !=
                    Com1OwnedPortIoBackend.DataRegisterPort ||
                CapturedWriteValues[payloadOffset + index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static string ExpectedFaultSummary()
    {
        return "OS_SMOKE_FAIL subsystem=idt reason=null-dereference-probe vector=14"
            + " errorCodePresent=true errorCode=0x0000000000000002"
            + " rip=0x0000000010002000 rsp=0x0000000000700000 rbp=0x0000000000700100"
            + " rflags=0x0000000000000202 cr2=0x0000000000001000 cr3=0x00000000001AA000"
            + " rax=0x1111111111111111 rbx=0x2222222222222222 rcx=0x3333333333333333"
            + " rdx=0x4444444444444444 rsi=0x5555555555555555 rdi=0x6666666666666666"
            + " r8=0x8888888888888888 r9=0x9999999999999999 r10=0xAAAAAAAAAAAAAAAA"
            + " r11=0xBBBBBBBBBBBBBBBB r12=0xCCCCCCCCCCCCCCCC r13=0xDDDDDDDDDDDDDDDD"
            + " r14=0xEEEEEEEEEEEEEEEE r15=0xFFFFFFFFFFFFFFFF\r\n";
    }

    private static PanicSafeOwnedCom1FaultRecord CreateFaultRecord(int vector = 14)
    {
        return new PanicSafeOwnedCom1FaultRecord(
            vector,
            hasErrorCode: true,
            errorCode: 0x0000000000000002,
            rip: 0x0000000010002000,
            rsp: 0x0000000000700000,
            rbp: 0x0000000000700100,
            rflags: 0x0000000000000202,
            cr2: 0x0000000000001000,
            cr3: 0x00000000001AA000,
            rax: 0x1111111111111111,
            rbx: 0x2222222222222222,
            rcx: 0x3333333333333333,
            rdx: 0x4444444444444444,
            rsi: 0x5555555555555555,
            rdi: 0x6666666666666666,
            r8: 0x8888888888888888,
            r9: 0x9999999999999999,
            r10: 0xAAAAAAAAAAAAAAAA,
            r11: 0xBBBBBBBBBBBBBBBB,
            r12: 0xCCCCCCCCCCCCCCCC,
            r13: 0xDDDDDDDDDDDDDDDD,
            r14: 0xEEEEEEEEEEEEEEEE,
            r15: 0xFFFFFFFFFFFFFFFF);
    }

    private static Com1OwnedPortIoBackend CreateUnreadyBackend()
    {
        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(
                default(CheckedPortIoFunctionPointerBindingRequest));
        return new Com1OwnedPortIoBackend(binding);
    }

    private static Com1OwnedPortIoBackend CreateReadyBackend()
    {
        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult bindingPlan =
            CreateReadyBindingPlan((nuint)out8Target, (nuint)in8Target);
        CheckedPortIoFunctionPointerBindingResult binding =
            InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(bindingPlan);

        return new Com1OwnedPortIoBackend(binding);
    }

    private static DiagnosticStubCallableBindingPlanResult CreateReadyBindingPlan(
        nuint out8Address,
        nuint in8Address)
    {
        nuint lowestTarget = out8Address < in8Address ? out8Address : in8Address;
        nuint regionCandidate = lowestTarget - ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint pageMask = (nuint)ExecutableStubMemoryPlan.DefaultPageSize - 1;
        nuint regionBaseAddress = regionCandidate & ~pageMask;
        nuint payloadBaseAddress =
            regionBaseAddress + ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint out8EndAddress = out8Address + PortIoStubPlan.Out8RequiredByteLength;
        nuint in8EndAddress = in8Address + PortIoStubPlan.In8RequiredByteLength;
        nuint highestTargetEnd = out8EndAddress > in8EndAddress
            ? out8EndAddress
            : in8EndAddress;

        ExecutableStubBundleMaterializerResult materializer =
            ExecutableStubBundleMaterializerResult.Success();
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
        Com1PortIoBackendContractResult backend =
            Com1PortIoBackendContractResult.Success(
                SerialPortConfiguration.Com1BasePort,
                out8Address - pageTransition.PayloadBaseAddress,
                in8Address - pageTransition.PayloadBaseAddress,
                out8Address,
                in8Address);
        DiagnosticStubInvocationReadinessResult readiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backend);

        return DiagnosticStubCallableBindingPlan.Create(readiness, backend, materializer);
    }

    [UnmanagedCallersOnly]
    private static void CaptureOut8(ushort port, byte value)
    {
        if (_capturedWriteCount < CapturedWritePorts.Length)
        {
            CapturedWritePorts[_capturedWriteCount] = port;
            CapturedWriteValues[_capturedWriteCount] = value;
        }

        _capturedWriteCount++;
    }

    [UnmanagedCallersOnly]
    private static byte CaptureIn8(ushort port)
    {
        _capturedReadCount++;
        _lastReadPort = port;

        if (port != Com1OwnedPortIoBackend.LineStatusRegisterPort)
        {
            return 0;
        }

        return _capturedReadCount <= _notReadyReadCount
            ? (byte)0
            : PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask;
    }

    private static void ResetCapturedIo(int notReadyReadCount)
    {
        Array.Clear(CapturedWritePorts);
        Array.Clear(CapturedWriteValues);
        _capturedWriteCount = 0;
        _capturedReadCount = 0;
        _notReadyReadCount = notReadyReadCount < 0 ? 0 : notReadyReadCount;
        _lastReadPort = 0;
    }

    private static bool NoIoCaptured()
    {
        return _capturedWriteCount == 0 &&
            _capturedReadCount == 0 &&
            _lastReadPort == 0;
    }
}
