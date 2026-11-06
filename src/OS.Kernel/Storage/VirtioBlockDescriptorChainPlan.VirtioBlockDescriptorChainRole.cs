using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorChainRole
{
    RequestHeader = 0,
    DataBuffer = 1,
    StatusByte = 2
}
