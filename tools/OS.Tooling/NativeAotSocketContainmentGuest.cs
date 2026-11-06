namespace OS.Tooling;
/// <summary>Value-only decoding of the containment control's raw stdout,
/// epoll observations and full transport lifetimes. This does not validate a
/// complete syscall/scheduler/descriptor/packet lifecycle or grant execution,
/// package, original-program or guest acceptance authority.</summary>

public static partial class NativeAotSocketContainmentGuest
{
    /// <summary>Borrow an inner M16-009 raw tail, decode exact bounded fd1 byte
    /// envelopes and the separate six-row app output, then validate observed
    /// wait generations/sequences and the three transport identities. Caller
    /// bytes remain unchanged and are not retained. Invalid framing/content
    /// throws ToolFailure; invalid UTF8 throws DecoderFallbackException. This
    /// method neither runs nor accepts the guest case.</summary>

    public static Capture Read(ReadOnlySpan<byte> rawTail)
    {
        var output = NativeAotThreadingOutput.Read(NativeAotSocketContainmentPublisher.Application, rawTail);
        var scheduling = NativeAotSocketGuest.ReadReadiness(output);
        var application = NativeAotSocketContainmentOutput.Read(output.Stdout);
        string[] rows = output.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        return new(scheduling, application, QemuTranscripts.ReadSocketContainmentTransport(rows));
    }
}
