using System.Runtime.CompilerServices;
using OS.Net;

internal static class DnsMessageTests
{
    private const ushort TransactionId = 0x1234;

    [HostTest("net.dns writes a-record query", HostLane.Fast, 0)]
    private static bool TestWritesARecordQuery()
    {
        byte[] buffer = new byte[64];
        byte[] expected =
        {
            0x12, 0x34,
            0x01, 0x00,
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00,
            0x07,
            0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65,
            0x03,
            0x63, 0x6F, 0x6D,
            0x00,
            0x00, 0x01,
            0x00, 0x01
        };

        DnsMessageStatus status = DnsMessage.TryWriteQuery(
            buffer,
            TransactionId,
            "example.com",
            out int bytesWritten);

        return HostCheck.Result(status == DnsMessageStatus.Success
            && bytesWritten == expected.Length
            && buffer.AsSpan(0, bytesWritten).SequenceEqual(expected));
    }

    [HostTest("net.dns parses compressed a-record response", HostLane.Fast, 1)]
    private static bool TestParsesCompressedARecordResponse()
    {
        byte[] response = CreateCompressedResponse(
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);

        DnsMessageStatus status = DnsMessage.TryParseAResponse(response, TransactionId, out Ipv4Address address);

        return HostCheck.Result(status == DnsMessageStatus.Success
            && address == new Ipv4Address(93, 184, 216, 34));
    }

    [HostTest("net.dns rejects malformed compression", HostLane.Fast, 2)]
    private static bool TestRejectsMalformedCompression()
    {
        byte[] response = CreateCompressedResponse(
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        int answerNameOffset = DnsMessage.HeaderLength + EncodedExampleComLength + 4;
        response[answerNameOffset] = 0xC0;
        response[answerNameOffset + 1] = (byte)answerNameOffset;

        DnsMessageStatus status = DnsMessage.TryParseAResponse(response, TransactionId, out Ipv4Address _);

        return HostCheck.Result(status == DnsMessageStatus.CompressionPointerLoop);
    }

    [HostTest("net.dns rejects forward compression pointer", HostLane.Fast, 3)]
    private static bool TestRejectsForwardCompressionPointer()
    {
        byte[] response = CreateCompressedResponse(
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        int answerNameOffset = DnsMessage.HeaderLength + EncodedExampleComLength + 4;
        response[answerNameOffset] = 0xC0;
        response[answerNameOffset + 1] = (byte)(answerNameOffset + 4);

        DnsMessageStatus status = DnsMessage.TryParseAResponse(response, TransactionId, out Ipv4Address _);

        return HostCheck.Result(status == DnsMessageStatus.CompressionPointerLoop);
    }

    [HostTest("net.dns rejects out-of-range compression pointer", HostLane.Fast, 4)]
    private static bool TestRejectsOutOfRangeCompressionPointer()
    {
        byte[] response = CreateCompressedResponse(
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        int answerNameOffset = DnsMessage.HeaderLength + EncodedExampleComLength + 4;
        response[answerNameOffset] = 0xC0;
        response[answerNameOffset + 1] = 0xFF;

        DnsMessageStatus status = DnsMessage.TryParseAResponse(response, TransactionId, out Ipv4Address _);

        return HostCheck.Result(status == DnsMessageStatus.CompressionPointerOutOfRange);
    }

    [HostTest("net.dns rejects unsupported response", HostLane.Fast, 5)]
    private static bool TestRejectsUnsupportedResponse()
    {
        byte[] rcodeResponse = CreateCompressedResponse(
            flags: 0x8183,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsMessageStatus rcodeStatus = DnsMessage.TryParseAResponse(
            rcodeResponse,
            TransactionId,
            out Ipv4Address _);

        byte[] unsupportedTypeResponse = CreateCompressedResponse(
            flags: 0x8180,
            answerType: 28,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsMessageStatus typeStatus = DnsMessage.TryParseAResponse(
            unsupportedTypeResponse,
            TransactionId,
            out Ipv4Address _);

        byte[] unsupportedClassResponse = CreateCompressedResponse(
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: 3,
            answerDataLength: 4);
        DnsMessageStatus classStatus = DnsMessage.TryParseAResponse(
            unsupportedClassResponse,
            TransactionId,
            out Ipv4Address _);

        return HostCheck.Result(rcodeStatus == DnsMessageStatus.ResponseCodeFailure
            && typeStatus == DnsMessageStatus.UnsupportedType
            && classStatus == DnsMessageStatus.UnsupportedClass);
    }

    [HostTest("net.dns message uses shared ascii hostname helper", HostLane.Fast, 6)]
    private static bool TestUsesSharedAsciiHostnameHelper()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var helper = catalog.Method(typeof(DnsMessage).FullName!, "IsHostNameByte");
        var calls = helper.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Select(catalog.Invocation).ToArray();
        return HostCheck.Result(calls.Length == 1
            && calls[0].Name == nameof(OS.Std.Ascii.IsLetterOrDigit)
            && calls[0].ContainingType.ToDisplayString() == typeof(OS.Std.Ascii).FullName
            && calls[0].ContainingAssembly.Name == typeof(OS.Std.Ascii).Assembly.GetName().Name
            && !helper.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax>()
                .Any(binary => binary.RawKind is (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanOrEqualExpression
                    or (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanOrEqualExpression));
    }

    private const int EncodedExampleComLength = 13;

    private static byte[] CreateCompressedResponse(
        ushort flags,
        ushort answerType,
        ushort answerClass,
        ushort answerDataLength)
    {
        byte[] response = new byte[DnsMessage.HeaderLength + EncodedExampleComLength + 4 + 2 + 10 + answerDataLength];

        response[0] = 0x12;
        response[1] = 0x34;
        response[2] = (byte)(flags >> 8);
        response[3] = (byte)flags;
        response[4] = 0x00;
        response[5] = 0x01;
        response[6] = 0x00;
        response[7] = 0x01;
        response[8] = 0x00;
        response[9] = 0x00;
        response[10] = 0x00;
        response[11] = 0x00;

        int offset = DnsMessage.HeaderLength;
        response[offset++] = 0x07;
        response[offset++] = 0x65;
        response[offset++] = 0x78;
        response[offset++] = 0x61;
        response[offset++] = 0x6D;
        response[offset++] = 0x70;
        response[offset++] = 0x6C;
        response[offset++] = 0x65;
        response[offset++] = 0x03;
        response[offset++] = 0x63;
        response[offset++] = 0x6F;
        response[offset++] = 0x6D;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x01;
        response[offset++] = 0x00;
        response[offset++] = 0x01;

        response[offset++] = 0xC0;
        response[offset++] = 0x0C;
        response[offset++] = (byte)(answerType >> 8);
        response[offset++] = (byte)answerType;
        response[offset++] = (byte)(answerClass >> 8);
        response[offset++] = (byte)answerClass;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x3C;
        response[offset++] = (byte)(answerDataLength >> 8);
        response[offset++] = (byte)answerDataLength;

        if (answerDataLength > 0)
        {
            response[offset++] = 0x5D;
        }

        if (answerDataLength > 1)
        {
            response[offset++] = 0xB8;
        }

        if (answerDataLength > 2)
        {
            response[offset++] = 0xD8;
        }

        if (answerDataLength > 3)
        {
            response[offset] = 0x22;
        }

        return response;
    }
}
