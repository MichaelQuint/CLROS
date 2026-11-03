namespace OS.Kernel.Runtime;

[Flags]
public enum ExecutableStubMemoryProtection : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
