using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class PanicSerialReporterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("panic serial reporter flushes fault summary", TestFlushesFaultSummary, ref failures);
        Run("panic serial reporter reports buffer overflow", TestReportsBufferOverflow, ref failures);
        Run("panic serial reporter reports sink failure", TestReportsSinkFailure, ref failures);
        Run("panic serial reporter default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Panic serial reporter host tests failed: " + failures);
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

    private static bool TestFlushesFaultSummary()
    {
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024);

        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(
            sink,
            scratch,
            "idt",
            PanicFormatter.NullDereferenceProbe,
            CreateFrame(
                vector: 14,
                hasErrorCode: true,
                errorCode: 0x0000000000000002,
                cr2: 0x0000000000001000));

        string expected = ExpectedFaultSummary();

        return result.Succeeded
            && result.IsInitialized
            && result.Status == PanicSerialReporterStatus.Ok
            && result.BytesFormatted == expected.Length
            && result.BytesFlushed == expected.Length
            && sink.BytesWritten == expected.Length
            && sink.Text == expected;
    }

    private static bool TestReportsBufferOverflow()
    {
        byte[] scratch = new byte[16];
        CapturingByteSink sink = new CapturingByteSink(1024);

        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(
            sink,
            scratch,
            "idt",
            PanicFormatter.NullDereferenceProbe,
            CreateFrame(
                vector: 14,
                hasErrorCode: true,
                errorCode: 0x0000000000000002,
                cr2: 0x0000000000001000));

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PanicSerialReporterStatus.BufferOverflow
            && result.BytesFormatted == scratch.Length
            && result.BytesFlushed == 0
            && sink.BytesWritten == 0;
    }

    private static bool TestReportsSinkFailure()
    {
        byte[] scratch = new byte[1024];
        CapturingByteSink sink = new CapturingByteSink(1024, failAfterBytes: 10);

        PanicSerialReporterResult result = PanicSerialReporter.ReportFaultSummary(
            sink,
            scratch,
            "idt",
            PanicFormatter.NullDereferenceProbe,
            CreateFrame(
                vector: 14,
                hasErrorCode: true,
                errorCode: 0x0000000000000002,
                cr2: 0x0000000000001000));

        string expected = ExpectedFaultSummary();

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PanicSerialReporterStatus.SinkFailure
            && result.BytesFormatted == expected.Length
            && result.BytesFlushed == 10
            && sink.BytesWritten == 10
            && sink.Text == expected.Substring(0, 10);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PanicSerialReporterResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PanicSerialReporterStatus.Ok
            && result.BytesFormatted == 0
            && result.BytesFlushed == 0;
    }

    private static CpuFaultFrame CreateFrame(int vector, bool hasErrorCode, ulong errorCode, ulong cr2)
    {
        return new CpuFaultFrame(
            vector,
            hasErrorCode,
            errorCode,
            rip: 0x0000000010002000,
            rsp: 0x0000000000700000,
            rbp: 0x0000000000700100,
            rflags: 0x0000000000000202,
            cr2,
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
}
