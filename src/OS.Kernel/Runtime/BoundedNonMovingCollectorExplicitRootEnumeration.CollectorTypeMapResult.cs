using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>
/// Immediate metadata observation, not a retained allocation or root capability.
/// Collection must revalidate original metadata and allocation ownership before
/// following any object reference. This result alone authorizes no heap writes.
/// </summary>

public readonly struct CollectorTypeMapResult
{
    internal CollectorTypeMapResult(CollectorTypeMapStatus status, TypeManagerModuleStatus moduleStatus = default, ulong methodTable = 0, NativeAotMethodTablePrefix prefix = default, uint count = 0, CollectorReferenceSeries first = default, CollectorReferenceSeries second = default, CollectorReferenceSeries third = default, CollectorReferenceSeries fourth = default)
    {
        Status = status;
        ModuleStatus = moduleStatus;
        MethodTable = methodTable;
        Prefix = prefix;
        SeriesCount = count;
        this.first = first;
        this.second = second;
        this.third = third;
        this.fourth = fourth;
    }

    private readonly CollectorReferenceSeries first, second, third, fourth;
    public CollectorTypeMapStatus Status { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public ulong MethodTable { get; }
    public NativeAotMethodTablePrefix Prefix { get; }
    public uint SeriesCount { get; }
    public bool Succeeded => Status == CollectorTypeMapStatus.Success;

    public bool TryGetSeries(uint index, out CollectorReferenceSeries series)
    {
        series = default;
        if (!Succeeded || index >= SeriesCount)
            return false;
        series = index switch
        {
            0 => first,
            1 => second,
            2 => third,
            _ => fourth
        };
        return true;
    }
}
