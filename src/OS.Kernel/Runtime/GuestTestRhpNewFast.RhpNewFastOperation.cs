using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
public enum RhpNewFastOperation : byte
{
    Uninitialized = 0,
    Bind = 1,
    Allocation = 2
}
