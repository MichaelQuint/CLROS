namespace OS.Kernel.Runtime;
[Flags]
public enum ExecutableStubMemoryPlanFlags : byte
{
    None = 0,
    AllowTemporaryWritableExecutableException = 1
}
