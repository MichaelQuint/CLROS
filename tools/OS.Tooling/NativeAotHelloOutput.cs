namespace OS.Tooling;

/// <summary>
/// Host-only admission of the unchanged Hello's three stdout byte envelopes in
/// a raw M16-009 serial tail. The caller retains the input; validation never
/// normalizes or changes it. Other kernel rows belong to the complete transcript
/// validator. Success proves only these write bytes, never guest execution.
/// </summary>
public static class NativeAotHelloOutput
{
    /// <summary>
    /// Require exactly the ordered fd1 writes of 27, 19 and 26 bytes, including
    /// their original LF payload endings and the kernel's following CRLF. Every
    /// write marker must start the input or follow LF; embedded markers, altered
    /// envelopes and missing/extra writes throw InvalidDataException.
    /// </summary>
    public static void Validate(ReadOnlySpan<byte> rawTail)
    {
        int cursor = 0, count = 0;
        while (cursor < rawTail.Length)
        {
            int relative = rawTail[cursor..].IndexOf("M16_005_WRITE"u8);
            if (relative < 0)
                break;
            int at = cursor + relative;
            if (count == 3 || at != 0 && rawTail[at - 1] != (byte)'\n')
                Reject();
            ReadOnlySpan<byte> expected = count switch
            {
                0 => "M16_005_WRITE fd=1 bytes=27 data=TEST_START nativeaot.hello\n\r\n"u8,
                1 => "M16_005_WRITE fd=1 bytes=19 data=NATIVEAOT_HELLO_OK\n\r\n"u8,
                _ => "M16_005_WRITE fd=1 bytes=26 data=TEST_PASS nativeaot.hello\n\r\n"u8
            };
            if (!rawTail[at..].StartsWith(expected))
                Reject();
            cursor = at + expected.Length;
            count++;
        }
        if (count != 3)
            Reject();
    }

    private static void Reject() => throw new InvalidDataException("hello_output_envelopes");
}
