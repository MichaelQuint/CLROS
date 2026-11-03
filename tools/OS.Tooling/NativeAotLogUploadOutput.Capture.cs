namespace OS.Tooling;
public static partial class NativeAotLogUploadOutput
{
    /// <summary>Immutable copied text, original write segmentation and expected
    /// file values. The structural tail retains every non-payload byte, including
    /// incomplete/unverified kernel rows. No input array or file is retained.</summary>

    public sealed class Capture
    {
        private readonly NativeAotThreadingOutput.Capture framing;
        internal Capture(NativeAotThreadingOutput.Capture framing, NativeAotLogUploadContract.Transcript transcript)
        {
            this.framing = framing;
            Phase = transcript.Phase;
            ExpectedFiles = transcript.ExpectedFiles;
        }

        public string Phase { get; }
        public NativeAotLogUploadContract.FileValues ExpectedFiles { get; }
        public string Stdout => framing.Stdout;
        public string NormalizedTail => framing.NormalizedTail;
        public IReadOnlyList<int> WriteLengths => framing.WriteLengths;
        public IReadOnlyList<uint> WriteChannels => framing.WriteChannels;
        public bool GrantsSourceSchedulerGuestOrPersistenceAuthority => false;
    }
}
