using OS.Kernel.Diagnostics;

internal static unsafe partial class OwnedCom1WriterTests
{
    [HostTest("serial.text low bytes preserve exact text including embedded zero")]
    private static bool TextLowBytes()
    {
        string text = new(Enumerable.Range(0, 256).Select(i => (char)(i + 256)).ToArray());
        Reset(0xc1, 16);
        return EmitText(text) && ExactText(text) && _queued == 0 && _identificationReads == 1;
    }

    [HostTest("serial.text FIFO boundaries and final tails preserve every byte")]
    private static bool TextBoundaries()
    {
        foreach (int length in Enumerable.Range(1, 33).Concat([255, 511, 512, 513, 1000]))
        {
            Reset(0xc1, 16);
            string text = new(Enumerable.Range(0, length).Select(i => (char)(i * 37)).ToArray());
            if (!EmitText(text) || !ExactText(text) || _queued != 0 || _polls > length * 128)
                return false;
        }

        return true;
    }

    [HostTest("serial.text empty and unready inputs perform no port IO")]
    private static bool TextInputs()
    {
        Reset(0xc1, 16);
        return OwnedCom1TextWriter.Write(default, "") &&
            !OwnedCom1TextWriter.Write(default, "a") && _reads == 0 && _written == 0;
    }

    [HostTest("serial.text unknown and broken FIFO identifications use byte fallback")]
    private static bool TextFallback()
    {
        foreach (byte identification in new byte[] { 0x01, 0x41, 0x81, 0xe1 })
        {
            Reset(identification, 1);
            if (!EmitText("a\0b\u01ff") || !ExactText("a\0b\u01ff") || _polls < 4 || _identificationReads != 1)
                return false;
        }

        return true;
    }

    [HostTest("serial.text enabled UART interrupts never acknowledge IIR")]
    private static bool TextInterrupts()
    {
        Reset(0xc1, 1);
        _interrupts = 2;
        return EmitText("interrupts enabled") && ExactText("interrupts enabled") && _identificationReads == 0;
    }

    [HostTest("serial.text divisor mode rejects before output")]
    private static bool TextDivisor()
    {
        Reset(0xc1, 16);
        _lineControl = 0x83;
        return !EmitText("blocked") && _written == 0 && _reads == 1 && !_invalidIo;
    }

    [HostTest("serial.text first readiness timeout retains 128 polls")]
    private static bool TextInitialTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 0;
        return !EmitText("blocked") && _polls == 128 && _written == 0 && !_invalidIo;
    }

    [HostTest("serial.text interrupted burst preserves exact prefix and bounded failure")]
    private static bool TextPrefixTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 16;
        const string text = "0123456789abcdef_after_prefix";
        return !EmitText(text) && _written == 16 && _polls == 2049 &&
            Captured.AsSpan(0, 16).SequenceEqual("0123456789abcdef"u8) && !_invalidIo;
    }

    [HostTest("serial.text final FIFO drain timeout invalidates a fully written string")]
    private static bool TextFinalTimeout()
    {
        Reset(0xc1, 16);
        _stallAfterBytes = 1;
        return !EmitText("a") && _written == 1 && _polls == 128 && Captured[0] == 'a' && !_invalidIo;
    }

    [HostTest("serial.text slow FIFO drain stays within per-byte allowance")]
    private static bool TextSlowDrain()
    {
        Reset(0xc1, 16);
        _ticksPerCharacter = 16;
        string text = new('x', 33);
        return EmitText(text) && ExactText(text) && _queued == 0 && _polls > 128 && _polls <= text.Length * 128;
    }

    [HostTest("serial.text successive string and hex writers return exclusive FIFO ownership")]
    private static bool TextOwnership()
    {
        Reset(0xc1, 16);
        return EmitText("prefix:") && _queued == 0 && Emit([0xab, 0xcd]) && _queued == 0 &&
            EmitText("\r\n") && _queued == 0 && _written == 13 &&
            Captured.AsSpan(0, _written).SequenceEqual("prefix:abcd\r\n"u8) &&
            _identificationReads == 3 && !_invalidIo;
    }

    [HostTest("serial.text FIFO needs one readiness observation per instant-drained burst")]
    private static bool TextReadinessWork()
    {
        Reset(0xc1, 16);
        _drainOnPoll = true;
        string text = new('x', 33);
        return EmitText(text) && ExactText(text) && _polls == 4 && _reads == 7 && _queued == 0;
    }

    [HostTest("serial.text waits for inherited FIFO and returns it to an ordinary byte writer")]
    private static bool TextByteHandoff()
    {
        Reset(0xc1, 16);
        _queued = 16;
        var backend = CreateBackend();
        return OwnedCom1TextWriter.Write(backend, "value=") && _queued == 0 &&
            backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, (byte)'7').Succeeded &&
            EmitText("\r\n") && _queued == 0 && !_invalidIo &&
            Captured.AsSpan(0, _written).SequenceEqual("value=7\r\n"u8);
    }

    private static bool EmitText(string text) => OwnedCom1TextWriter.Write(CreateBackend(), text);

    private static bool ExactText(string text) => !_invalidIo && _written == text.Length &&
        Captured.AsSpan(0, _written).SequenceEqual(text.Select(c => unchecked((byte)c)).ToArray());
}
