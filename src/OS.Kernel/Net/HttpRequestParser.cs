using OS.Std.Text;

namespace OS.Net;
public static class HttpRequestParser
{
    public const int MinimumRequestLineLength = 16;
    public static HttpRequestParseStatus TryParse(ReadOnlySpan<byte> request, out HttpRequestParseResult result)
    {
        result = default;
        if (request.Length < MinimumRequestLineLength)
        {
            result = HttpRequestParseResult.Fail(HttpRequestParseStatus.BufferTooSmall);
            return result.Status;
        }

        int requestLineEnd = IndexOfCrlf(request, 0);
        if (requestLineEnd < 0)
        {
            result = HttpRequestParseResult.Fail(HttpRequestParseStatus.MissingRequestLineTerminator);
            return result.Status;
        }

        if (!TryParseRequestLine(request, requestLineEnd, out HttpRequestMethod method, out int targetOffset, out int targetLength, out byte major, out byte minor, out HttpRequestParseStatus requestLineStatus))
        {
            result = HttpRequestParseResult.Fail(requestLineStatus);
            return result.Status;
        }

        int headerTerminator = IndexOfHeaderTerminator(request, requestLineEnd + 2);
        if (headerTerminator < 0)
        {
            result = HttpRequestParseResult.Fail(HttpRequestParseStatus.MissingHeaderTerminator);
            return result.Status;
        }

        bool hasHost = false;
        int hostOffset = 0;
        int hostLength = 0;
        bool hasContentLength = false;
        int contentLength = 0;
        bool connectionClose = false;
        HttpRequestParseStatus headerStatus = ParseHeaders(request, requestLineEnd + 2, headerTerminator, ref hasHost, ref hostOffset, ref hostLength, ref hasContentLength, ref contentLength, ref connectionClose);
        if (headerStatus != HttpRequestParseStatus.Success)
        {
            result = HttpRequestParseResult.Fail(headerStatus);
            return result.Status;
        }

        int bodyOffset = headerTerminator + 4;
        int availableBodyBytes = request.Length - bodyOffset;
        int bodyLength = hasContentLength ? contentLength : availableBodyBytes;
        if (bodyLength > availableBodyBytes)
        {
            result = HttpRequestParseResult.Fail(HttpRequestParseStatus.BodyTooShort);
            return result.Status;
        }

        result = HttpRequestParseResult.Success(method, targetOffset, targetLength, major, minor, bodyOffset, hasHost, hostOffset, hostLength, hasContentLength, contentLength, connectionClose, bodyOffset, bodyLength);
        return result.Status;
    }

    private static bool TryParseRequestLine(ReadOnlySpan<byte> request, int requestLineEnd, out HttpRequestMethod method, out int targetOffset, out int targetLength, out byte major, out byte minor, out HttpRequestParseStatus status)
    {
        method = HttpRequestMethod.Unknown;
        targetOffset = 0;
        targetLength = 0;
        major = 0;
        minor = 0;
        status = HttpRequestParseStatus.Success;
        int firstSpace = IndexOfByte(request, 0, requestLineEnd, (byte)' ');
        if (firstSpace <= 0)
        {
            status = HttpRequestParseStatus.UnsupportedMethod;
            return false;
        }

        method = ParseMethod(request.Slice(0, firstSpace));
        if (method == HttpRequestMethod.Unknown)
        {
            status = HttpRequestParseStatus.UnsupportedMethod;
            return false;
        }

        int secondSpace = IndexOfByte(request, firstSpace + 1, requestLineEnd, (byte)' ');
        if (secondSpace <= firstSpace + 1)
        {
            status = HttpRequestParseStatus.InvalidRequestTarget;
            return false;
        }

        targetOffset = firstSpace + 1;
        targetLength = secondSpace - targetOffset;
        if (!IsValidOriginFormTarget(request.Slice(targetOffset, targetLength)))
        {
            status = HttpRequestParseStatus.InvalidRequestTarget;
            return false;
        }

        ReadOnlySpan<byte> protocol = request.Slice(secondSpace + 1, requestLineEnd - secondSpace - 1);
        if (protocol.Length != 8 || !OS.Std.MemoryExtensions.StartsWith(protocol, "HTTP/"u8) || !OS.Std.Ascii.IsDigit(protocol[5]) || protocol[6] != (byte)'.' || !OS.Std.Ascii.IsDigit(protocol[7]))
        {
            status = HttpRequestParseStatus.InvalidProtocol;
            return false;
        }

        major = (byte)(protocol[5] - (byte)'0');
        minor = (byte)(protocol[7] - (byte)'0');
        return true;
    }

    private static HttpRequestParseStatus ParseHeaders(ReadOnlySpan<byte> request, int headersStart, int headersEnd, ref bool hasHost, ref int hostOffset, ref int hostLength, ref bool hasContentLength, ref int contentLength, ref bool connectionClose)
    {
        int offset = headersStart;
        while (offset < headersEnd)
        {
            int lineEnd = IndexOfCrlf(request, offset);
            if (lineEnd < 0 || lineEnd > headersEnd)
            {
                return HttpRequestParseStatus.MissingHeaderTerminator;
            }

            if (lineEnd == offset)
            {
                return HttpRequestParseStatus.InvalidHeader;
            }

            int colon = IndexOfByte(request, offset, lineEnd, (byte)':');
            if (colon <= offset || !IsValidHeaderName(request.Slice(offset, colon - offset)))
            {
                return HttpRequestParseStatus.InvalidHeader;
            }

            int valueOffset = colon + 1;
            while (valueOffset < lineEnd && OS.Std.Ascii.IsSpaceOrTab(request[valueOffset]))
            {
                valueOffset++;
            }

            ReadOnlySpan<byte> value = request.Slice(valueOffset, lineEnd - valueOffset);
            if (!IsValidHeaderValue(value))
            {
                return HttpRequestParseStatus.InvalidHeader;
            }

            ReadOnlySpan<byte> semanticValue = AsciiString.TrimSpaceOrTab(value);
            ReadOnlySpan<byte> name = request.Slice(offset, colon - offset);
            if (AsciiString.EqualsIgnoreCase(name, "Host"u8))
            {
                hasHost = true;
                hostOffset = valueOffset;
                hostLength = value.Length;
            }
            else if (AsciiString.EqualsIgnoreCase(name, "Content-Length"u8))
            {
                if (hasContentLength)
                {
                    return HttpRequestParseStatus.DuplicateContentLength;
                }

                if (!TryParseNonNegativeDecimal(semanticValue, out int parsedLength))
                {
                    return HttpRequestParseStatus.InvalidContentLength;
                }

                hasContentLength = true;
                contentLength = parsedLength;
            }
            else if (AsciiString.EqualsIgnoreCase(name, "Connection"u8))
            {
                connectionClose = AsciiString.EqualsIgnoreCase(semanticValue, "close"u8);
            }
            else if (AsciiString.EqualsIgnoreCase(name, "Transfer-Encoding"u8) && !AsciiString.EqualsIgnoreCase(semanticValue, "identity"u8))
            {
                return HttpRequestParseStatus.UnsupportedTransferEncoding;
            }

            offset = lineEnd + 2;
        }

        return HttpRequestParseStatus.Success;
    }

    private static HttpRequestMethod ParseMethod(ReadOnlySpan<byte> method)
    {
        if (AsciiString.Equals(method, "GET"u8))
        {
            return HttpRequestMethod.Get;
        }

        if (AsciiString.Equals(method, "HEAD"u8))
        {
            return HttpRequestMethod.Head;
        }

        return AsciiString.Equals(method, "POST"u8) ? HttpRequestMethod.Post : HttpRequestMethod.Unknown;
    }

    private static bool IsValidOriginFormTarget(ReadOnlySpan<byte> target)
    {
        if (target.Length == 0 || target[0] != (byte)'/')
        {
            return false;
        }

        for (int index = 0; index < target.Length; index++)
        {
            byte value = target[index];
            if (value <= 0x20 || value >= 0x7f)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidHeaderName(ReadOnlySpan<byte> name)
    {
        for (int index = 0; index < name.Length; index++)
        {
            byte value = name[index];
            bool token = OS.Std.Ascii.IsLetterOrDigit(value) || value == (byte)'-';
            if (!token)
            {
                return false;
            }
        }

        return true;
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
        if ((uint)start > (uint)bytes.Length)
        {
            return -1;
        }

        int relativeIndex = OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), "\r\n\r\n"u8);
        return relativeIndex < 0 ? -1 : start + relativeIndex;
    }

    private static int IndexOfCrlf(ReadOnlySpan<byte> bytes, int start)
    {
        if ((uint)start > (uint)bytes.Length)
        {
            return -1;
        }

        int relativeIndex = OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), "\r\n"u8);
        return relativeIndex < 0 ? -1 : start + relativeIndex;
    }

    private static int IndexOfByte(ReadOnlySpan<byte> bytes, int start, int endExclusive, byte value)
    {
        if ((uint)start > (uint)bytes.Length || (uint)endExclusive > (uint)bytes.Length || start > endExclusive)
        {
            return -1;
        }

        int relativeIndex = OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start, endExclusive - start), value);
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
