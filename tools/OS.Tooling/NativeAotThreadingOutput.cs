using System.Text;

namespace OS.Tooling;
/// <summary>Host-only decoding of stdout writes in a raw M16-009 serial tail.
/// Exact byte framing and complete app output are checked without executing or
/// qualifying any payload. Kernel scheduling/ownership records remain the parent
/// protocol checker's responsibility. Size failures retain their existing reason
/// and expose only scalar rejection details through ToolFailure.Data.</summary>

public static partial class NativeAotThreadingOutput
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Borrow an unmodified raw tail of at most 8 MiB for exactly
    /// NativeAotSuspensionSmoke, NativeAotThreadPoolSmoke,
    /// NativeAotSuspensionStress, NativeAotThreadPoolStress, NativeAotRuntimeMatrix
    /// the original NativeAotRuntimeSmoke, NativeAotSocketSmoke or its Async leaf,
    /// the containment control or the dedicated BufferList fixture.
    /// Each line-bound fd1
    /// envelope supplies 1..4096 canonical decimal payload bytes followed by the
    /// kernel's CRLF; total stdout is at most4096 bytes, or32768 solely for the
    /// BufferList fixture's bounded per-read progress. Arbitrary segmentation
    /// is retained in WriteLengths. NormalizedTail removes only CR/LF within
    /// those payload spans, retaining original headers/counts and all other bytes.
    /// Malformed framing/size/application/output throws ToolFailure; invalid UTF8
    /// throws DecoderFallbackException. Success validates neither other rows nor
    /// NativeAOT/CLROS execution. The caller owns the input throughout the call.</summary>

    public static Capture Read(string application, ReadOnlySpan<byte> rawTail)
    {
        Need(application is "NativeAotSuspensionSmoke" or "NativeAotThreadPoolSmoke" or "NativeAotSuspensionStress" or "NativeAotThreadPoolStress" or "NativeAotRuntimeMatrix" or "NativeAotRuntimeSmoke" or "NativeAotSocketSmoke" or "NativeAotSocketSmokeAsync" or "NativeAotSocketContainment" or "NativeAotSocketVectors", "application");
        Capture capture = ReadFramed(rawTail, web: false, vectors: application == "NativeAotSocketVectors");
        string text = capture.Stdout;
        switch (application)
        {
            case "NativeAotSuspensionSmoke":
                NativeAotThreadingGuest.ValidateSuspensionOutput(text);
                break;
            case "NativeAotThreadPoolSmoke":
                NativeAotThreadingGuest.ValidateThreadPoolOutput(text);
                break;
            case "NativeAotSuspensionStress":
                NativeAotSuspensionStressGuest.ValidateOutput(text);
                break;
            case "NativeAotThreadPoolStress":
                NativeAotThreadPoolStressGuest.ValidateOutput(text);
                break;
            case "NativeAotRuntimeMatrix":
                NativeAotRuntimeMatrixGuest.ValidateOutput(text);
                break;
            case "NativeAotRuntimeSmoke":
                ValidateRuntimeSmokeOutput(text);
                break;
            case "NativeAotSocketSmoke":
                NativeAotSocketPayload.ValidateOutput(text);
                break;
            case "NativeAotSocketSmokeAsync":
                NativeAotSocketPayload.ValidateAsyncOutput(text);
                break;
            case "NativeAotSocketContainment":
                _ = NativeAotSocketContainmentOutput.Read(text);
                break;
            case "NativeAotSocketVectors":
                _ = NativeAotSocketVectorsOutput.Read(text);
                break;
        }

        return capture;
    }

    /// <summary>Decode borrowed raw web-leaf bytes with exact fd1/fd2 framing,
    /// at most8192 bytes per write and65536 combined bytes, or the explicitly
    /// selected Full host collection allowance. Reassemble UTF8 per
    /// channel across arbitrary write boundaries. Structural text retains the
    /// canonical envelope headers but none of their untrusted payload bytes.
    /// Other bytes, including a final incomplete kernel row, remain unchanged.
    /// Malformed envelopes, size overflow or invalid channel UTF8 throw. This
    /// value-only decoder grants no app, scheduler, source or guest authority.</summary>

    public static Capture ReadWeb(ReadOnlySpan<byte> rawTail, bool diskStaticFull = false) => ReadFramed(rawTail, web: true, diskStaticFull: diskStaticFull);
    /// <summary>Decode a bounded continuing-service capture of at most128 MiB.
    /// The8192-byte write,65536 combined-output and4096-write limits match the
    /// web framing contract. Only the enclosing serial extent increases; finite
    /// readers retain8 MiB. Caller retains bytes through return. Framing alone
    /// validates no application, scheduler, completion or guest authority.</summary>

    public static Capture ReadService(ReadOnlySpan<byte> rawTail) => ReadFramed(rawTail, web: true, service: true);
    private static Capture ReadFramed(ReadOnlySpan<byte> rawTail, bool web, bool vectors = false, bool service = false, bool diskStaticFull = false)
    {
        Need(!diskStaticFull || web && !service && !vectors, "full_mode");
        string mode = diskStaticFull ? "disk-static-full" : service ? "service" : vectors ? "vectors" : web ? "web" : "threading";
        int rawLimit = diskStaticFull ? DiskStaticCapturePolicy.SerialBytes : (service ? 128 : 8) * 1024 * 1024;
        NeedSize(rawTail.Length > 0 && rawTail.Length <= rawLimit, "raw extent", 0, 0, rawTail.Length, 0, 0, 0, mode, rawLimit);
        int limit = diskStaticFull ? DiskStaticCapturePolicy.CombinedOutputBytes : web ? 65536 : vectors ? 32768 : 4096, writeLimit = web ? 8192 : 4096;
        byte[] stdout = new byte[limit], stderr = new byte[web ? limit : 0], normalized = new byte[rawTail.Length];
        List<int> lengths = [];
        List<uint> channels = [];
        int cursor = 0, total = 0, errors = 0, written = 0;
        while (cursor < rawTail.Length)
        {
            int relative = rawTail[cursor..].IndexOf("M16_005_WRITE"u8);
            if (relative < 0)
            {
                rawTail[cursor..].CopyTo(normalized.AsSpan(written));
                written += rawTail.Length - cursor;
                break;
            }

            int at = cursor + relative;
            Need(at == 0 || rawTail[at - 1] == (byte)'\n', "boundary");
            ReadOnlySpan<byte> prefix = "M16_005_WRITE fd="u8;
            Need(rawTail[at..].StartsWith(prefix), "envelope");
            int channelAt = at + prefix.Length;
            Need(channelAt < rawTail.Length && (rawTail[channelAt] == (byte)'1' || web && rawTail[channelAt] == (byte)'2'), "envelope");
            uint channel = (uint)(rawTail[channelAt] - (byte)'0');
            int numberAt = channelAt + 1;
            Need(rawTail[numberAt..].StartsWith(" bytes="u8), "envelope");
            numberAt += 7;
            Need(numberAt < rawTail.Length && rawTail[numberAt] is >= (byte)'1' and <= (byte)'9', "length");
            int bytes = 0;
            while (numberAt < rawTail.Length && rawTail[numberAt] is >= (byte)'0' and <= (byte)'9')
            {
                bytes = bytes * 10 + rawTail[numberAt++] - (byte)'0';
                Need(bytes <= writeLimit, "length");
            }

            Need(rawTail[numberAt..].StartsWith(" data="u8), "envelope");
            int start = numberAt + 6;
            int remaining = rawTail.Length - start - 2;
            NeedSize(bytes <= remaining, "payload exceeds remaining", start, bytes, remaining, total, errors, lengths.Count, mode, remaining);
            NeedSize(bytes <= limit - total - errors, "combined stdout+stderr", start, bytes, remaining, total, errors, lengths.Count, mode, limit);
            NeedSize(lengths.Count < 4096, "write count", start, bytes, remaining, total, errors, lengths.Count, mode, 4096);
            int end = start + bytes;
            Need(rawTail[end] == (byte)'\r' && rawTail[end + 1] == (byte)'\n', "framing");
            rawTail[start..end].CopyTo(channel == 1 ? stdout.AsSpan(total) : stderr.AsSpan(errors));
            if (channel == 1)
                total += bytes;
            else
                errors += bytes;
            lengths.Add(bytes);
            channels.Add(channel);
            // Keep the kernel prefix, every unrelated row and the kernel CRLF.
            rawTail[cursor..start].CopyTo(normalized.AsSpan(written));
            written += start - cursor;
            if (!web)
                for (int i = start; i < end; i++)
                    if (rawTail[i] != (byte)'\r' && rawTail[i] != (byte)'\n')
                        normalized[written++] = rawTail[i];
            normalized[written++] = rawTail[end];
            normalized[written++] = rawTail[end + 1];
            cursor = end + 2;
        }

        string text = Utf8.GetString(stdout.AsSpan(0, total));
        string tail = Utf8.GetString(normalized.AsSpan(0, written));
        return new(text, tail, lengths.ToArray(), channels.ToArray(), Utf8.GetString(stderr.AsSpan(0, errors)));
    }

    /// <summary>Require the original, unchanged runtime smoke's five ordered
    /// output rows. The exact ELF owns its EH, GC, TLS, task and timer assertions;
    /// this text alone proves no scheduler activity, Linux or guest execution.
    /// Borrowed LF/CRLF text must be complete; substitution or extra rows throw.</summary>

    public static void ValidateRuntimeSmokeOutput(string text)
    {
        Need(text.Length is> 0 and <= 4096, "runtime_smoke_size");
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(normalized == "TEST_START nativeaot.runtime\nNATIVEAOT_RUNTIME_EH_OK\n" + "NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK\nNATIVEAOT_RUNTIME_TASK_TIMER_OK\nTEST_PASS nativeaot.runtime\n", "runtime_smoke_output");
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("nativeaot_threading_raw_" + reason);
    }

    // Failure-only host diagnostics. Keep the existing public reason and all
    // bounds; retain scalar counters without copying any payload into the error.
    private static void NeedSize(bool accepted, string condition, int offset, int declared, int remaining, int stdout, int stderr, int writes, string mode, int limit)
    {
        if (accepted)
            return;
        var error = new ToolFailure("nativeaot_threading_raw_size");
        error.Data["condition"] = condition;
        error.Data["inputOffset"] = offset;
        error.Data["declaredPayloadLength"] = declared;
        error.Data["bytesRemaining"] = remaining;
        error.Data["stdoutBytes"] = stdout;
        error.Data["stderrBytes"] = stderr;
        error.Data["writeCount"] = writes;
        error.Data["selectedMode"] = mode;
        error.Data["applicableLimit"] = limit;
        throw error;
    }
}
