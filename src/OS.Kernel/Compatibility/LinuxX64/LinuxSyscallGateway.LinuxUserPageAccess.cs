namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxUserPageAccess : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
