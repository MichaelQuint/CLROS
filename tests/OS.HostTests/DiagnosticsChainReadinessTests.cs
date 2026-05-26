using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class DiagnosticsChainReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.chain readiness accepts complete chain", TestAcceptsCompleteChain, ref failures);
        Run("diagnostics.chain readiness rejects serial backend failure", TestRejectsSerialBackendFailure, ref failures);
        Run("diagnostics.chain readiness rejects descriptor failure", TestRejectsDescriptorFailure, ref failures);
        Run("diagnostics.chain readiness rejects fault probe failure", TestRejectsFaultProbeFailure, ref failures);
        Run("diagnostics.chain readiness rejects panic failure", TestRejectsPanicFailure, ref failures);
        Run("diagnostics.chain readiness rejects stub materialization failure", TestRejectsStubMaterializationFailure, ref failures);
        Run("diagnostics.chain readiness default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("diagnostics.chain readiness result contains no managed references", TestResultContainsNoManagedReferences, ref failures);
        Run("boot.sequence diagnostics chain emits ready marker", TestBootSequenceReadyMarker, ref failures);
        Run("boot.sequence diagnostics chain emits blocked marker", TestBootSequenceBlockedMarker, ref failures);
        Run("boot.sequence diagnostics chain leaves default markers unchanged", TestBootSequenceDefaultMarkersUnchanged, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Diagnostics chain readiness host tests failed: " + failures);
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

    private static bool TestAcceptsCompleteChain()
    {
        DiagnosticsChainReadinessResult result = CreateReadyChain();

        return result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticsChainReadinessStatus.Ready
            && result.SerialBackendStatus == Com1PortIoBackendPlanStatus.Success
            && result.DescriptorTableStatus == DescriptorTableInstallReadinessStatus.Ready
            && result.FaultProbeStatus == FaultProbeReadinessStatus.Ready
            && result.CpuFaultPanicStatus == CpuFaultPanicReadinessStatus.Ready
            && result.StubMaterializationStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.StubMaterializationBytesCopied > 0;
    }

    private static bool TestRejectsSerialBackendFailure()
    {
        DiagnosticsChainReadinessResult result = DiagnosticsChainReadiness.RequireReady(
            Com1PortIoBackendPlanResult.InvalidSerialConfiguration(
                SerialPortConfigurationStatus.InvalidBasePort,
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                availableStubBytes: 0),
            CreateReadyDescriptorInstallResult(),
            CreateReadyFaultProbeResult(),
            CreateReadyCpuFaultPanicResult(),
            ExecutableStubBundleMaterializerResult.Success());

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticsChainReadinessStatus.SerialBackendNotReady
            && result.SerialBackendStatus == Com1PortIoBackendPlanStatus.InvalidSerialConfiguration;
    }

    private static bool TestRejectsDescriptorFailure()
    {
        DiagnosticsChainReadinessResult result = DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.IdtTableNotReady,
                IdtTablePlanStatus.MissingRequiredVector,
                IdtrDescriptorStatus.Ok,
                GdtDescriptorPlanStatus.Ok,
                TssIstPlanStatus.Ok),
            CreateReadyFaultProbeResult(),
            CreateReadyCpuFaultPanicResult(),
            ExecutableStubBundleMaterializerResult.Success());

        return !result.Succeeded
            && result.Status == DiagnosticsChainReadinessStatus.DescriptorTablesNotReady
            && result.SerialBackendStatus == Com1PortIoBackendPlanStatus.Success
            && result.DescriptorTableStatus == DescriptorTableInstallReadinessStatus.IdtTableNotReady;
    }

    private static bool TestRejectsFaultProbeFailure()
    {
        DiagnosticsChainReadinessResult result = DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.MemoryPlanTooSmall),
            CreateReadyCpuFaultPanicResult(),
            ExecutableStubBundleMaterializerResult.Success());

        return !result.Succeeded
            && result.Status == DiagnosticsChainReadinessStatus.FaultProbeNotReady
            && result.FaultProbeStatus == FaultProbeReadinessStatus.MemoryPlanTooSmall;
    }

    private static bool TestRejectsPanicFailure()
    {
        DiagnosticsChainReadinessResult result = DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            CreateReadyFaultProbeResult(),
            default,
            ExecutableStubBundleMaterializerResult.Success());

        return !result.Succeeded
            && result.Status == DiagnosticsChainReadinessStatus.CpuFaultPanicNotReady
            && result.CpuFaultPanicStatus == CpuFaultPanicReadinessStatus.Ready;
    }

    private static bool TestRejectsStubMaterializationFailure()
    {
        ExecutableStubBundleMaterializerResult materializer =
            ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                ExecutableStubMemoryPlanStatus.Success);

        DiagnosticsChainReadinessResult result = DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            CreateReadyFaultProbeResult(),
            CreateReadyCpuFaultPanicResult(),
            materializer);

        return !result.Succeeded
            && result.Status == DiagnosticsChainReadinessStatus.StubMaterializationNotReady
            && result.StubMaterializationStatus == ExecutableStubBundleMaterializerStatus.InvalidBundlePlan
            && result.StubMaterializationBytesCopied == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DiagnosticsChainReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticsChainReadinessStatus.Ready
            && result.SerialBackendStatus == Com1PortIoBackendPlanStatus.Success
            && result.DescriptorTableStatus == DescriptorTableInstallReadinessStatus.Ready
            && result.FaultProbeStatus == FaultProbeReadinessStatus.Ready
            && result.CpuFaultPanicStatus == CpuFaultPanicReadinessStatus.Ready
            && result.StubMaterializationStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.StubMaterializationBytesCopied == 0;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<DiagnosticsChainReadinessResult>();
    }

    private static bool TestBootSequenceReadyMarker()
    {
        DiagnosticsChainReadinessResult readiness = CreateReadyChain();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunDiagnosticsChainReadiness(readiness, serial, events);

        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.DiagnosticsChainReady
            && events.Snapshot()[0] == BootSequence.DiagnosticsChainReady;
    }

    private static bool TestBootSequenceBlockedMarker()
    {
        DiagnosticsChainReadinessResult readiness = DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            CreateReadyFaultProbeResult(),
            default,
            ExecutableStubBundleMaterializerResult.Success());
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateDiagnosticsChainNotReadyMarker(readiness);

        BootResult result = BootSequence.RunDiagnosticsChainReadiness(readiness, serial, events);

        return !result.Succeeded
            && result.Reason.Contains(nameof(DiagnosticsChainReadinessStatus.CpuFaultPanicNotReady), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestBootSequenceDefaultMarkersUnchanged()
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

        return !serial.Contains(BootSequence.DiagnosticsChainReady)
            && !serial.ContainsPrefix(BootSequence.DiagnosticsChainNotReadyPrefix);
    }

    private static DiagnosticsChainReadinessResult CreateReadyChain()
    {
        return DiagnosticsChainReadiness.RequireReady(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            CreateReadyFaultProbeResult(),
            CreateReadyCpuFaultPanicResult(),
            ExecutableStubBundleMaterializerResult.Success());
    }

    private static Com1PortIoBackendPlanResult CreateReadyCom1BackendResult()
    {
        return Com1PortIoBackendPlanResult.Success(
            Com1PortIoBackendPlan.RequiredExecutableStubBytes,
            availableStubBytes: 4096);
    }

    private static DescriptorTableInstallReadinessResult CreateReadyDescriptorInstallResult()
    {
        return DescriptorTableInstallReadinessResult.Pass(
            IdtTablePlanStatus.Ok,
            IdtrDescriptorStatus.Ok,
            GdtDescriptorPlanStatus.Ok,
            TssIstPlanStatus.Ok);
    }

    private static FaultProbeReadinessResult CreateReadyFaultProbeResult()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        return FaultProbeReadiness.RequireReady(invalidOpcodeBuffer, nullWriteBuffer, memoryPlan);
    }

    private static CpuFaultPanicReadinessResult CreateReadyCpuFaultPanicResult()
    {
        byte[] frameBytes = new byte[CpuFaultEntryFrameLayout.TotalSize];
        CpuFaultEntryFrameLayoutResult writeResult = CpuFaultEntryFrameLayout.TryWrite(
            frameBytes,
            CreateFaultFrame(),
            codeSegment: 0x0008,
            stackSegment: 0x0010);

        if (!writeResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to encode diagnostics chain CPU fault frame.");
        }

        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);
        return CpuFaultPanicReadiness.RequireReady(
            frameBytes,
            scratch,
            sink,
            "idt",
            "diagnostics-chain");
    }

    private static CpuFaultFrame CreateFaultFrame()
    {
        return new CpuFaultFrame(
            FaultVectorCatalog.PageFault,
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

    private sealed class CapturingByteSink : IPanicSerialByteSink
    {
        private readonly byte[] _buffer;
        private int _written;

        public CapturingByteSink(int capacity)
        {
            _buffer = new byte[capacity];
        }

        public bool TryWriteByte(byte value)
        {
            if (_written >= _buffer.Length)
            {
                return false;
            }

            _buffer[_written] = value;
            _written++;
            return true;
        }
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
