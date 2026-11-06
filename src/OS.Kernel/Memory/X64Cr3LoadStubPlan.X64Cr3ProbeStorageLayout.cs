namespace OS.Kernel.Memory;
/// <summary>
/// Binds the caller-owned state and canary bytes inside the final LoaderData
/// state page. The values describe ownership only; they do not map memory.
/// </summary>

public readonly struct X64Cr3ProbeStorageLayout
{
    public const ulong StructureSize = 56;
    public const ulong RequiredStateRecordByteLength = X64ReversibleCr3TransitionRuntimeState.StructureSize;
    public const ulong RequiredCanaryByteLength = 8;
    public const ulong RequiredAlignment = 8;
    private readonly OS.Boot.VirtualAddress _stateAddress;
    private readonly ulong _stateByteLength;
    private readonly OS.Boot.VirtualAddress _canaryAddress;
    private readonly ulong _canaryByteLength;
    private readonly ulong _initialCanaryValue;
    private readonly ulong _updatedCanaryValue;
    private readonly byte _isInitialized;
    private readonly X64Cr3ProbeStorageProvenance _provenance;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    public X64Cr3ProbeStorageLayout(OS.Boot.VirtualAddress stateAddress, ulong stateByteLength, OS.Boot.VirtualAddress canaryAddress, ulong canaryByteLength, ulong initialCanaryValue, ulong updatedCanaryValue, X64Cr3ProbeStorageProvenance provenance)
    {
        _stateAddress = stateAddress;
        _stateByteLength = stateByteLength;
        _canaryAddress = canaryAddress;
        _canaryByteLength = canaryByteLength;
        _initialCanaryValue = initialCanaryValue;
        _updatedCanaryValue = updatedCanaryValue;
        _isInitialized = 1;
        _provenance = provenance;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public OS.Boot.VirtualAddress StateAddress => _stateAddress;
    public ulong StateByteLength => _stateByteLength;
    public OS.Boot.VirtualAddress CanaryAddress => _canaryAddress;
    public ulong CanaryByteLength => _canaryByteLength;
    public ulong InitialCanaryValue => _initialCanaryValue;
    public ulong UpdatedCanaryValue => _updatedCanaryValue;
    public X64Cr3ProbeStorageProvenance Provenance => _provenance;
}
