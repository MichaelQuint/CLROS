namespace OS.Kernel.Diagnostics;

public enum GuardedIstStackKind : byte
{
    DoubleFault = 1,
    PageFault = 2,
    NonMaskableInterrupt = 3
}
