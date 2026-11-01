namespace OS.Kernel.Compatibility.LinuxX64;
public enum LinuxElfMaterializeStatus : byte
{
    Uninitialized,
    Ready,
    InvalidElf,
    InvalidStorage,
    OverlappingStorage,
    ReadBackFailed
}
