namespace OS.Kernel.Diagnostics;

public readonly struct PanicContext
{
    public PanicContext(string? subsystem, string? reason, int? vector)
    {
        Subsystem = subsystem;
        Reason = reason;
        Vector = vector;
    }

    public string? Subsystem { get; }

    public string? Reason { get; }

    public int? Vector { get; }
}

public static class PanicFormatter
{
    public const string NullDereferenceProbe = "null-dereference-probe";

    public const string InvalidOpcodeProbe = "invalid-opcode-probe";

    public static string Format(PanicContext context)
    {
        return Format(context.Subsystem, context.Reason, context.Vector);
    }

    public static string Format(string? subsystem, string? reason, int? vector)
    {
        // This host-testable formatter uses managed strings. A future IDT path
        // should add an allocation-free writer before running inside faults.
        string message = SerialSmokeMarkers.OsSmokeFail;

        if (!string.IsNullOrWhiteSpace(subsystem))
        {
            message += " subsystem=" + subsystem;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            message += " reason=" + reason;
        }

        if (vector.HasValue)
        {
            message += " vector=" + vector.Value;
        }

        return message;
    }

    public static string FormatFault(string? subsystem, string? reason, CpuFaultFrame frame)
    {
        return Format(subsystem, reason, frame.Vector)
            + " errorCodePresent=" + (frame.HasErrorCode ? "true" : "false")
            + " errorCode=" + Hex(frame.ErrorCode)
            + " rip=" + Hex(frame.Rip)
            + " rsp=" + Hex(frame.Rsp)
            + " rbp=" + Hex(frame.Rbp)
            + " rflags=" + Hex(frame.Rflags)
            + " cr2=" + Hex(frame.Cr2)
            + " cr3=" + Hex(frame.Cr3)
            + " rax=" + Hex(frame.Rax)
            + " rbx=" + Hex(frame.Rbx)
            + " rcx=" + Hex(frame.Rcx)
            + " rdx=" + Hex(frame.Rdx)
            + " rsi=" + Hex(frame.Rsi)
            + " rdi=" + Hex(frame.Rdi)
            + " r8=" + Hex(frame.R8)
            + " r9=" + Hex(frame.R9)
            + " r10=" + Hex(frame.R10)
            + " r11=" + Hex(frame.R11)
            + " r12=" + Hex(frame.R12)
            + " r13=" + Hex(frame.R13)
            + " r14=" + Hex(frame.R14)
            + " r15=" + Hex(frame.R15);
    }

    private static string Hex(ulong value)
    {
        return "0x" + value.ToString("X16");
    }
}
