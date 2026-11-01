namespace OS.Kernel.Memory;
/// <summary>
/// One exact event in the modeled future execution transcript.
/// </summary>

public readonly struct X64ProtectionProbeEvent
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeEventKind _kind;
    private readonly ulong _ordinal;
    private readonly ulong _reserved;
    public X64ProtectionProbeEvent(X64ProtectionProbeEventKind kind, ulong ordinal)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _kind = kind;
        _ordinal = ordinal;
        _reserved = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public X64ProtectionProbeEventKind Kind => _kind;
    public ulong Ordinal => _ordinal;

    public X64ProtectionProbeStatus Validate()
    {
        if (!IsInitialized || _kind < X64ProtectionProbeEventKind.StubMaterialized || _kind > X64ProtectionProbeEventKind.ConstantReturnAndCanaryObserved)
        {
            return X64ProtectionProbeStatus.InvalidEvent;
        }

        return X64ProtectionProbeStatus.Success;
    }
}
