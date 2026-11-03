using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow at most4MiB of quota HTTP values. Parse
    /// GET/POST HTTP/1.1 requests or explicitly length/chunk-framed responses;
    /// return consumed offsets and preserve a final incomplete suffix. Headers
    /// including terminator are<=8192 bytes/64 lines; decoded entity<=4MiB and
    /// messages<=8192. Reject duplicate/conflicting headers, malformed CRLF,
    /// unsupported transfer/chunk syntax, trailers, interim/upgrade/HEAD and
    /// close-delimited responses. Existing positive readers/caps are unchanged.
    /// No FIN/RST, PCAP, clock, ownership or execution verdict is supplied.</summary>

    public static ServiceQuotaHttpPrefixes InspectNativeAotServiceQuotaHttpPrefixesForTests(ReadOnlySpan<byte> bytes, bool responses) => ReadServiceQuotaHttpPrefixes(bytes, responses);
    private static ServiceQuotaHttpPrefixes ReadServiceQuotaHttpPrefixes(ReadOnlySpan<byte> bytes, bool responses, bool webDiagnostics = false)
    {
        const int maximum = 4 * 1024 * 1024;
        Need(bytes.Length <= maximum, "quota_http_input_bound");
        List<ServiceQuotaHttpPrefix> messages = [];
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            Need(messages.Count < 8192, "quota_http_message_bound");
            var remaining = bytes[cursor..];
            int at = remaining.IndexOf("\r\n\r\n"u8);
            bool completeHead = at >= 0;
            Need(completeHead ? at + 4 <= 8192 : remaining.Length < 8192, "quota_http_header_bound");
            var head = QuotaHttpPrefixHead(completeHead ? remaining[..at] : remaining, completeHead, responses, webDiagnostics);
            var headers = head.Headers;
            bool chunked = headers.TryGetValue("Transfer-Encoding", out string? transfer), hasLength = headers.TryGetValue("Content-Length", out string? lengthText);
            Need(!chunked || !hasLength && transfer == "chunked", "quota_http_framing");
            Need(!headers.ContainsKey("Trailer") && !headers.ContainsKey("Content-Encoding"), "quota_http_encoding");
            int length = hasLength ? QuotaHttpPrefixLength(lengthText!) : 0;
            var frozen = new ReadOnlyDictionary<string, string>(headers);
            if (!completeHead)
            {
                messages.Add(new(cursor, -1, bytes.Length, head.First, frozen, "pending", -1, "header", ""));
                break;
            }

            Need(!responses || chunked || hasLength, "quota_http_response_framing");
            int headerEnd = cursor + at + 4, end;
            string state, body;
            if (chunked)
            {
                var entity = QuotaHttpPrefixChunks(bytes[headerEnd..], maximum);
                end = headerEnd + entity.Consumed;
                state = entity.State;
                body = entity.Body;
            }
            else
            {
                int available = Math.Min(length, bytes.Length - headerEnd);
                end = headerEnd + available;
                state = available == length ? "complete" : "body";
                body = Convert.ToBase64String(bytes.Slice(headerEnd, available));
            }

            messages.Add(new(cursor, headerEnd, end, head.First, frozen, chunked ? "chunked" : hasLength ? "length" : "none", chunked ? -1 : length, state, body));
            if (state != "complete")
                break;
            cursor = end;
        }

        return new(bytes.Length, responses, Array.AsReadOnly(messages.ToArray()));
    }

    private static int QuotaHttpPrefixLength(string text)
    {
        Need(int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int length) && length is >= 0 and <= 4 * 1024 * 1024 && text == length.ToString(CultureInfo.InvariantCulture), "quota_http_content_length");
        return length;
    }

    private static (string First, Dictionary<string, string> Headers) QuotaHttpPrefixHead(ReadOnlySpan<byte> bytes, bool complete, bool responses, bool webDiagnostics)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            Need(b == 13 ? i + 1 == bytes.Length ? !complete : bytes[i + 1] == 10 : b == 10 ? i > 0 && bytes[i - 1] == 13 : b is >= 32 and <= 126, "quota_http_header_byte");
        }

        string[] rows = Encoding.ASCII.GetString(bytes).Split("\r\n", StringSplitOptions.None);
        Need(rows.Length <= 64 && rows.All(row => row.Length <= 8192), "quota_http_header_lines");
        int known = complete ? rows.Length : rows.Length - 1;
        if (known > 0)
            QuotaHttpPrefixFirst(rows[0], responses, webDiagnostics);
        else
            QuotaHttpPrefixFirstFragment(rows[0], responses, webDiagnostics);
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < known; i++)
            QuotaHttpPrefixHeader(rows[i], headers);
        if (!complete && known > 0 && rows[^1].Length > 0)
        {
            string fragment = rows[^1];
            bool lineEnd = fragment.EndsWith('\r');
            if (lineEnd)
                fragment = fragment[..^1];
            if (fragment.Length == 0)
                return (rows[0], headers);
            if (lineEnd)
            {
                QuotaHttpPrefixHeader(fragment, headers);
                return (rows[0], headers);
            }

            int colon = fragment.IndexOf(':');
            int nameEnd = colon < 0 ? fragment.Length : colon;
            Need(nameEnd > 0 && fragment[..nameEnd].All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && (colon < 0 || !headers.ContainsKey(fragment[..colon]) && (colon + 1 == fragment.Length || fragment[colon + 1] == ' ')), "quota_http_header_fragment");
        }

        if (complete && !responses)
            Need(headers.ContainsKey("Host"), "quota_http_host");
        return (rows[0], headers);
    }

    private static void QuotaHttpPrefixHeader(string row, Dictionary<string, string> headers)
    {
        int colon = row.IndexOf(':');
        Need(colon > 0 && colon + 2 < row.Length && row[colon + 1] == ' ' && row[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && row[(colon + 2)..] == row[(colon + 2)..].Trim() && headers.TryAdd(row[..colon], row[(colon + 2)..]), "quota_http_header");
    }

    private static void QuotaHttpPrefixFirst(string first, bool responses, bool webDiagnostics)
    {
        if (responses)
        {
            Need(first.Length >= 14 && first.StartsWith("HTTP/1.1 ", StringComparison.Ordinal) && first[12] == ' ' && int.TryParse(first.AsSpan(9, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int status) && status >= 200 && status <= (webDiagnostics ? 999 : 599) && status is not (204 or 304), "quota_http_response");
        }
        else
        {
            string[] fields = first.Split(' ');
            Need(fields.Length == 3 && fields[0] is "GET" or "POST" && fields[1].StartsWith("/", StringComparison.Ordinal) && fields[1].Length > 0 && !fields[1].Contains('#') && fields[2] == "HTTP/1.1", "quota_http_request");
        }
    }

    private static void QuotaHttpPrefixFirstFragment(string fragment, bool responses, bool webDiagnostics)
    {
        // A final CR may be the first half of the unfinished line terminator.
        if (fragment.EndsWith('\r'))
        {
            QuotaHttpPrefixFirst(fragment[..^1], responses, webDiagnostics);
            return;
        }

        if (responses)
        {
            const string prefix = "HTTP/1.1 ";
            Need(fragment.Length <= prefix.Length ? prefix.StartsWith(fragment, StringComparison.Ordinal) : fragment.StartsWith(prefix, StringComparison.Ordinal), "quota_http_response_fragment");
            if (fragment.Length > 9)
            {
                int digits = Math.Min(3, fragment.Length - 9);
                Need(fragment.Substring(9, digits).All(char.IsAsciiDigit) && fragment[9] >= '2' && fragment[9] <= (webDiagnostics ? '9' : '5') && (fragment.Length < 13 || fragment[12] == ' '), "quota_http_response_fragment");
            }
        }
        else
        {
            string[] fields = fragment.Split(' ');
            Need(fields.Length <= 3 && (fields.Length == 1 ? "GET".StartsWith(fragment, StringComparison.Ordinal) || "POST".StartsWith(fragment, StringComparison.Ordinal) : fields[0] is "GET" or "POST" && (fields[1].Length == 0 || fields[1].StartsWith("/", StringComparison.Ordinal)) && !fields[1].Contains('#') && (fields.Length < 3 || fields[1].Length > 0 && "HTTP/1.1".StartsWith(fields[2], StringComparison.Ordinal))), "quota_http_request_fragment");
        }
    }

    private static (int Consumed, string State, string Body) QuotaHttpPrefixChunks(ReadOnlySpan<byte> bytes, int maximum)
    {
        using MemoryStream body = new();
        int cursor = 0, inputLength = bytes.Length;
        (int, string, string) Result(string state) => (inputLength, state, Convert.ToBase64String(body.ToArray()));
        for (;;)
        {
            int line = bytes[cursor..].IndexOf("\r\n"u8);
            if (line < 0)
            {
                var partial = bytes[cursor..];
                if (!partial.IsEmpty && partial[^1] == 13)
                {
                    partial = partial[..^1];
                    Need(!partial.IsEmpty, "quota_http_chunk_size");
                }

                Need(partial.Length <= 8 && Encoding.ASCII.GetString(partial).All(char.IsAsciiHexDigit) && (partial.IsEmpty || uint.TryParse(Encoding.ASCII.GetString(partial), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint pending) && pending <= maximum - body.Length), "quota_http_chunk_size");
                return Result("chunk-size");
            }

            Need(line is> 0 and <= 8, "quota_http_chunk_size");
            string digits = Encoding.ASCII.GetString(bytes.Slice(cursor, line));
            uint size = 0;
            Need(digits.All(char.IsAsciiHexDigit) && uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out size) && size <= maximum - body.Length, "quota_http_chunk_size");
            cursor += line + 2;
            int copied = Math.Min((int)size, bytes.Length - cursor);
            body.Write(bytes.Slice(cursor, copied));
            cursor += copied;
            if (copied < size)
                return Result("chunk-data");
            int delimiter = bytes.Length - cursor;
            Need(delimiter == 0 || bytes[cursor] == 13, "quota_http_chunk_delimiter");
            if (delimiter >= 2)
                Need(bytes[cursor + 1] == 10, "quota_http_chunk_delimiter");
            if (delimiter < 2)
                return Result(size == 0 ? "chunk-end" : "chunk-delimiter");
            cursor += 2;
            if (size == 0)
                return (cursor, "complete", Convert.ToBase64String(body.ToArray()));
        }
    }
}
