using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>
/// One stable, initially zeroed controller for one retained page. Collecting mode
/// uses additional caller storage; the original 80-byte prefix stays fixed.
/// Self-binding detects accidental copies, not forged PODs,
/// external resets, ABA, duplicate controllers or concurrent modification.
/// </summary>

public struct RhpNewFastBumpState
{
    internal ulong InitializedValue;
    internal ulong SelfAddressValue;
    internal ulong CapacityValue;
    internal ulong HeapAddressValue;
    internal ulong HeapCapacityValue;
    internal ulong MappedStartValue;
    internal ulong PhysicalStartValue;
    internal ulong PhysicalEndValue;
    internal ulong UsedValue;
    internal ulong CountValue;
    public bool IsInitialized => InitializedValue == 1 || InitializedValue == 2;
    public bool IsCollecting => InitializedValue == 2;
    public ulong SelfAddress => SelfAddressValue;
    public ulong HeapAddress => HeapAddressValue;
    public ulong MappedStart => MappedStartValue;
    public ulong PhysicalStart => PhysicalStartValue;
    public ulong PhysicalEndExclusive => PhysicalEndValue;
    public ulong UsedBytes => UsedValue;
    public ulong AllocationCount => CountValue;
}
