namespace OS.Kernel.Platform;
/// <summary>Original CPUID observations and ordered RDTSC on the same bootstrap processor.</summary>

public unsafe struct OwnedTscAccess
{
    public delegate* unmanaged<ulong> Read;
    public bool IntelVendor;
    public uint MaximumBasicLeaf, MaximumExtendedLeaf, Leaf1Edx, Leaf80000007Edx;
    public uint Denominator, Numerator, CrystalHz;
}
