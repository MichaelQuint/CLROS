namespace OS.Kernel.Diagnostics;

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

    public BootEventRecordRingBufferResult Record(BootEventRecordKind kind, uint phase, uint eventId, ulong arg0 = 0, ulong arg1 = 0, ulong arg2 = 0)
    {
        if (!_isInitialized)
        {
            return BootEventRecordRingBufferResult.Fail(BootEventRecordRingBufferStatus.NoStorage, 0, 1, 0, 0);
        }

        BootEventRecord record = BootEventRecord.Create(_nextSequence, kind, phase, eventId, arg0, arg1, arg2);
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
        return BootEventRecordRingBufferResult.Success(1, 1, _count, _nextSequence);
    }

    public BootEventRecordRingBufferResult Snapshot(Span<BootEventRecord> destination)
    {
        if (!_isInitialized)
        {
            return BootEventRecordRingBufferResult.Fail(BootEventRecordRingBufferStatus.NotInitialized, 0, 0, 0, 0);
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
            return BootEventRecordRingBufferResult.Success(written, _count, _count, _nextSequence);
        }

        return BootEventRecordRingBufferResult.Fail(status, written, _count, _count, _nextSequence);
    }
}
