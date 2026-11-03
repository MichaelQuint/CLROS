namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public sealed partial class WebSignalRaceReport
    {
        /// <summary>Generation-bound original creation and explicit close rows.</summary>

        public readonly record struct DescriptorObservation(int Descriptor, uint Generation, int CreatedRecordIndex, int ClosedRecordIndex);
    }
}
