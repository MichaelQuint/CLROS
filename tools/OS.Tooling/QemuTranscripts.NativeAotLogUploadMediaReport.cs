using B = OS.Storage.WritableBlockSession;
using Combined = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using F = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable copied media consistency and independent extractions.
    /// BlockRows and UnjoinedRows partition the supplied original positions.
    /// The inclusive window and capacity are values whose original provenance
    /// the caller must authenticate. No raw image buffer is retained.</summary>

    public sealed class NativeAotLogUploadMediaReport
    {
        internal NativeAotLogUploadMediaReport(string phase, string packageHash, int rows, int first, int last, WritableBlockValueReport block, List<WritableDiskRow> selected, List<WritableDiskRow> residual, Combined.Inspection initial, Combined.Inspection final)
        {
            Phase = phase;
            PackageSha256 = packageHash;
            OriginalRowCount = rows;
            FirstRecordIndex = first;
            LastRecordIndex = last;
            Block = block;
            BlockRows = Array.AsReadOnly(selected.ToArray());
            UnjoinedRows = Array.AsReadOnly(residual.ToArray());
            Initial = initial;
            Final = final;
            ObservedOutSectors = Array.AsReadOnly(block.Io.Where(row => row.Kind == B.Operation.Write).Select(row => row.Sector).Distinct().Order().ToArray());
        }

        public string Phase
        {
            get;
        }
        public string PackageSha256
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
        public WritableBlockValueReport Block
        {
            get;
        }
        public IReadOnlyList<WritableDiskRow> BlockRows
        {
            get;
        }
        public IReadOnlyList<WritableDiskRow> UnjoinedRows
        {
            get;
        }
        public IReadOnlyList<ulong> ObservedOutSectors
        {
            get;
        }
        public Combined.Inspection Initial
        {
            get;
        }
        public Combined.Inspection Final
        {
            get;
        }
        public bool SyscallInputQualified => false;
        public bool OriginalCreatorOrProcessQualified => false;
        public bool NativeOrGuestQualified => false;
        public bool RebootOrPhysicalPersistenceQualified => false;
        public bool WholeCaptureQualified => false;
    }
}
