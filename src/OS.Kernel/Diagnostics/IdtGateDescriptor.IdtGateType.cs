using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public enum IdtGateType : byte
{
    InterruptGate = 0x0e,
    TrapGate = 0x0f
}
