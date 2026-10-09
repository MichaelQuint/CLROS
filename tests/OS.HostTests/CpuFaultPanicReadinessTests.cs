using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class CpuFaultPanicReadinessTests
{

    [HostTest("diagnostics.cpu_fault_panic readiness emits os smoke fail", HostLane.Fast, 0)]
    private static bool TestReadinessEmitsOsSmokeFail()
    {
        byte[] frameBytes = CreateEncodedFrame();
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);

        CpuFaultPanicReadinessResult result = CpuFaultPanicReadiness.RequireReady(
            frameBytes,
            scratch,
            sink,
            "idt",
            PanicFormatter.NullDereferenceProbe);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == CpuFaultPanicReadinessStatus.Ready
            && result.FrameStatus == CpuFaultEntryFrameLayoutStatus.Ok
            && result.ReporterStatus == PanicSerialReporterStatus.Ok
            && result.Vector == FaultVectorCatalog.PageFault
            && result.RequiredFrameBytes == CpuFaultEntryFrameLayout.TotalSize
            && result.BytesFormatted == ExpectedFaultSummary().Length
            && result.BytesFlushed == ExpectedFaultSummary().Length
            && sink.Text == ExpectedFaultSummary());
    }

    [HostTest("diagnostics.cpu_fault_panic readiness rejects invalid frame", HostLane.Fast, 1)]
    private static bool TestReadinessRejectsInvalidFrame()
    {
        byte[] frameBytes = new byte[CpuFaultEntryFrameLayout.TotalSize - 1];
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);

        CpuFaultPanicReadinessResult result = CpuFaultPanicReadiness.RequireReady(
            frameBytes,
            scratch,
            sink,
            "idt",
            PanicFormatter.NullDereferenceProbe);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == CpuFaultPanicReadinessStatus.FaultFrameInvalid
            && result.FrameStatus == CpuFaultEntryFrameLayoutStatus.BufferTooSmall
            && result.ReporterStatus == PanicSerialReporterStatus.Ok
            && result.RequiredFrameBytes == CpuFaultEntryFrameLayout.TotalSize
            && result.BytesFormatted == 0
            && result.BytesFlushed == 0
            && sink.BytesWritten == 0);
    }

    [HostTest("diagnostics.cpu_fault_panic readiness reports reporter failure", HostLane.Fast, 2)]
    private static bool TestReadinessReportsReporterFailure()
    {
        byte[] frameBytes = CreateEncodedFrame();
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024, failAfterBytes: 8);

        CpuFaultPanicReadinessResult result = CpuFaultPanicReadiness.RequireReady(
            frameBytes,
            scratch,
            sink,
            "idt",
            PanicFormatter.NullDereferenceProbe);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == CpuFaultPanicReadinessStatus.PanicReportFailed
            && result.FrameStatus == CpuFaultEntryFrameLayoutStatus.Ok
            && result.ReporterStatus == PanicSerialReporterStatus.SinkFailure
            && result.Vector == FaultVectorCatalog.PageFault
            && result.BytesFormatted == ExpectedFaultSummary().Length
            && result.BytesFlushed == 8
            && sink.Text == ExpectedFaultSummary().Substring(0, 8));
    }

    [HostTest("diagnostics.cpu_fault_panic readiness default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        CpuFaultPanicReadinessResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessStatus.Ready
            && result.FrameStatus == CpuFaultEntryFrameLayoutStatus.Ok
            && result.ReporterStatus == PanicSerialReporterStatus.Ok
            && result.BytesFormatted == 0
            && result.BytesFlushed == 0);
    }

    [HostTest("diagnostics.cpu_fault_panic readiness result contains no managed references", HostLane.Fast, 4)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<CpuFaultPanicReadinessResult>());
    }

    [HostTest("boot.sequence cpu fault panic readiness emits ready marker", HostLane.Fast, 5)]
    private static bool TestBootSequenceReadyMarker()
    {
        CpuFaultPanicReadinessResult readiness = CreateReadyPanicReadiness();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunCpuFaultPanicReadiness(readiness, serial, events);

        return HostCheck.Result(readiness.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.CpuFaultPanicReady
            && events.Snapshot()[0] == BootSequence.CpuFaultPanicReady);
    }

    [HostTest("boot.sequence cpu fault panic readiness emits blocked marker", HostLane.Fast, 6)]
    private static bool TestBootSequenceBlockedMarker()
    {
        CpuFaultPanicReadinessResult readiness = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateCpuFaultPanicNotReadyMarker(
            CpuFaultPanicBootReadinessStatus.PanicReadinessFailed,
            readiness);

        BootResult result = BootSequence.RunCpuFaultPanicReadiness(readiness, serial, events);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(CpuFaultPanicBootReadinessStatus.PanicReadinessFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(nameof(CpuFaultPanicReadinessStatus.Ready), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence cpu fault panic readiness preserves default markers", HostLane.Fast, 7)]
    private static bool TestBootSequenceDefaultMarkersUnchanged()
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

        return HostCheck.Result(!serial.Contains(BootSequence.CpuFaultPanicReady)
            && !serial.ContainsPrefix(BootSequence.CpuFaultPanicNotReadyPrefix));
    }

    private static CpuFaultPanicReadinessResult CreateReadyPanicReadiness()
    {
        byte[] frameBytes = CreateEncodedFrame();
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);

        return CpuFaultPanicReadiness.RequireReady(
            frameBytes,
            scratch,
            sink,
            "idt",
            PanicFormatter.NullDereferenceProbe);
    }

    private static byte[] CreateEncodedFrame()
    {
        byte[] frameBytes = new byte[CpuFaultEntryFrameLayout.TotalSize];
        CpuFaultEntryFrameLayoutResult result = CpuFaultEntryFrameLayout.TryWrite(
            frameBytes,
            CreateFrame(),
            codeSegment: 0x0008,
            stackSegment: 0x0010);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to encode test CPU fault frame.");
        }

        return frameBytes;
    }

    private static CpuFaultFrame CreateFrame()
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

    private sealed class CapturingByteSink : IPanicSerialByteSink
    {
        private readonly byte[] _buffer;
        private readonly int _failAfterBytes;
        private int _written;

        public CapturingByteSink(int capacity)
            : this(capacity, -1)
        {
        }

        public CapturingByteSink(int capacity, int failAfterBytes)
        {
            _buffer = new byte[capacity];
            _failAfterBytes = failAfterBytes;
        }

        public int BytesWritten => _written;

        public string Text => Encoding.ASCII.GetString(_buffer, 0, _written);

        public bool TryWriteByte(byte value)
        {
            if (_failAfterBytes >= 0 && _written >= _failAfterBytes)
            {
                return false;
            }

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
