using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class PanicTextBufferTests
{

    [HostTest("panic text buffer writes ascii lines", HostLane.Fast, 0)]
    private static bool TestAsciiLines()
    {
        byte[] storage = new byte[64];
        PanicTextBuffer buffer = new PanicTextBuffer(storage);

        PanicTextBufferStatus first = buffer.WriteAsciiLine("OS_SMOKE_FAIL");
        PanicTextBufferStatus second = buffer.WriteAsciiLine("reason=manual-probe");

        return HostCheck.Result(first == PanicTextBufferStatus.Ok
            && second == PanicTextBufferStatus.Ok
            && buffer.IsInitialized
            && buffer.Succeeded
            && buffer.Status == PanicTextBufferStatus.Ok
            && ToText(buffer.WrittenSpan) == "OS_SMOKE_FAIL\r\nreason=manual-probe\r\n");
    }

    [HostTest("panic text buffer writes decimal and hex", HostLane.Fast, 1)]
    private static bool TestDecimalAndHex()
    {
        byte[] storage = new byte[64];
        PanicTextBuffer buffer = new PanicTextBuffer(storage);

        buffer.WriteAscii("vector=");
        buffer.WriteUnsignedDecimal(14);
        buffer.WriteAscii(" rip=");
        buffer.WriteHex64(0x0123456789ABCDEF);
        PanicTextBufferStatus status = buffer.WriteCrLf();

        return HostCheck.Result(status == PanicTextBufferStatus.Ok
            && buffer.IsInitialized
            && buffer.Succeeded
            && buffer.Status == PanicTextBufferStatus.Ok
            && ToText(buffer.WrittenSpan) == "vector=14 rip=0x0123456789ABCDEF\r\n");
    }

    [HostTest("panic text buffer reports overflow", HostLane.Fast, 2)]
    private static bool TestOverflow()
    {
        byte[] storage = new byte[8];
        for (int index = 0; index < storage.Length; index++)
        {
            storage[index] = 0xcc;
        }

        PanicTextBuffer buffer = new PanicTextBuffer(storage.AsSpan(0, 5));
        PanicTextBufferStatus status = buffer.WriteAsciiLine("abcdef");

        return HostCheck.Result(status == PanicTextBufferStatus.Overflow
            && buffer.IsInitialized
            && !buffer.Succeeded
            && buffer.Status == PanicTextBufferStatus.Overflow
            && buffer.Overflowed
            && buffer.Truncated
            && buffer.Written == 5
            && ToText(buffer.WrittenSpan) == "abcde"
            && storage[5] == 0xcc
            && storage[6] == 0xcc
            && storage[7] == 0xcc);
    }

    [HostTest("panic text buffer formats fault summary", HostLane.Fast, 3)]
    private static bool TestFaultSummary()
    {
        byte[] storage = new byte[1024];
        PanicTextBuffer buffer = new PanicTextBuffer(storage);
        CpuFaultFrame frame = CreateFrame(
            vector: 14,
            hasErrorCode: true,
            errorCode: 0x0000000000000002,
            cr2: 0x0000000000001000);

        PanicTextBufferStatus status = buffer.WriteFaultSummary(
            "idt",
            PanicFormatter.NullDereferenceProbe,
            frame);

        string expected = "OS_SMOKE_FAIL subsystem=idt reason=null-dereference-probe vector=14"
            + " errorCodePresent=true errorCode=0x0000000000000002"
            + " rip=0x0000000010002000 rsp=0x0000000000700000 rbp=0x0000000000700100"
            + " rflags=0x0000000000000202 cr2=0x0000000000001000 cr3=0x00000000001AA000"
            + " rax=0x1111111111111111 rbx=0x2222222222222222 rcx=0x3333333333333333"
            + " rdx=0x4444444444444444 rsi=0x5555555555555555 rdi=0x6666666666666666"
            + " r8=0x8888888888888888 r9=0x9999999999999999 r10=0xAAAAAAAAAAAAAAAA"
            + " r11=0xBBBBBBBBBBBBBBBB r12=0xCCCCCCCCCCCCCCCC r13=0xDDDDDDDDDDDDDDDD"
            + " r14=0xEEEEEEEEEEEEEEEE r15=0xFFFFFFFFFFFFFFFF\r\n";

        return HostCheck.Result(status == PanicTextBufferStatus.Ok
            && buffer.IsInitialized
            && buffer.Succeeded
            && buffer.Status == PanicTextBufferStatus.Ok
            && ToText(buffer.WrittenSpan) == expected);
    }

    [HostTest("panic text buffer default buffer is not success", HostLane.Fast, 4)]
    private static bool TestDefaultBufferIsNotSuccess()
    {
        PanicTextBuffer buffer = default;

        return HostCheck.Result(!buffer.IsInitialized
            && !buffer.Succeeded
            && buffer.Status == PanicTextBufferStatus.Ok
            && buffer.Capacity == 0
            && buffer.Written == 0
            && buffer.Remaining == 0
            && buffer.WrittenSpan.Length == 0);
    }

    [HostTest("panic text buffer uses shared ascii whitespace helper", HostLane.Fast, 5)]
    private static bool TestUsesSharedAsciiWhitespaceHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Diagnostics", "PanicTextBuffer.cs"));

        return HostCheck.Result(source.Contains("OS.Std.Ascii.IsWhiteSpace(value[index])", StringComparison.Ordinal)
            && !source.Contains("private static bool IsAsciiWhiteSpace", StringComparison.Ordinal));
    }

    [HostTest("panic text buffer uses shared ascii text builder", HostLane.Fast, 6)]
    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Diagnostics", "PanicTextBuffer.cs"));

        return HostCheck.Result(source.Contains("AsciiTextBuilder.AppendUInt64Decimal", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Hex", StringComparison.Ordinal)
            && !source.Contains("private static byte GetUpperHexDigit", StringComparison.Ordinal)
            && !source.Contains("for (int shift = 60", StringComparison.Ordinal));
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

    private static string ToText(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }
}
