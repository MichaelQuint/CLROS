using OS.Boot;

namespace OS.Kernel.Runtime;
public readonly struct TypeManagerModuleResult
{
    internal TypeManagerModuleResult(TypeManagerModuleStatus status, ulong requiredStorage = 0, ulong handle = 0, ulong section = 0, int length = 0)
    {
        Status = status;
        RequiredStorage = requiredStorage;
        Handle = handle;
        Section = section;
        Length = length;
    }

    public TypeManagerModuleStatus Status { get; }
    public ulong RequiredStorage { get; }
    public ulong Handle { get; }
    public ulong Section { get; }
    public int Length { get; }
    public bool Succeeded => Status == TypeManagerModuleStatus.Success;
}
