namespace OS.Net;

using OS.Std.Memory;

public static class HttpResponseWriter
{
    private const string StatusLinePrefix = "HTTP/1.1 ";
    private const string ContentLengthHeaderPrefix = "Content-Length: ";
    private const string ContentTypeHeaderPrefix = "Content-Type: ";
    private const string ConnectionHeaderPrefix = "Connection: ";
    private const string CloseValue = "close";
    private const string KeepAliveValue = "keep-alive";
    private const string Crlf = "\r\n";
    private const string HeaderTerminator = "\r\n";
    private const int StatusLineFixedBytes = 15;
    private const int ContentLengthFixedBytes = 18;
    private const int ContentTypeFixedBytes = 16;
    private const int ConnectionCloseFixedBytes = 19;
    private const int ConnectionKeepAliveFixedBytes = 24;
    private const int HeaderTerminatorBytes = 2;
    public static HttpResponseWriterStatus TryWrite(ushort statusCode, ReadOnlySpan<byte> reasonPhrase, ReadOnlySpan<byte> contentType, ReadOnlySpan<byte> body, bool connectionClose, Span<byte> destination, out HttpResponseWriterResult result)
    {
        int contentLengthDigitCount = CountDecimalDigits(body.Length);
        int bytesRequired = StatusLineFixedBytes + reasonPhrase.Length + ContentLengthFixedBytes + contentLengthDigitCount + ContentTypeFixedBytes + contentType.Length + (connectionClose ? ConnectionCloseFixedBytes : ConnectionKeepAliveFixedBytes) + HeaderTerminatorBytes + body.Length;
        if (statusCode < 100 || statusCode > 599)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.InvalidStatusCode, statusCode, bytesRequired);
            return result.Status;
        }

        if (reasonPhrase.Length == 0)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.EmptyReasonPhrase, statusCode, bytesRequired);
            return result.Status;
        }

        if (!IsSafeReasonPhrase(reasonPhrase))
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.InvalidReasonPhrase, statusCode, bytesRequired);
            return result.Status;
        }

        if (contentType.Length == 0)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.EmptyContentType, statusCode, bytesRequired);
            return result.Status;
        }

        if (!IsSafeHeaderValue(contentType))
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.InvalidContentType, statusCode, bytesRequired);
            return result.Status;
        }

        if (destination.Length < bytesRequired)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.DestinationTooSmall, statusCode, bytesRequired);
            return result.Status;
        }

        int offset = 0;
        WriteAscii(StatusLinePrefix, destination, ref offset);
        WriteStatusCode(statusCode, destination, ref offset);
        destination[offset] = (byte)' ';
        offset++;
        BufferOperationResult reasonCopy = BufferOperations.MemoryCopy(reasonPhrase, destination.Slice(offset, reasonPhrase.Length));
        if (!reasonCopy.Succeeded)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.DestinationTooSmall, statusCode, bytesRequired);
            return result.Status;
        }

        offset += reasonPhrase.Length;
        WriteAscii(Crlf, destination, ref offset);
        WriteAscii(ContentLengthHeaderPrefix, destination, ref offset);
        WriteDecimal(body.Length, destination, ref offset);
        WriteAscii(Crlf, destination, ref offset);
        WriteAscii(ContentTypeHeaderPrefix, destination, ref offset);
        BufferOperationResult contentTypeCopy = BufferOperations.MemoryCopy(contentType, destination.Slice(offset, contentType.Length));
        if (!contentTypeCopy.Succeeded)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.DestinationTooSmall, statusCode, bytesRequired);
            return result.Status;
        }

        offset += contentType.Length;
        WriteAscii(Crlf, destination, ref offset);
        WriteAscii(ConnectionHeaderPrefix, destination, ref offset);
        WriteAscii(connectionClose ? CloseValue : KeepAliveValue, destination, ref offset);
        WriteAscii(Crlf, destination, ref offset);
        WriteAscii(HeaderTerminator, destination, ref offset);
        int bodyOffset = offset;
        BufferOperationResult bodyCopy = BufferOperations.MemoryCopy(body, destination.Slice(offset, body.Length));
        if (!bodyCopy.Succeeded)
        {
            result = HttpResponseWriterResult.Fail(HttpResponseWriterStatus.DestinationTooSmall, statusCode, bytesRequired);
            return result.Status;
        }

        offset += body.Length;
        result = HttpResponseWriterResult.Success(statusCode, offset, bodyOffset, bodyOffset, body.Length);
        return result.Status;
    }

    private static int CountDecimalDigits(int value)
    {
        int digits = 1;
        int remaining = value;
        while (remaining >= 10)
        {
            remaining /= 10;
            digits++;
        }

        return digits;
    }

    private static void WriteStatusCode(ushort statusCode, Span<byte> destination, ref int offset)
    {
        destination[offset] = (byte)('0' + (statusCode / 100));
        destination[offset + 1] = (byte)('0' + ((statusCode / 10) % 10));
        destination[offset + 2] = (byte)('0' + (statusCode % 10));
        offset += 3;
    }

    private static void WriteDecimal(int value, Span<byte> destination, ref int offset)
    {
        int digits = CountDecimalDigits(value);
        int writeOffset = offset + digits - 1;
        int remaining = value;
        for (int index = 0; index < digits; index++)
        {
            destination[writeOffset - index] = (byte)('0' + (remaining % 10));
            remaining /= 10;
        }

        offset += digits;
    }

    private static void WriteAscii(string value, Span<byte> destination, ref int offset)
    {
        for (int i = 0; i < value.Length; i++)
        {
            destination[offset] = (byte)value[i];
            offset++;
        }
    }

    private static bool IsSafeReasonPhrase(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 0x20 || b > 0x7e || b == (byte)'\r' || b == (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeHeaderValue(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 0x20 || b > 0x7e || b == (byte)'\r' || b == (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }
}
