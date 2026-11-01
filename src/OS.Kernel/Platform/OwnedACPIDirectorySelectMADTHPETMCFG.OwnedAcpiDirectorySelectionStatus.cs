namespace OS.Kernel.Platform;
public enum OwnedAcpiDirectorySelectionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    InvalidDirectory = 3,
    InvalidRsdp = 4,
    InvalidTable = 5,
    InvalidRoot = 6
}
