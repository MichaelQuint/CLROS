using System.Collections.ObjectModel;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Complete copied source5 envelopes only. Calls, EnvelopeRows and
    /// ResidualRows partition every original index exactly once. Source4 wakes
    /// are shape-checked but remain explicitly unjoined residuals. Known device
    /// rows and unrelated outer rows require the whole original consumer;
    /// unknown M16_027 records reject rather than entering the residual census.
    /// Neither these capacities nor this report authenticate resources.</summary>

    public sealed class NativeAotLogUploadWaitReport
    {
        internal NativeAotLogUploadWaitReport(int rows, uint descriptors, uint output, List<NativeAotLogUploadWaitRow> calls, List<NativeAotLogUploadWaitRow> envelopes, List<NativeAotLogUploadWaitOperation> operations, List<WritableDiskRow> residual, List<NativeAotLogUploadWaitRow> readonlyWakes)
        {
            OriginalRowCount = rows;
            DescriptorCapacity = descriptors;
            CopiedOutputCapacity = output;
            Calls = Array.AsReadOnly(calls.ToArray());
            EnvelopeRows = Array.AsReadOnly(envelopes.ToArray());
            Operations = Array.AsReadOnly(operations.ToArray());
            ResidualRows = Array.AsReadOnly(residual.ToArray());
            UnjoinedSource4Wakes = Array.AsReadOnly(readonlyWakes.ToArray());
        }

        public int OriginalRowCount
        {
            get;
        }
        public uint DescriptorCapacity
        {
            get;
        }
        public uint CopiedOutputCapacity
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadWaitRow> Calls
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadWaitRow> EnvelopeRows
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadWaitOperation> Operations
        {
            get;
        }
        public IReadOnlyList<WritableDiskRow> ResidualRows
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadWaitRow> UnjoinedSource4Wakes
        {
            get;
        }
        public bool GrantsSchedulerAuthority => false;
        public bool GrantsPackageOrProcessAuthority => false;
        public bool GrantsGuestOrNativeAuthority => false;
        public bool GrantsWholeCaptureQualification => false;
        public bool GrantsPhysicalOrPersistenceAuthority => false;
    }
}
