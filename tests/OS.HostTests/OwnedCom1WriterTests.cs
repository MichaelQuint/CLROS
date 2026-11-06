using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;

// Host-only UART model. No hardware I/O or guest qualification is implied.
internal static unsafe partial class OwnedCom1WriterTests
{
    private static readonly byte[] Captured = new byte[4096];
    private static byte _identification;
    private static byte _lineControl;
    private static byte _interrupts;
    private static int _capacity;
    private static int _queued;
    private static int _ticksPerCharacter;
    private static int _ticks;
    private static int _stallAfterBytes;
    private static int _written;
    private static int _polls;
    private static int _identificationReads;
    private static int _reads;
    private static bool _invalidIo;
    private static bool _drainOnPoll;

    [HostTest("serial.hex all byte values preserve exact lowercase hex and input")]
    private static bool AllByteValues()
    {
        Reset(0xc1, 16);
        byte[] source = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        byte[] original = source.ToArray();
        return Emit(source) && source.AsSpan().SequenceEqual(original) && Exact(source) &&
            _polls <= source.Length * 2 * 128 && _queued == 0 && _identificationReads == 1;
    }

    [HostTest("serial.hex short tails and FIFO boundaries drain without overflow")]
    private static bool LengthBoundaries()
    {
        foreach (int length in Enumerable.Range(1, 33).Concat([511, 512, 513]))
        {
            Reset(0xc1, 16);
            byte[] source = Enumerable.Range(0, length).Select(i => (byte)(i * 37)).ToArray();
            if (!Emit(source) || !Exact(source) || _queued != 0 || _polls > source.Length * 2 * 128)
                return false;
        }

        return true;
    }

    [HostTest("serial.hex no FIFO uses checked per-byte readiness")]
    private static bool NoFifo() => PerByte(0x01);
    [HostTest("serial.hex broken FIFO uses checked per-byte readiness")]
    private static bool BrokenFifo() => PerByte(0x81);

    [HostTest("serial.hex unknown FIFO identifications use checked per-byte readiness")]
    private static bool UnknownFifo()
    {
        return PerByte(0x41) && PerByte(0xe1);
    }

    private static bool PerByte(byte identification)
    {
        Reset(identification, 1);
        byte[] source = [0x00, 0x01, 0x7f, 0x80, 0xff];
        return Emit(source) && Exact(source) && _polls >= source.Length * 2 && _identificationReads == 1;
    }

    [HostTest("serial.hex enabled UART interrupts avoid IIR acknowledgement")]
    private static bool EnabledInterrupts()
    {
        Reset(0xc1, 1);
        _interrupts = 2;
        byte[] source = [0xab, 0xcd];
        return Emit(source) && Exact(source) && _identificationReads == 0;
    }

    [HostTest("serial.hex DLAB rejects before divisor writes")]
    private static bool RejectDivisorMode()
    {
        Reset(0xc1, 16);
        _lineControl = 0x83;
        return !Emit([0xab]) && _written == 0 && _reads == 1 && !_invalidIo;
    }

    [HostTest("serial.hex null, empty and unready inputs retain fail-closed behavior")]
    private static bool InvalidInputs()
    {
        Reset(0xc1, 16);
        byte value = 42;
        return OwnedCom1HexWriter.Write(default, null, 0) &&
            !OwnedCom1HexWriter.Write(default, &value, 1) &&
            !OwnedCom1HexWriter.Write(CreateBackend(), null, 1) && _reads == 0 && _written == 0;
    }

    [HostTest("serial.hex initial readiness timeout remains 128 polls")]
    private static bool InitialTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 0;
        return !Emit([0xab]) && _polls == 128 && _written == 0 && !_invalidIo;
    }

    [HostTest("serial.hex interrupted output retains only the exact written prefix")]
    private static bool PrefixTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 16;
        byte[] source = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        return !Emit(source) && _written == 16 && _polls == 2049 && !_invalidIo &&
            Encoding.ASCII.GetString(Captured, 0, _written) == Convert.ToHexString(source.AsSpan(0, 8)).ToLowerInvariant();
    }

    [HostTest("serial.hex final FIFO drain failure invalidates output")]
    private static bool FinalDrainTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 2;
        return !Emit([0xab]) && _written == 2 && _polls == 256 && !_invalidIo &&
            Captured[0] == 'a' && Captured[1] == 'b';
    }

    [HostTest("serial.hex fallback timeout preserves an exact partial prefix")]
    private static bool FallbackPrefixTimeout()
    {
        Reset(0x81, 1);
        _stallAfterBytes = 1;
        return !Emit([0xab, 0xcd]) && _written == 1 && _polls == 129 &&
            !_invalidIo && Captured[0] == 'a' && _identificationReads == 1;
    }

    [HostTest("serial.hex slow FIFO drain uses byte-normalized bounded readiness")]
    private static bool SlowDrain()
    {
        Reset(0xc1, 16);
        _ticksPerCharacter = 16;
        byte[] source = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        return Emit(source) && Exact(source) && _queued == 0 && _polls > 128 && _polls <= source.Length * 2 * 128;
    }

    [HostTest("serial.hex calls carry no FIFO credits across ownership boundaries")]
    private static bool SeparateCalls()
    {
        Reset(0xc1, 16);
        if (!Emit([0xab]) || _queued != 0)
            return false;
        _queued = 16;
        _ticks = 1;
        return Emit([0xcd]) && _queued == 0 && !_invalidIo && _written == 4 &&
            Encoding.ASCII.GetString(Captured, 0, _written) == "abcd" && _identificationReads == 2;
    }

    private static bool Emit(byte[] source)
    {
        fixed (byte* pointer = source)
            return OwnedCom1HexWriter.Write(CreateBackend(), pointer, (uint)source.Length);
    }

    private static bool Exact(byte[] source) => !_invalidIo &&
        Encoding.ASCII.GetString(Captured, 0, _written) == Convert.ToHexString(source).ToLowerInvariant();

    private static void Reset(byte identification, int capacity)
    {
        Array.Clear(Captured);
        _identification = identification;
        _lineControl = 3;
        _interrupts = 0;
        _capacity = capacity;
        _queued = 0;
        _ticksPerCharacter = 1;
        _ticks = 1;
        _stallAfterBytes = int.MaxValue;
        _written = 0;
        _polls = 0;
        _identificationReads = 0;
        _reads = 0;
        _invalidIo = false;
        _drainOnPoll = false;
    }

    private static Com1OwnedPortIoBackend CreateBackend()
    {
        nuint output = (nuint)(delegate* unmanaged<ushort, byte, void>)&WritePort;
        nuint input = (nuint)(delegate* unmanaged<ushort, byte>)&ReadPort;
        nuint start = output < input ? output : input;
        nuint end = output > input ? output + (nuint)PortIoStubPlan.Out8RequiredByteLength : input + (nuint)PortIoStubPlan.In8RequiredByteLength;
        var request = CheckedPortIoFunctionPointerBindingRequest.Create(true, Com1OwnedPortIoBackend.BasePort,
            start, end, end - start, output - start, input - start, output, input,
            PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength,
            PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength);
        return new Com1OwnedPortIoBackend(InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request));
    }

    [UnmanagedCallersOnly]
    private static void WritePort(ushort port, byte value)
    {
        if (port != Com1OwnedPortIoBackend.DataRegisterPort || _queued >= _capacity || _written >= Captured.Length)
        {
            _invalidIo = true;
            return;
        }

        Captured[_written++] = value;
        _queued++;
    }

    [UnmanagedCallersOnly]
    private static byte ReadPort(ushort port)
    {
        _reads++;
        if (port == Com1OwnedPortIoBackend.LineControlRegisterPort)
            return _lineControl;
        if (port == Com1OwnedPortIoBackend.InterruptEnableRegisterPort)
            return _interrupts;
        if (port == Com1OwnedPortIoBackend.BasePort + 2)
        {
            _identificationReads++;
            return _identification;
        }

        if (port != Com1OwnedPortIoBackend.LineStatusRegisterPort)
        {
            _invalidIo = true;
            return 0;
        }

        _polls++;
        if (_written >= _stallAfterBytes)
            return 0;
        if (_drainOnPoll)
            _queued = 0;
        if (_queued != 0 && --_ticks == 0)
        {
            _queued--;
            _ticks = _ticksPerCharacter;
        }

        return _queued == 0 ? (byte)0x20 : (byte)0;
    }
}
