using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static partial class PanicSerialReporterTests
{
    [HostTest("panic serial reporter flushes fault summary", HostLane.Fast, 0)]
    private static bool TestFlushesFaultSummary()
    {
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);
        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(sink, scratch, "idt", PanicFormatter.NullDereferenceProbe, CreateFrame(vector: 14, hasErrorCode: true, errorCode: 0x0000000000000002, cr2: 0x0000000000001000));
        string expected = ExpectedFaultSummary();
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == PanicSerialReporterStatus.Ok && result.BytesFormatted == expected.Length && result.BytesFlushed == expected.Length && sink.BytesWritten == expected.Length && sink.Text == expected);
    }

    [HostTest("panic serial reporter reports buffer overflow", HostLane.Fast, 1)]
    private static bool TestReportsBufferOverflow()
    {
        byte[] scratch = new byte[16];
        CapturingByteSink sink = new CapturingByteSink(1024);
        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(sink, scratch, "idt", PanicFormatter.NullDereferenceProbe, CreateFrame(vector: 14, hasErrorCode: true, errorCode: 0x0000000000000002, cr2: 0x0000000000001000));
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == PanicSerialReporterStatus.BufferOverflow && result.BytesFormatted == scratch.Length && result.BytesFlushed == 0 && sink.BytesWritten == 0);
    }

    [HostTest("panic serial reporter reports sink failure", HostLane.Fast, 2)]
    private static bool TestReportsSinkFailure()
    {
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024, failAfterBytes: 10);
        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(sink, scratch, "idt", PanicFormatter.NullDereferenceProbe, CreateFrame(vector: 14, hasErrorCode: true, errorCode: 0x0000000000000002, cr2: 0x0000000000001000));
        string expected = ExpectedFaultSummary();
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == PanicSerialReporterStatus.SinkFailure && result.BytesFormatted == expected.Length && result.BytesFlushed == 10 && sink.BytesWritten == 10 && sink.Text == expected.Substring(0, 10));
    }

    [HostTest("panic serial reporter default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PanicSerialReporterResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == PanicSerialReporterStatus.Ok && result.BytesFormatted == 0 && result.BytesFlushed == 0);
    }

    private static CpuFaultFrame CreateFrame(int vector, bool hasErrorCode, ulong errorCode, ulong cr2)
    {
        return new CpuFaultFrame(vector, hasErrorCode, errorCode, rip: 0x0000000010002000, rsp: 0x0000000000700000, rbp: 0x0000000000700100, rflags: 0x0000000000000202, cr2, cr3: 0x00000000001AA000, rax: 0x1111111111111111, rbx: 0x2222222222222222, rcx: 0x3333333333333333, rdx: 0x4444444444444444, rsi: 0x5555555555555555, rdi: 0x6666666666666666, r8: 0x8888888888888888, r9: 0x9999999999999999, r10: 0xAAAAAAAAAAAAAAAA, r11: 0xBBBBBBBBBBBBBBBB, r12: 0xCCCCCCCCCCCCCCCC, r13: 0xDDDDDDDDDDDDDDDD, r14: 0xEEEEEEEEEEEEEEEE, r15: 0xFFFFFFFFFFFFFFFF);
    }

    private static string ExpectedFaultSummary()
    {
        return "OS_SMOKE_FAIL subsystem=idt reason=null-dereference-probe vector=14" + " errorCodePresent=true errorCode=0x0000000000000002" + " rip=0x0000000010002000 rsp=0x0000000000700000 rbp=0x0000000000700100" + " rflags=0x0000000000000202 cr2=0x0000000000001000 cr3=0x00000000001AA000" + " rax=0x1111111111111111 rbx=0x2222222222222222 rcx=0x3333333333333333" + " rdx=0x4444444444444444 rsi=0x5555555555555555 rdi=0x6666666666666666" + " r8=0x8888888888888888 r9=0x9999999999999999 r10=0xAAAAAAAAAAAAAAAA" + " r11=0xBBBBBBBBBBBBBBBB r12=0xCCCCCCCCCCCCCCCC r13=0xDDDDDDDDDDDDDDDD" + " r14=0xEEEEEEEEEEEEEEEE r15=0xFFFFFFFFFFFFFFFF\r\n";
    }
}
