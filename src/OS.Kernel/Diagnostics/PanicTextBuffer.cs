using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public ref struct PanicTextBuffer
{
    private Span<byte> _destination;
    private int _written;
    private PanicTextBufferStatus _status;
    private bool _isInitialized;
    public PanicTextBuffer(Span<byte> destination)
    {
        _destination = destination;
        _written = 0;
        _status = PanicTextBufferStatus.Ok;
        _isInitialized = true;
    }

    public bool IsInitialized => _isInitialized;
    public int Capacity => _destination.Length;
    public int Written => _written;
    public int Remaining => _destination.Length - _written;
    public PanicTextBufferStatus Status => _status;
    public bool Succeeded => _isInitialized && _status == PanicTextBufferStatus.Ok;
    public bool Overflowed => _status == PanicTextBufferStatus.Overflow;
    public bool Truncated => _status == PanicTextBufferStatus.Overflow;
    public ReadOnlySpan<byte> WrittenSpan => _destination.Slice(0, _written);

    public PanicTextBufferStatus WriteAscii(ReadOnlySpan<char> value)
    {
        if (_status != PanicTextBufferStatus.Ok)
        {
            return _status;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character > 0x7f)
            {
                _status = PanicTextBufferStatus.InvalidAscii;
                return _status;
            }

            if (!TryWriteByte((byte)character))
            {
                return _status;
            }
        }

        return _status;
    }

    public PanicTextBufferStatus WriteAscii(ReadOnlySpan<byte> value)
    {
        if (_status != PanicTextBufferStatus.Ok)
        {
            return _status;
        }

        for (int index = 0; index < value.Length; index++)
        {
            byte character = value[index];
            if (character > 0x7f)
            {
                _status = PanicTextBufferStatus.InvalidAscii;
                return _status;
            }

            if (!TryWriteByte(character))
            {
                return _status;
            }
        }

        return _status;
    }

    public PanicTextBufferStatus WriteAsciiLine(ReadOnlySpan<char> value)
    {
        WriteAscii(value);
        if (_status == PanicTextBufferStatus.Ok)
        {
            WriteCrLf();
        }

        return _status;
    }

    public PanicTextBufferStatus WriteAsciiLine(ReadOnlySpan<byte> value)
    {
        WriteAscii(value);
        if (_status == PanicTextBufferStatus.Ok)
        {
            WriteCrLf();
        }

        return _status;
    }

    public PanicTextBufferStatus WriteUnsignedDecimal(ulong value)
    {
        if (_status != PanicTextBufferStatus.Ok)
        {
            return _status;
        }

        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(_destination, _written, value);
        return ApplyTextBuilderResult(result);
    }

    public PanicTextBufferStatus WriteHex64(ulong value)
    {
        if (_status != PanicTextBufferStatus.Ok)
        {
            return _status;
        }

        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Hex(_destination, _written, value, minimumDigits: 16, includePrefix: true);
        return ApplyTextBuilderResult(result);
    }

    public PanicTextBufferStatus WriteCrLf()
    {
        if (_status != PanicTextBufferStatus.Ok)
        {
            return _status;
        }

        if (!TryWriteByte((byte)'\r'))
        {
            return _status;
        }

        TryWriteByte((byte)'\n');
        return _status;
    }

    public PanicTextBufferStatus WriteFaultSummary(ReadOnlySpan<char> subsystem, ReadOnlySpan<char> reason, in CpuFaultFrame frame)
    {
        WriteAscii(SerialSmokeMarkers.OsSmokeFail);
        WriteTextField("subsystem", subsystem);
        WriteTextField("reason", reason);
        WriteDecimalField("vector", (ulong)frame.Vector);
        WriteBooleanField("errorCodePresent", frame.HasErrorCode);
        WriteHexField("errorCode", frame.ErrorCode);
        WriteHexField("rip", frame.Rip);
        WriteHexField("rsp", frame.Rsp);
        WriteHexField("rbp", frame.Rbp);
        WriteHexField("rflags", frame.Rflags);
        WriteHexField("cr2", frame.Cr2);
        WriteHexField("cr3", frame.Cr3);
        WriteHexField("rax", frame.Rax);
        WriteHexField("rbx", frame.Rbx);
        WriteHexField("rcx", frame.Rcx);
        WriteHexField("rdx", frame.Rdx);
        WriteHexField("rsi", frame.Rsi);
        WriteHexField("rdi", frame.Rdi);
        WriteHexField("r8", frame.R8);
        WriteHexField("r9", frame.R9);
        WriteHexField("r10", frame.R10);
        WriteHexField("r11", frame.R11);
        WriteHexField("r12", frame.R12);
        WriteHexField("r13", frame.R13);
        WriteHexField("r14", frame.R14);
        WriteHexField("r15", frame.R15);
        WriteCrLf();
        return _status;
    }

    private PanicTextBufferStatus WriteTextField(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        if (!ShouldWriteTextField(value))
        {
            return _status;
        }

        WriteFieldPrefix(name);
        WriteAscii(value);
        return _status;
    }

    private PanicTextBufferStatus WriteBooleanField(ReadOnlySpan<char> name, bool value)
    {
        WriteFieldPrefix(name);
        WriteAscii(value ? "true" : "false");
        return _status;
    }

    private PanicTextBufferStatus WriteDecimalField(ReadOnlySpan<char> name, ulong value)
    {
        WriteFieldPrefix(name);
        WriteUnsignedDecimal(value);
        return _status;
    }

    private PanicTextBufferStatus WriteHexField(ReadOnlySpan<char> name, ulong value)
    {
        WriteFieldPrefix(name);
        WriteHex64(value);
        return _status;
    }

    private PanicTextBufferStatus WriteFieldPrefix(ReadOnlySpan<char> name)
    {
        WriteAscii(" ");
        WriteAscii(name);
        WriteAscii("=");
        return _status;
    }

    private bool TryWriteByte(byte value)
    {
        if (_written >= _destination.Length)
        {
            _status = PanicTextBufferStatus.Overflow;
            return false;
        }

        _destination[_written] = value;
        _written++;
        return true;
    }

    private static bool ShouldWriteTextField(ReadOnlySpan<char> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (!OS.Std.Ascii.IsWhiteSpace(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private PanicTextBufferStatus ApplyTextBuilderResult(AsciiTextBuilderResult result)
    {
        if (result.Succeeded)
        {
            _written = result.NextOffset;
            return _status;
        }

        _status = PanicTextBufferStatus.Overflow;
        return _status;
    }
}
