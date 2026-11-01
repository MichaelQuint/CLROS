namespace OS.Kernel.Runtime;
public readonly struct NativeAotClassConstructorKey
{
    public NativeAotClassConstructorKey(uint moduleId, uint typeId)
    {
        ModuleId = moduleId;
        TypeId = typeId;
    }

    public uint ModuleId { get; }
    public uint TypeId { get; }
    public bool IsValid => ModuleId != 0 && TypeId != 0;

    public bool Equals(NativeAotClassConstructorKey other)
    {
        return ModuleId == other.ModuleId && TypeId == other.TypeId;
    }
}
