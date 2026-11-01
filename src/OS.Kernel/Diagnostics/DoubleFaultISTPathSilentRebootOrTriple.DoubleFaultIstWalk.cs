namespace OS.Kernel.Diagnostics;
/// <summary>Raw supplied walk, at most four reads. It does not authorize dereferencing any table.</summary>

public readonly struct DoubleFaultIstWalk
{
    public DoubleFaultIstWalk(ulong linear, ulong levelsRead, ulong pml4e, ulong pdpte, ulong pde, ulong pte)
    {
        Linear = linear;
        LevelsRead = levelsRead;
        Pml4e = pml4e;
        Pdpte = pdpte;
        Pde = pde;
        Pte = pte;
    }

    public ulong Linear { get; }
    public ulong LevelsRead { get; }
    public ulong Pml4e { get; }
    public ulong Pdpte { get; }
    public ulong Pde { get; }
    public ulong Pte { get; }

    internal ulong Entry(int level) => level == 0 ? Pml4e : level == 1 ? Pdpte : level == 2 ? Pde : Pte;
}
