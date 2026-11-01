namespace OS.Kernel.Diagnostics;
public enum SeparatePageFaultField : byte
{
    None = 0,
    Vector = 1,
    HasErrorCode = 2,
    ErrorCode = 3,
    Cr2 = 4,
    Rip = 5
}
