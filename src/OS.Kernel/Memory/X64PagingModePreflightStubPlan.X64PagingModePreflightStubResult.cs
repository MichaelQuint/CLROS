namespace OS.Kernel.Memory;
/// <summary>
/// Result of one exact byte materialization or exact byte readback.
/// </summary>

public readonly struct X64PagingModePreflightStubResult
{
    public const ulong StructureSize = 56;
    public const ulong NoMismatchOffset = ~0UL;
    private readonly ulong _declaredByteCount;
    private readonly ulong _bufferCapacity;
    private readonly ulong _entryOffset;
    private readonly ulong _entryAddress;
    private readonly ulong _bytesProcessed;
    private readonly ulong _mismatchOffset;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightStatus _status;
    private readonly X64PagingModePreflightOperation _operation;
    private readonly byte _exactBytes;
    private readonly uint _reserved;
    internal X64PagingModePreflightStubResult(X64PagingModePreflightStatus status, X64PagingModePreflightOperation operation, ulong declaredByteCount, ulong bufferCapacity, ulong entryOffset, ulong entryAddress, ulong bytesProcessed, ulong mismatchOffset, bool exactBytes)
    {
        _declaredByteCount = declaredByteCount;
        _bufferCapacity = bufferCapacity;
        _entryOffset = entryOffset;
        _entryAddress = entryAddress;
        _bytesProcessed = bytesProcessed;
        _mismatchOffset = mismatchOffset;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _exactBytes = exactBytes ? (byte)1 : (byte)0;
        _reserved = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _status >= X64PagingModePreflightStatus.Success && _status <= X64PagingModePreflightStatus.OwnedRootOutsidePhysicalWidth && _operation >= X64PagingModePreflightOperation.Materialize && _operation <= X64PagingModePreflightOperation.Validate && _exactBytes <= 1 && _reserved == 0;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized || _status != X64PagingModePreflightStatus.Success || _exactBytes != 1 || _declaredByteCount != X64PagingModePreflightStubPlan.RequiredByteLength || _bufferCapacity != X64PagingModePreflightStubPlan.StubPageByteLength || _entryOffset >= _bufferCapacity || (_entryOffset & (X64PagingModePreflightStubPlan.RequiredEntryAlignment - 1)) != 0 || _entryAddress == 0 || (_entryAddress & (X64PagingModePreflightStubPlan.RequiredEntryAlignment - 1)) != 0 || _entryAddress < _entryOffset || _bytesProcessed != X64PagingModePreflightStubPlan.RequiredByteLength || _mismatchOffset != NoMismatchOffset)
            {
                return false;
            }

            ulong bufferAddress = _entryAddress - _entryOffset;
            if (bufferAddress == 0 || (bufferAddress & (X64PagingModePreflightStubPlan.RequiredEntryAlignment - 1)) != 0 || bufferAddress > ~0UL - _bufferCapacity)
            {
                return false;
            }

            ulong bufferEnd = bufferAddress + _bufferCapacity;
            if (_entryAddress > ~0UL - X64PagingModePreflightStubPlan.RequiredByteLength)
            {
                return false;
            }

            ulong entryEnd = _entryAddress + X64PagingModePreflightStubPlan.RequiredByteLength;
            return entryEnd <= bufferEnd;
        }
    }

    public X64PagingModePreflightStatus Status => _status;
    public X64PagingModePreflightOperation Operation => _operation;
    public ulong DeclaredByteCount => _declaredByteCount;
    public ulong BufferCapacity => _bufferCapacity;
    public ulong EntryOffset => _entryOffset;
    public ulong EntryAddress => _entryAddress;
    public ulong BytesProcessed => _bytesProcessed;
    public ulong MismatchOffset => _mismatchOffset;
    public bool ExactBytes => _exactBytes == 1;
}
