namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public sealed partial class WebSignalRaceReport
    {
        /// <summary>Original row order; zero ordinal/sequence means inapplicable.</summary>

        public readonly record struct Step(string Kind, int RecordIndex, ulong Identity, ulong Ordinal, ulong Sequence);
    }
}
