using OS.Std.Text;

namespace OS.Net;
public static class HttpResponseParser
{
    public const int MinimumStatusLineLength = 12;
    public static HttpResponseParseStatus TryParse(ReadOnlySpan<byte> response, out HttpResponseParseResult result)
    {
        result = default;
        if (response.Length < MinimumStatusLineLength)
        {
            result = HttpResponseParseResult.Fail(HttpResponseParseStatus.BufferTooSmall);
            return result.Status;
        }

        int statusLineEnd = IndexOfCrlf(response, 0);
        if (statusLineEnd < 0)
        {
            result = HttpResponseParseResult.Fail(HttpResponseParseStatus.MissingStatusLineTerminator);
            return result.Status;
        }

        if (!TryParseStatusLine(response, statusLineEnd, out byte major, out byte minor, out ushort statusCode, out int reasonOffset, out int reasonLength, out HttpResponseParseStatus statusLineStatus))
        {
            result = HttpResponseParseResult.Fail(statusLineStatus);
            return result.Status;
        }

        int headerTerminator = IndexOfHeaderTerminator(response, statusLineEnd + 2);
        if (headerTerminator < 0)
        {
            result = HttpResponseParseResult.Fail(HttpResponseParseStatus.MissingHeaderTerminator);
            return result.Status;
        }

        bool hasContentLength = false;
        int contentLength = 0;
        bool connectionClose = false;
        HttpResponseParseStatus headerStatus = ParseHeaders(response, statusLineEnd + 2, headerTerminator, ref hasContentLength, ref contentLength, ref connectionClose);
        if (headerStatus != HttpResponseParseStatus.Success)
        {
            result = HttpResponseParseResult.Fail(headerStatus);
            return result.Status;
        }

        if (!hasContentLength)
        {
            result = HttpResponseParseResult.Fail(HttpResponseParseStatus.MissingContentLength);
            return result.Status;
        }

        int bodyOffset = headerTerminator + 4;
        int availableBodyBytes = response.Length - bodyOffset;
        int bodyLength = contentLength;
        if (bodyLength > availableBodyBytes)
        {
            result = HttpResponseParseResult.Fail(HttpResponseParseStatus.BodyTooShort);
            return result.Status;
        }

        result = HttpResponseParseResult.Success(major, minor, statusCode, reasonOffset, reasonLength, bodyOffset, hasContentLength, contentLength, connectionClose, bodyOffset, bodyLength);
        return result.Status;
    }

    private static bool TryParseStatusLine(ReadOnlySpan<byte> response, int statusLineEnd, out byte major, out byte minor, out ushort statusCode, out int reasonOffset, out int reasonLength, out HttpResponseParseStatus status)
    {
        major = 0;
        minor = 0;
        statusCode = 0;
        reasonOffset = 0;
        reasonLength = 0;
        status = HttpResponseParseStatus.Success;
        if (statusLineEnd < MinimumStatusLineLength - 2 || !OS.Std.MemoryExtensions.StartsWith(response, "HTTP/"u8) || !OS.Std.Ascii.IsDigit(response[5]) || response[6] != (byte)'.' || !OS.Std.Ascii.IsDigit(response[7]) || response[8] != (byte)' ')
        {
            status = HttpResponseParseStatus.InvalidProtocol;
            return false;
        }

        if (!OS.Std.Ascii.IsDigit(response[9]) || !OS.Std.Ascii.IsDigit(response[10]) || !OS.Std.Ascii.IsDigit(response[11]))
        {
            status = HttpResponseParseStatus.InvalidStatusCode;
            return false;
        }

        major = (byte)(response[5] - (byte)'0');
        minor = (byte)(response[7] - (byte)'0');
        statusCode = (ushort)(((response[9] - (byte)'0') * 100) + ((response[10] - (byte)'0') * 10) + (response[11] - (byte)'0'));
        if (statusLineEnd > 12)
        {
            if (response[12] != (byte)' ')
            {
                status = HttpResponseParseStatus.InvalidStatusCode;
                return false;
            }

            reasonOffset = 13;
            reasonLength = statusLineEnd - reasonOffset;
        }
        else
        {
            reasonOffset = statusLineEnd;
            reasonLength = 0;
        }

        return true;
    }

    private static HttpResponseParseStatus ParseHeaders(ReadOnlySpan<byte> response, int headersStart, int headersEnd, ref bool hasContentLength, ref int contentLength, ref bool connectionClose)
    {
        int offset = headersStart;
        while (offset < headersEnd)
        {
            int lineEnd = IndexOfCrlf(response, offset);
            if (lineEnd < 0 || lineEnd > headersEnd)
            {
                return HttpResponseParseStatus.MissingHeaderTerminator;
            }

            if (lineEnd == offset)
            {
                return HttpResponseParseStatus.InvalidHeader;
            }

            int colon = IndexOfByte(response, offset, lineEnd, (byte)':');
            if (colon <= offset)
            {
                return HttpResponseParseStatus.InvalidHeader;
            }

            ReadOnlySpan<byte> rawValue = response.Slice(colon + 1, lineEnd - colon - 1);
            if (!IsValidHeaderValue(rawValue))
            {
                return HttpResponseParseStatus.InvalidHeader;
            }

            ReadOnlySpan<byte> name = response.Slice(offset, colon - offset);
            ReadOnlySpan<byte> value = AsciiString.TrimSpaceOrTab(rawValue);
            if (AsciiString.EqualsIgnoreCase(name, "Content-Length"u8))
            {
                if (hasContentLength)
                {
                    return HttpResponseParseStatus.DuplicateContentLength;
                }

                if (!TryParseNonNegativeDecimal(value, out int parsedLength))
                {
                    return HttpResponseParseStatus.InvalidContentLength;
                }

                hasContentLength = true;
                contentLength = parsedLength;
            }
            else if (AsciiString.EqualsIgnoreCase(name, "Connection"u8))
            {
                connectionClose = AsciiString.EqualsIgnoreCase(value, "close"u8);
            }
            else if (AsciiString.EqualsIgnoreCase(name, "Transfer-Encoding"u8) && !AsciiString.EqualsIgnoreCase(value, "identity"u8))
            {
                return HttpResponseParseStatus.UnsupportedTransferEncoding;
            }

            offset = lineEnd + 2;
        }

        return HttpResponseParseStatus.Success;
    }

    private static bool IsValidHeaderValue(ReadOnlySpan<byte> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            byte c = value[index];
            if (c != (byte)'\t' && (c < 0x20 || c >= 0x7f))
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOfHeaderTerminator(ReadOnlySpan<byte> bytes, int start)
    {
        ReadOnlySpan<byte> terminator = stackalloc byte[]
        {
            (byte)'\r',
            (byte)'\n',
            (byte)'\r',
            (byte)'\n'
        };
        return IndexOfBytes(bytes, start, terminator);
    }

    private static int IndexOfCrlf(ReadOnlySpan<byte> bytes, int start)
    {
        ReadOnlySpan<byte> crlf = stackalloc byte[]
        {
            (byte)'\r',
            (byte)'\n'
        };
        return IndexOfBytes(bytes, start, crlf);
    }

    private static int IndexOfByte(ReadOnlySpan<byte> bytes, int start, int endExclusive, byte value)
    {
        if ((uint)start > (uint)bytes.Length || endExclusive < start || endExclusive > bytes.Length)
        {
            return -1;
        }

        int relativeIndex = OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start, endExclusive - start), value);
        return relativeIndex < 0 ? -1 : start + relativeIndex;
    }

    private static int IndexOfBytes(ReadOnlySpan<byte> bytes, int start, ReadOnlySpan<byte> value)
    {
        if ((uint)start > (uint)bytes.Length)
        {
            return -1;
        }

        int relativeIndex = OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), value);
        return relativeIndex < 0 ? -1 : start + relativeIndex;
    }

    private static bool TryParseNonNegativeDecimal(ReadOnlySpan<byte> value, out int result)
    {
        result = 0;
        if (value.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            byte c = value[index];
            if (!OS.Std.Ascii.IsDigit(c))
            {
                return false;
            }

            int digit = c - (byte)'0';
            if (result > (int.MaxValue - digit) / 10)
            {
                return false;
            }

            result = (result * 10) + digit;
        }

        return true;
    }
}
