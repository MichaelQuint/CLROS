using OS.Kernel.Platform;

namespace OS.Drivers;

public enum DmaBufferOwnership : byte
{
    Free,
    Cpu,
    Device
}
