namespace OS.Kernel.Diagnostics;
/// <summary>
/// Identifies the first deterministic synthetic-frame comparison.
/// </summary>

public enum DisabledDefaultUd2VectorField : byte
{
    None = 0,
    Vector = 1,
    HasErrorCode = 2,
    ErrorCode = 3,
    Rip = 4,
    Rsp = 5,
    Rbp = 6,
    Rflags = 7,
    Cr2 = 8,
    Cr3 = 9,
    Rax = 10,
    Rbx = 11,
    Rcx = 12,
    Rdx = 13,
    Rsi = 14,
    Rdi = 15,
    R8 = 16,
    R9 = 17,
    R10 = 18,
    R11 = 19,
    R12 = 20,
    R13 = 21,
    R14 = 22,
    R15 = 23
}
