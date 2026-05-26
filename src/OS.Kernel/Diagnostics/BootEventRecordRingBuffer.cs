namespace OS.Kernel.Diagnostics;

public enum BootEventRecordKind
{
    Marker = 0,
    Phase = 1,
    Diagnostic = 2,
    Panic = 3
}

public enum BootEventRecordRingBufferStatus
{
    Success = 0,
    NotInitialized = 1,
    NoStorage = 2,
    DestinationTooSmall = 3
}

public readonly struct BootEventRecord
{
    private BootEventRecord(
        bool isInitialized,
        ulong sequence,
        BootEventRecordKind kind,
        uint phase,
        uint eventId,
        ulong arg0,
        ulong arg1,
        ulong arg2)
    {
        IsInitialized = isInitialized;
        Sequence = sequence;
        Kind = kind;
        Phase = phase;
        EventId = eventId;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
    }

    public bool IsInitialized { get; }

    public ulong Sequence { get; }

    public BootEventRecordKind Kind { get; }

    public uint Phase { get; }

    public uint EventId { get; }

    public ulong Arg0 { get; }

    public ulong Arg1 { get; }

    public ulong Arg2 { get; }

    public static BootEventRecord Create(
        ulong sequence,
        BootEventRecordKind kind,
        uint phase,
        uint eventId,
        ulong arg0,
        ulong arg1,
        ulong arg2)
    {
        return new BootEventRecord(
            true,
            sequence,
            kind,
            phase,
            eventId,
            arg0,
            arg1,
            arg2);
    }
}

public readonly struct BootEventRecordRingBufferResult
{
    private BootEventRecordRingBufferResult(
        bool isInitialized,
        BootEventRecordRingBufferStatus status,
        int written,
        int required,
        int count,
        ulong nextSequence)
    {
        IsInitialized = isInitialized;
        Status = status;
        Written = written;
        Required = required;
        Count = count;
        NextSequence = nextSequence;
    }

    public bool IsInitialized { get; }

    public BootEventRecordRingBufferStatus Status { get; }

    public int Written { get; }

    public int Required { get; }

    public int Count { get; }

    public ulong NextSequence { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == BootEventRecordRingBufferStatus.Success;

    public static BootEventRecordRingBufferResult Success(
        int written,
        int required,
        int count,
        ulong nextSequence)
    {
        return new BootEventRecordRingBufferResult(
            true,
            BootEventRecordRingBufferStatus.Success,
            written,
            required,
            count,
            nextSequence);
    }

    public static BootEventRecordRingBufferResult Fail(
        BootEventRecordRingBufferStatus status,
        int written,
        int required,
        int count,
        ulong nextSequence)
    {
        return new BootEventRecordRingBufferResult(
            true,
            status,
            written,
            required,
            count,
            nextSequence);
    }
}

public ref struct BootEventRecordRingBuffer
{
    private Span<BootEventRecord> _storage;
    private int _next;
    private int _count;
    private ulong _nextSequence;
    private bool _isInitialized;

    public BootEventRecordRingBuffer(Span<BootEventRecord> storage)
    {
        _storage = storage;
        _next = 0;
        _count = 0;
        _nextSequence = 1;
        _isInitialized = storage.Length > 0;
    }

    public bool IsInitialized => _isInitialized;

    public int Capacity => _storage.Length;

    public int Count => _count;

    public ulong NextSequence => _nextSequence;

    public BootEventRecordRingBufferResult Record(
        BootEventRecordKind kind,
        uint phase,
        uint eventId,
        ulong arg0 = 0,
        ulong arg1 = 0,
        ulong arg2 = 0)
    {
        if (!_isInitialized)
        {
            return BootEventRecordRingBufferResult.Fail(
                BootEventRecordRingBufferStatus.NoStorage,
                0,
                1,
                0,
                0);
        }

        BootEventRecord record = BootEventRecord.Create(
            _nextSequence,
            kind,
            phase,
            eventId,
            arg0,
            arg1,
            arg2);

        _storage[_next] = record;
        _next++;
        if (_next == _storage.Length)
        {
            _next = 0;
        }

        if (_count < _storage.Length)
        {
            _count++;
        }

        _nextSequence++;
        return BootEventRecordRingBufferResult.Success(
            1,
            1,
            _count,
            _nextSequence);
    }

    public BootEventRecordRingBufferResult Snapshot(Span<BootEventRecord> destination)
    {
        if (!_isInitialized)
        {
            return BootEventRecordRingBufferResult.Fail(
                BootEventRecordRingBufferStatus.NotInitialized,
                0,
                0,
                0,
                0);
        }

        int written = _count;
        BootEventRecordRingBufferStatus status = BootEventRecordRingBufferStatus.Success;
        int skip = 0;

        if (destination.Length < _count)
        {
            written = destination.Length;
            skip = _count - destination.Length;
            status = BootEventRecordRingBufferStatus.DestinationTooSmall;
        }

        int start = _next - _count;
        if (start < 0)
        {
            start += _storage.Length;
        }

        for (int index = 0; index < written; index++)
        {
            int sourceIndex = start + skip + index;
            if (sourceIndex >= _storage.Length)
            {
                sourceIndex -= _storage.Length;
            }

            destination[index] = _storage[sourceIndex];
        }

        if (status == BootEventRecordRingBufferStatus.Success)
        {
            return BootEventRecordRingBufferResult.Success(
                written,
                _count,
                _count,
                _nextSequence);
        }

        return BootEventRecordRingBufferResult.Fail(
            status,
            written,
            _count,
            _count,
            _nextSequence);
    }
}
