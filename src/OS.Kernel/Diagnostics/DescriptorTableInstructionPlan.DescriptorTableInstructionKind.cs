using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum DescriptorTableInstructionKind
{
    Unknown = 0,
    Lidt = 1,
    Lgdt = 2,
    Ltr = 3
}
