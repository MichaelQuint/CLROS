namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Bounded Linux byte-stream pipe over retained caller-owned storage. One IRQ-off
/// owner keeps this struct and its mapped writable buffer exclusive and stable;
/// never copy a live pipe. Trusted copy buffers must be mapped for their entire
/// declared range and cannot alias the pipe or its retained buffer. No allocation,
/// firmware, concurrency, scheduling, blocking wait or signal delivery is supplied.
/// I/O returns byte counts or Linux negative errno; rejected calls change nothing.
/// Endpoint counts represent owned references, not threads or process identities.
/// </summary>
public unsafe struct LinuxPipe
{
    public const uint PipeBuf = 4096, MaximumCapacity = 65536;
    private const long BadDescriptor = -9, Again = -11, Fault = -14, BrokenPipe = -32, Unsupported = -38;
    private byte* _buffer;
    private uint _capacity, _readIndex, _writeIndex, _buffered, _readers, _writers, _initialized;

    public readonly bool Initialized => _initialized == 1;
    public readonly uint Capacity => _capacity;
    public readonly uint BufferedBytes => _buffered;
    public readonly uint ReaderCount => _readers;
    public readonly uint WriterCount => _writers;

    /// <summary>
    /// Admit a default/released owner and a power-of-two buffer from 4KiB through
    /// 64KiB. Starts with one reader and writer and clears all retained bytes only
    /// after complete admission. The caller retains both storage lifetimes until
    /// successful Release; invalid or already-owned state is unchanged.
    /// </summary>
    public bool Initialize(byte* buffer, uint capacity)
    {
        if (_buffer != null || _capacity != 0 || _readIndex != 0 || _writeIndex != 0 ||
            _buffered != 0 || _readers != 0 || _writers != 0 || _initialized != 0 ||
            !CapacityAllowed(capacity) || !Extent(buffer, capacity)) return false;
        fixed (LinuxPipe* self = &this)
            if (Overlap((ulong)buffer, capacity, (ulong)self, (uint)sizeof(LinuxPipe))) return false;
        for (uint i = 0; i < capacity; i++) buffer[i] = 0;
        _buffer = buffer; _capacity = capacity; _readers = 1; _writers = 1; _initialized = 1;
        return true;
    }

    /// <summary>
    /// Copy the available prefix without consuming it. The adapter must complete
    /// user copy-out before Consume and prevent intervening pipe mutation. A live
    /// reader's zero-length call returns zero without inspecting the destination.
    /// Positive blocking calls return ENOSYS. Nonblocking empty returns EAGAIN
    /// while writers remain, or zero after their last close; buffered data drains
    /// first. Bad state/closed reader is EBADF; bad trusted range is EFAULT.
    /// </summary>
    public long Peek(byte* trustedDestination, uint requested, bool nonBlocking)
    {
        if (!Valid() || _readers == 0) return BadDescriptor;
        if (requested == 0) return 0;
        if (!nonBlocking) return Unsupported;
        if (!TrustedRange(trustedDestination, requested)) return Fault;
        if (_buffered == 0) return _writers == 0 ? 0 : Again;
        uint bytes = requested < _buffered ? requested : _buffered;
        for (uint i = 0; i < bytes; i++)
            trustedDestination[i] = _buffer[(_readIndex + i) & (_capacity - 1)];
        return bytes;
    }

    /// <summary>
    /// Commit a successful copy-out under the same exclusive ownership as Peek.
    /// A live reader may consume at most BufferedBytes; zero succeeds. Consumed
    /// retained bytes are scrubbed. Invalid state/count or a closed reader fails
    /// without mutation; this API does not authenticate an earlier Peek receipt.
    /// </summary>
    public bool Consume(uint bytes)
    {
        if (!Valid() || _readers == 0 || bytes > _buffered) return false;
        for (uint i = 0; i < bytes; i++) _buffer[(_readIndex + i) & (_capacity - 1)] = 0;
        _readIndex = (_readIndex + bytes) & (_capacity - 1); _buffered -= bytes;
        return true;
    }

    /// <summary>
    /// Append a nonblocking write. At most PIPE_BUF bytes are atomic: inadequate
    /// space returns EAGAIN unchanged. Larger writes return the actual available
    /// prefix, or EAGAIN when full. A live writer's zero-length call returns zero
    /// without inspecting the source or reader count. Positive blocking calls
    /// return ENOSYS. With no readers, explicit ignored-SIGPIPE admission returns
    /// EPIPE; other signal policy returns ENOSYS. Bad state/closed writer is EBADF,
    /// and a bad trusted source range is EFAULT. No failed call writes any bytes.
    /// </summary>
    public long Write(byte* trustedSource, uint bytes, bool nonBlocking, bool sigpipeIgnored)
    {
        if (!Valid() || _writers == 0) return BadDescriptor;
        if (bytes == 0) return 0;
        if (!nonBlocking) return Unsupported;
        if (_readers == 0) return sigpipeIgnored ? BrokenPipe : Unsupported;
        if (!TrustedRange(trustedSource, bytes)) return Fault;
        uint available = _capacity - _buffered;
        if (available == 0 || (bytes <= PipeBuf && bytes > available)) return Again;
        uint written = bytes < available ? bytes : available;
        for (uint i = 0; i < written; i++)
            _buffer[(_writeIndex + i) & (_capacity - 1)] = trustedSource[i];
        _writeIndex = (_writeIndex + written) & (_capacity - 1); _buffered += written;
        return written;
    }

    /// <summary>Add one owned live-reader reference. Zero and exhausted counts cannot grow.</summary>
    public bool DuplicateReader()
    {
        if (!Valid() || _readers == 0 || _readers == uint.MaxValue) return false;
        _readers++; return true;
    }

    /// <summary>Add one owned live-writer reference. Zero and exhausted counts cannot grow.</summary>
    public bool DuplicateWriter()
    {
        if (!Valid() || _writers == 0 || _writers == uint.MaxValue) return false;
        _writers++; return true;
    }

    /// <summary>Drop one owned reader reference. A second close after zero fails unchanged.</summary>
    public bool CloseReader()
    {
        if (!Valid() || _readers == 0) return false;
        _readers--; return true;
    }

    /// <summary>Drop one owned writer reference. Remaining bytes survive the final close for draining.</summary>
    public bool CloseWriter()
    {
        if (!Valid() || _writers == 0) return false;
        _writers--; return true;
    }

    /// <summary>
    /// Return ownership only after both endpoint counts reach zero. Scrubs the
    /// entire retained buffer and resets this struct, permitting fresh admission.
    /// The caller may then reuse/free storage. Invalid or still-owned state fails
    /// unchanged; this method never frees caller memory.
    /// </summary>
    public bool Release()
    {
        if (!Valid() || _readers != 0 || _writers != 0) return false;
        for (uint i = 0; i < _capacity; i++) _buffer[i] = 0;
        this = default; return true;
    }

    private bool Valid()
    {
        if (_initialized != 1 || !CapacityAllowed(_capacity) || !Extent(_buffer, _capacity) ||
            _readIndex >= _capacity || _writeIndex >= _capacity || _buffered > _capacity ||
            _writeIndex != ((_readIndex + _buffered) & (_capacity - 1))) return false;
        fixed (LinuxPipe* self = &this)
            return !Overlap((ulong)_buffer, _capacity, (ulong)self, (uint)sizeof(LinuxPipe));
    }

    private bool TrustedRange(byte* buffer, uint bytes)
    {
        if (!Extent(buffer, bytes) || Overlap((ulong)buffer, bytes, (ulong)_buffer, _capacity)) return false;
        fixed (LinuxPipe* self = &this)
            return !Overlap((ulong)buffer, bytes, (ulong)self, (uint)sizeof(LinuxPipe));
    }

    private static bool CapacityAllowed(uint capacity)
        => capacity >= PipeBuf && capacity <= MaximumCapacity && (capacity & (capacity - 1)) == 0;
    private static bool Extent(byte* buffer, uint bytes)
        => buffer != null && bytes != 0 && (ulong)buffer <= ulong.MaxValue - bytes;
    private static bool Overlap(ulong a, uint an, ulong b, uint bn)
        => a < b + bn && b < a + an;
}
