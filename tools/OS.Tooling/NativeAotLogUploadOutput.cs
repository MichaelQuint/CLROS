namespace OS.Tooling;

/// <summary>Host-only decoding of the console log/upload candidate's copied
/// serial bytes. Exact byte framing and complete phase output are checked;
/// kernel rows, source, instructions, scheduling and original execution are
/// separate enclosing-owner obligations.</summary>
public static class NativeAotLogUploadOutput
{
    public const int MaximumRawBytes = 8 * 1024 * 1024;

    /// <summary>Immutable copied text, original write segmentation and expected
    /// file values. The structural tail retains every non-payload byte, including
    /// incomplete/unverified kernel rows. No input array or file is retained.</summary>
    public sealed class Capture
    {
        private readonly NativeAotThreadingOutput.Capture framing;
        internal Capture(NativeAotThreadingOutput.Capture framing, NativeAotLogUploadContract.Transcript transcript)
        { this.framing = framing; Phase = transcript.Phase; ExpectedFiles = transcript.ExpectedFiles; }
        public string Phase { get; }
        public NativeAotLogUploadContract.FileValues ExpectedFiles { get; }
        public string Stdout => framing.Stdout;
        public string NormalizedTail => framing.NormalizedTail;
        public IReadOnlyList<int> WriteLengths => framing.WriteLengths;
        public IReadOnlyList<uint> WriteChannels => framing.WriteChannels;
        public bool GrantsSourceSchedulerGuestOrPersistenceAuthority => false;
    }

    /// <summary>Synchronously borrow at most8MiB of stable raw serial bytes for
    /// explicit write or verify. Reuse the bounded fd1/fd2 byte framer, reassemble
    /// UTF8 across write boundaries, require empty fd2 and the exact phase
    /// transcript, then retain copied output/segmentation only. Payload bytes
    /// cannot become structural rows. Invalid phase, framing, limits or content
    /// throw ToolFailure; invalid UTF8 throws DecoderFallbackException. Success
    /// neither checks unrelated kernel rows nor admits web, package, process,
    /// scheduler, device, original capture, guest or persistence authority.</summary>
    public static Capture Read(ReadOnlySpan<byte> rawTail, string phase)
    {
        if (phase is not ("write" or "verify")) throw new ToolFailure("log_upload_output_phase");
        if (rawTail.Length is 0 or > MaximumRawBytes) throw new ToolFailure("log_upload_output_size");
        var framed = NativeAotThreadingOutput.ReadWeb(rawTail);
        if (framed.Stderr.Length != 0 || framed.WriteChannels.Any(channel => channel != 1))
            throw new ToolFailure("log_upload_output_stderr");
        var transcript = NativeAotLogUploadContract.InspectTranscriptValues(framed.Stdout, phase);
        return new(framed, transcript);
    }
}
