using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorOwnershipState
{
    Free = 0,
    InFlight = 1
}
