namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public sealed partial class WebSignalRaceReport
    {
        /// <summary>Actual replay identity and FS at the selected gettid CALL;
        /// native lifecycle rows are optional only for legacy profiles and
        /// framework-owned callback/recovery-writer threads.</summary>

        public readonly record struct Thread(ulong Identity, int Tid, ulong Fs, int GetTidRecordIndex, int? CloneRecordIndex, int? ExitRecordIndex, int? ReapRecordIndex);
    }
}
