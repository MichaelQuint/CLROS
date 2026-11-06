namespace OS.Kernel.Memory;
/// <summary>
/// Caller-owned raw observations written by the audited stub. The completion
/// signature is written last. Reserved bytes must remain zero.
/// </summary>

public readonly struct X64PagingModePreflightObservation
{
    public const ulong StructureSize = 56;
    public const uint CompletionSignature = 0x31464c50;
    private readonly ulong _cr0;
    private readonly ulong _rawCr3;
    private readonly ulong _rawCr4;
    private readonly ulong _ia32Efer;
    private readonly uint _cpuidMaximumExtendedLeaf;
    private readonly uint _cpuidAddressSizesEax;
    private readonly ulong _rflags;
    private readonly uint _completionSignature;
    private readonly uint _reserved;
    private X64PagingModePreflightObservation(ulong cr0, ulong rawCr3, ulong rawCr4, ulong ia32Efer, uint cpuidMaximumExtendedLeaf, uint cpuidAddressSizesEax, ulong rflags)
    {
        _cr0 = cr0;
        _rawCr3 = rawCr3;
        _rawCr4 = rawCr4;
        _ia32Efer = ia32Efer;
        _cpuidMaximumExtendedLeaf = cpuidMaximumExtendedLeaf;
        _cpuidAddressSizesEax = cpuidAddressSizesEax;
        _rflags = rflags;
        _completionSignature = CompletionSignature;
        _reserved = 0;
    }

    public bool IsInitialized => _completionSignature == CompletionSignature && _reserved == 0;
    public ulong Cr0 => _cr0;
    public ulong RawCr3 => _rawCr3;
    public ulong RawCr4 => _rawCr4;
    public ulong Ia32Efer => _ia32Efer;
    public uint CpuidMaximumExtendedLeaf => _cpuidMaximumExtendedLeaf;
    public uint CpuidAddressSizesEax => _cpuidAddressSizesEax;
    public byte PhysicalAddressWidth => (byte)_cpuidAddressSizesEax;
    public ulong Rflags => _rflags;

    public static X64PagingModePreflightObservation CreateObserved(ulong cr0, ulong rawCr3, ulong rawCr4, ulong ia32Efer, uint cpuidMaximumExtendedLeaf, uint cpuidAddressSizesEax, ulong rflags)
    {
        return new X64PagingModePreflightObservation(cr0, rawCr3, rawCr4, ia32Efer, cpuidMaximumExtendedLeaf, cpuidAddressSizesEax, rflags);
    }
}
