namespace OS.Kernel.Memory;
/// <summary>
/// Caller-owned request for evaluation of one observation. The root and arena
/// are physical-address facts supplied by the future integration layer; this
/// host-audited leaf does not discover or allocate either range.
/// </summary>

public readonly struct X64PagingModePreflightRequest
{
    public const ulong StructureSize = 32;
    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly ulong _tableArenaByteLength;
    private readonly byte _isInitialized;
    private readonly byte _probeEnabled;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private X64PagingModePreflightRequest(bool enabled, OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, ulong tableArenaByteLength)
    {
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaByteLength = tableArenaByteLength;
        _isInitialized = 1;
        _probeEnabled = enabled ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _probeEnabled <= 1 && _reserved0 == 0 && _reserved1 == 0;
    public bool ProbeEnabled => _probeEnabled == 1;
    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;
    public OS.Boot.PhysicalAddress TableArenaStart => _tableArenaStart;
    public ulong TableArenaByteLength => _tableArenaByteLength;

    public static X64PagingModePreflightRequest CreateEnabled(OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, ulong tableArenaByteLength)
    {
        return new X64PagingModePreflightRequest(enabled: true, ownedRoot, tableArenaStart, tableArenaByteLength);
    }

    public static X64PagingModePreflightRequest CreateDisabled(OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, ulong tableArenaByteLength)
    {
        return new X64PagingModePreflightRequest(enabled: false, ownedRoot, tableArenaStart, tableArenaByteLength);
    }
}
