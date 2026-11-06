namespace OS.Kernel.Memory;
/// <summary>
/// Identifies the single canonical four-level x64 page-table level token.
/// Numeric values intentionally match the walk depth from a leaf PTE.
/// </summary>

public enum X64PageTableLevel : byte
{
    Uninitialized = 0,
    Pte = 1,
    Pde = 2,
    Pdpte = 3,
    Pml4e = 4
}
