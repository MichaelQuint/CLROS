using OS.Drivers;
using OS.Kernel.Platform;

namespace OS.Storage;

public enum VirtioBlkDmaStatus : byte
{
    Uninitialized, Success, InvalidRange, InvalidLayout, AllocationFailed
}
