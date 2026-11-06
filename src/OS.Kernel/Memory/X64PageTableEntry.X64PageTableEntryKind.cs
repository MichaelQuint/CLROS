namespace OS.Kernel.Memory;
/// <summary>
/// Distinguishes an absent entry, an upper-level table pointer, and a leaf
/// mapping. A table pointer is not itself a leaf memory mapping.
/// </summary>

public enum X64PageTableEntryKind : byte
{
    Uninitialized = 0,
    Unmapped = 1,
    TablePointer = 2,
    Leaf = 3
}
