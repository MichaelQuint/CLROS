namespace OS.Kernel.Memory;
/// <summary>
/// Raw mapping-boundary input used specifically because the typed
/// VirtualAddress contract intentionally cannot represent address zero.
/// Construction records hostile inputs; validation classifies them.
/// </summary>

public readonly struct PageZeroMappingIntent
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _present;
    private readonly ulong _rawVirtualStart;
    private readonly ulong _byteLength;
    private PageZeroMappingIntent(ulong rawVirtualStart, ulong byteLength, bool present)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _present = present ? (byte)1 : (byte)0;
        _rawVirtualStart = rawVirtualStart;
        _byteLength = byteLength;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _present <= 1;
    public bool IsPresentMapping => _present == 1;
    public ulong RawVirtualStart => _rawVirtualStart;
    public ulong ByteLength => _byteLength;
    internal bool IsCanonicalDefault => _structureSize == 0 && _structureVersion == 0 && _isInitialized == 0 && _present == 0 && _rawVirtualStart == 0 && _byteLength == 0;

    public static PageZeroMappingIntent CreateAtRawMappingBoundary(ulong rawVirtualStart, ulong byteLength, bool present)
    {
        return new PageZeroMappingIntent(rawVirtualStart, byteLength, present);
    }

    internal PageZeroMappingPolicyStatus Validate()
    {
        if (_structureSize != StructureSize || _structureVersion != StructureVersion || _isInitialized != 1)
        {
            return PageZeroMappingPolicyStatus.InvalidIntentState;
        }

        if (_present > 1)
        {
            return PageZeroMappingPolicyStatus.InvalidPresentFlag;
        }

        if (_byteLength == 0)
        {
            return PageZeroMappingPolicyStatus.ZeroLength;
        }

        if (_rawVirtualStart > ~0UL - _byteLength)
        {
            return PageZeroMappingPolicyStatus.RangeWrapsThroughPageZero;
        }

        if (_present == 1 && _rawVirtualStart < PageZeroMappingPolicy.PageSize)
        {
            return PageZeroMappingPolicyStatus.PresentRangeCoversPageZero;
        }

        if ((_rawVirtualStart & (PageZeroMappingPolicy.PageSize - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedRangeStart;
        }

        if ((_byteLength & (PageZeroMappingPolicy.PageSize - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedByteLength;
        }

        ulong rawEndExclusive = _rawVirtualStart + _byteLength;
        if (_rawVirtualStart >= PageZeroMappingPolicy.LowCanonicalEndExclusive || rawEndExclusive > PageZeroMappingPolicy.LowCanonicalEndExclusive)
        {
            return PageZeroMappingPolicyStatus.NonCanonicalRange;
        }

        return PageZeroMappingPolicyStatus.Success;
    }
}
