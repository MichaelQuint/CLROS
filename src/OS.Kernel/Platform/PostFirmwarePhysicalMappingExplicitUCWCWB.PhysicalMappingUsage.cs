using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;
public enum PhysicalMappingUsage : byte
{
    Uninitialized = 0,
    Normal = 1,
    Device = 2,
    Framebuffer = 3
}
