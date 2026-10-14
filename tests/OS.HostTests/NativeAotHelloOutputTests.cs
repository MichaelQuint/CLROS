using System.Text;
using OS.Tooling;

internal static class NativeAotHelloOutputTests
{
    // Independent raw serial vectors: application LF bytes precede kernel CRLF.
    private const string First = "M16_005_WRITE fd=1 bytes=27 data=TEST_START nativeaot.hello\n\r\n";
    private const string Second = "M16_005_WRITE fd=1 bytes=19 data=NATIVEAOT_HELLO_OK\n\r\n";
    private const string Third = "M16_005_WRITE fd=1 bytes=26 data=TEST_PASS nativeaot.hello\n\r\n";
    private const string Raw = "TEST_START m16-009\r\n" + First + "M16_008_CALL first\r\n" +
        Second + "M16_009_IRQ captured\r\n" + Third + "M16_008_CALL last\r\n";

    [HostTest("nativeaot.hello-output accepts exact three raw writes among kernel rows")]
    private static void ExactBytes()
    {
        NativeAotHelloOutput.Validate(Encoding.ASCII.GetBytes(Raw));
        NativeAotHelloOutput.Validate(Encoding.ASCII.GetBytes(First + Second + Third));
    }

    [HostTest("nativeaot.hello-output rejects malformed fd count and data envelopes")]
    private static void EnvelopeFields()
    {
        foreach (string changed in new[]
        {
            First.Replace("fd=1", "fd=2", StringComparison.Ordinal),
            First.Replace("fd=1", "fd=10", StringComparison.Ordinal),
            First.Replace("bytes=27", "bytes=26", StringComparison.Ordinal),
            First.Replace("bytes=27", "bytes=027", StringComparison.Ordinal),
            First.Replace("bytes=27", "bytes=-27", StringComparison.Ordinal),
            First.Replace("bytes=27", "bytes=27x", StringComparison.Ordinal),
            First.Replace("data=", "data =", StringComparison.Ordinal)
        }) Reject(changed + Second + Third);
    }

    [HostTest("nativeaot.hello-output preserves payload LF and requires line-bound markers and kernel CRLF")]
    private static void RawFraming()
    {
        Reject(First.Replace("hello\n", "hello\r\n", StringComparison.Ordinal) + Second + Third);
        Reject(First.Replace("hello\n\r\n", "hello\nextra\r\n", StringComparison.Ordinal) + Second + Third);
        Reject(First[..^2] + "\n" + Second + Third);
        Reject("embedded " + First + Second + Third);
        Reject(First + Second + Third + "embedded " + First);
        Reject(First + Second.Replace("HELLO_OK", "HELLO_NO", StringComparison.Ordinal) + Third);
    }

    [HostTest("nativeaot.hello-output rejects missing reordered duplicate extra and truncated writes")]
    private static void Inventory()
    {
        Reject("");
        Reject(First + Second);
        Reject(First + Third + Second);
        Reject(First + First + Third);
        Reject(First + Second + Third + First);
        Reject(First + Second + Third[..^1]);
        Reject(First + Second + Third[..^12]);
    }

    private static void Reject(string raw)
    {
        bool rejected = false;
        try { NativeAotHelloOutput.Validate(Encoding.ASCII.GetBytes(raw)); }
        catch (InvalidDataException error) { rejected = error.Message == "hello_output_envelopes"; }
        HostCheck.True(rejected, "strict raw Hello write admission");
    }
}
