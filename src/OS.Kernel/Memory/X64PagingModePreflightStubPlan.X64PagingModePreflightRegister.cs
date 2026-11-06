namespace OS.Kernel.Memory;
/// <summary>
/// Stable register/domain identifiers used by the semantic instruction plan.
/// The plan deliberately has no XMM register identifiers.
/// </summary>

public enum X64PagingModePreflightRegister : byte
{
    Uninitialized = 0,
    None = 1,
    Rax = 2,
    Rbx = 3,
    Rcx = 4,
    Rdx = 5,
    R8 = 6,
    Rflags = 7,
    Cr0 = 8,
    Cr3 = 9,
    Cr4 = 10,
    Ia32Efer = 11,
    CpuidEaxEbxEcxEdx = 12,
    ObservationMemory = 13,
    Stack = 14
}
