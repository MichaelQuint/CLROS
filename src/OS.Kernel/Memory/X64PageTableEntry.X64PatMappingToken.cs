namespace OS.Kernel.Memory;
/// <summary>
/// Binds the three CLROS cache policies to explicitly selected entries in one
/// observed IA32_PAT value. The token is caller-supplied value evidence only;
/// it does not read the MSR and does not prove that the value is live.
/// </summary>

public readonly struct X64PatMappingToken
{
    public const ulong StructureSize = 16;
    public const byte UnsupportedIndex = 0xff;
    private const byte UncachedMemoryType = 0;
    private const byte WriteCombiningMemoryType = 1;
    private const byte WriteBackMemoryType = 6;
    private const byte UncacheableMinusMemoryType = 7;
    private const byte WriteThroughMemoryType = 4;
    private const byte WriteProtectedMemoryType = 5;
    private const byte PatEntryReservedMask = 0xf8;
    private readonly ulong _rawIa32Pat;
    private readonly byte _isInitialized;
    private readonly byte _writeBackIndex;
    private readonly byte _uncachedIndex;
    private readonly byte _writeCombiningIndex;
    private readonly uint _reserved;
    private X64PatMappingToken(ulong rawIa32Pat, byte writeBackIndex, byte uncachedIndex, byte writeCombiningIndex)
    {
        _rawIa32Pat = rawIa32Pat;
        _isInitialized = 1;
        _writeBackIndex = writeBackIndex;
        _uncachedIndex = uncachedIndex;
        _writeCombiningIndex = writeCombiningIndex;
        _reserved = 0;
    }

    public bool IsInitialized => _isInitialized == 1;
    public ulong RawIa32Pat => _rawIa32Pat;
    public byte WriteBackIndex => _writeBackIndex;
    public byte UncachedIndex => _uncachedIndex;
    public byte WriteCombiningIndex => _writeCombiningIndex;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.PatMappingNotInitialized;
        }

        if (_isInitialized != 1)
        {
            return X64PageTableEntryStatus.InvalidPatMappingState;
        }

        if (_reserved != 0)
        {
            return X64PageTableEntryStatus.InvalidPatMappingState;
        }

        for (byte index = 0; index < 8; index++)
        {
            byte entry = ReadEntry(_rawIa32Pat, index);
            if ((entry & PatEntryReservedMask) != 0 || !IsArchitecturalMemoryType(entry))
            {
                return X64PageTableEntryStatus.InvalidPatEntry;
            }
        }

        X64PageTableEntryStatus status = ValidateSelectedIndex(_rawIa32Pat, _writeBackIndex, WriteBackMemoryType, allowUnsupported: false);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(_rawIa32Pat, _uncachedIndex, UncachedMemoryType, allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        return ValidateSelectedIndex(_rawIa32Pat, _writeCombiningIndex, WriteCombiningMemoryType, allowUnsupported: true);
    }

    public static X64PageTableEntryStatus TryCreate(ulong rawIa32Pat, byte writeBackIndex, byte uncachedIndex, byte writeCombiningIndex, out X64PatMappingToken token)
    {
        token = default;
        for (byte index = 0; index < 8; index++)
        {
            byte entry = ReadEntry(rawIa32Pat, index);
            if ((entry & PatEntryReservedMask) != 0 || !IsArchitecturalMemoryType(entry))
            {
                return X64PageTableEntryStatus.InvalidPatEntry;
            }
        }

        X64PageTableEntryStatus status = ValidateSelectedIndex(rawIa32Pat, writeBackIndex, WriteBackMemoryType, allowUnsupported: false);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(rawIa32Pat, uncachedIndex, UncachedMemoryType, allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(rawIa32Pat, writeCombiningIndex, WriteCombiningMemoryType, allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        token = new X64PatMappingToken(rawIa32Pat, writeBackIndex, uncachedIndex, writeCombiningIndex);
        return X64PageTableEntryStatus.Success;
    }

    public X64PageTableEntryStatus TryGetIndex(PageCachePolicy policy, out byte index)
    {
        index = UnsupportedIndex;
        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (policy == PageCachePolicy.WriteBack)
        {
            index = _writeBackIndex;
            return X64PageTableEntryStatus.Success;
        }

        if (policy == PageCachePolicy.Uncached)
        {
            if (_uncachedIndex == UnsupportedIndex)
            {
                return X64PageTableEntryStatus.CachePolicyNotSupported;
            }

            index = _uncachedIndex;
            return X64PageTableEntryStatus.Success;
        }

        if (policy == PageCachePolicy.WriteCombining)
        {
            if (_writeCombiningIndex == UnsupportedIndex)
            {
                return X64PageTableEntryStatus.CachePolicyNotSupported;
            }

            index = _writeCombiningIndex;
            return X64PageTableEntryStatus.Success;
        }

        return X64PageTableEntryStatus.InvalidCachePolicy;
    }

    public X64PageTableEntryStatus TryGetPolicy(byte index, out PageCachePolicy policy)
    {
        policy = PageCachePolicy.Uninitialized;
        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        if (index == _writeBackIndex)
        {
            policy = PageCachePolicy.WriteBack;
            return X64PageTableEntryStatus.Success;
        }

        if (_uncachedIndex != UnsupportedIndex && index == _uncachedIndex)
        {
            policy = PageCachePolicy.Uncached;
            return X64PageTableEntryStatus.Success;
        }

        if (_writeCombiningIndex != UnsupportedIndex && index == _writeCombiningIndex)
        {
            policy = PageCachePolicy.WriteCombining;
            return X64PageTableEntryStatus.Success;
        }

        return X64PageTableEntryStatus.CachePolicyNotSupported;
    }

    public X64PageTableEntryStatus TryValidateMemoryTypeAtIndex(byte index, PageCachePolicy policy)
    {
        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        byte expectedMemoryType;
        if (policy == PageCachePolicy.WriteBack)
        {
            expectedMemoryType = WriteBackMemoryType;
        }
        else if (policy == PageCachePolicy.Uncached)
        {
            expectedMemoryType = UncachedMemoryType;
        }
        else if (policy == PageCachePolicy.WriteCombining)
        {
            expectedMemoryType = WriteCombiningMemoryType;
        }
        else
        {
            return X64PageTableEntryStatus.InvalidCachePolicy;
        }

        return ReadEntry(_rawIa32Pat, index) == expectedMemoryType ? X64PageTableEntryStatus.Success : X64PageTableEntryStatus.PatMemoryTypeMismatch;
    }

    private static X64PageTableEntryStatus ValidateSelectedIndex(ulong rawIa32Pat, byte index, byte expectedMemoryType, bool allowUnsupported)
    {
        if (index == UnsupportedIndex)
        {
            return allowUnsupported ? X64PageTableEntryStatus.Success : X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        return ReadEntry(rawIa32Pat, index) == expectedMemoryType ? X64PageTableEntryStatus.Success : X64PageTableEntryStatus.PatMemoryTypeMismatch;
    }

    private static byte ReadEntry(ulong rawIa32Pat, byte index)
    {
        return (byte)(rawIa32Pat >> (index * 8));
    }

    private static bool IsArchitecturalMemoryType(byte memoryType)
    {
        return memoryType == UncachedMemoryType || memoryType == WriteCombiningMemoryType || memoryType == WriteThroughMemoryType || memoryType == WriteProtectedMemoryType || memoryType == WriteBackMemoryType || memoryType == UncacheableMinusMemoryType;
    }
}
