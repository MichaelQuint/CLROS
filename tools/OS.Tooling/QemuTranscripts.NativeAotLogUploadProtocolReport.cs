using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable copied scheduling relationships. Relative FIFO covers
    /// only observed source5 wakes; other runnable threads/futexes/pipes still
    /// require the enclosing ThreadingProtocol replay. Rows and residuals form
    /// one complete original-position census. No file or input lease survives.</summary>

    public sealed class NativeAotLogUploadProtocolReport
    {
        internal NativeAotLogUploadProtocolReport(string phase, string? packageHash, int count, int first, int last, NativeAotLogUploadWaitReport waits, List<WritableDiskRow> rows, List<WritableDiskRow> residual, List<NativeAotLogUploadProtocolTurn> turns, int transfers, int drains, int reaped, bool output)
        {
            Phase = phase;
            PackageSha256 = packageHash;
            OriginalRowCount = count;
            FirstRecordIndex = first;
            LastRecordIndex = last;
            Waits = waits;
            ProtocolRows = Array.AsReadOnly(rows.ToArray());
            ResidualRows = Array.AsReadOnly(residual.ToArray());
            WritableTurns = Array.AsReadOnly(turns.ToArray());
            Transfers = transfers;
            Drains = drains;
            Reaped = reaped;
            CheckedPhaseOutputValues = output;
        }

        public string Phase
        {
            get;
        }
        public string? PackageSha256
        {
            get;
        }
        public int OriginalRowCount
        {
            get;
        }
        public int FirstRecordIndex
        {
            get;
        }
        public int LastRecordIndex
        {
            get;
        }
        public NativeAotLogUploadWaitReport Waits
        {
            get;
        }
        public IReadOnlyList<WritableDiskRow> ProtocolRows
        {
            get;
        }
        public IReadOnlyList<WritableDiskRow> ResidualRows
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadProtocolTurn> WritableTurns
        {
            get;
        }
        public int Transfers
        {
            get;
        }
        public int Drains
        {
            get;
        }
        public int Reaped
        {
            get;
        }
        public bool CheckedPhaseOutputValues
        {
            get;
        }
        public bool CheckedSource5RelativeFifoValues => true;
        public bool CheckedFullSchedulerValues => false;
        public bool GrantsOriginalInstructionAuthority => false;
        public bool GrantsSchedulerAuthority => false;
        public bool GrantsProcessAuthority => false;
        public bool SyscallInputQualified => false;
        public bool NativeOrGuestQualified => false;
        public bool RebootOrPhysicalPersistenceQualified => false;
        public bool WholeCaptureQualified => false;
    }
}
