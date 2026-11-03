using System.Globalization;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using B = OS.Storage.WritableBlockSession;
using Fixture = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Finite copied-value joins only. All known auxiliary leaf rows
    /// retain original indexes. The original owner must independently authenticate
    /// the full input, index census, firmware/native/process/IRQ/network/packet
    /// provenance and joined child lifetime. None of those authorities follows.</summary>

    public sealed class WritableDiskValueReport
    {
        internal WritableDiskValueReport(WritableBlockValueReport block, List<WritableDiskWait> waits, List<WritableDiskRow> auxiliary, int calls, int transfers, ulong epochs, bool images)
        {
            Block = block;
            Waits = Array.AsReadOnly(waits.ToArray());
            Auxiliary = Array.AsReadOnly(auxiliary.OrderBy(x => x.RecordIndex).ToArray());
            Calls = calls;
            Transfers = transfers;
            Epochs = epochs;
            CheckedImageValues = images;
        }

        public WritableBlockValueReport Block { get; }
        public IReadOnlyList<WritableDiskWait> Waits { get; }
        public IReadOnlyList<WritableDiskRow> Auxiliary { get; }
        public int Calls { get; }
        public int Transfers { get; }
        public ulong Epochs { get; }
        public bool CheckedCallAndWaitValues => true;
        public bool CheckedAuxiliaryJoins => false;
        public bool CheckedImageValues { get; }
        public bool NativeQualified => false;
        public bool FirmwareQualified => false;
        public bool ProcessQualified => false;
        public bool PacketQualified => false;
        public bool PhysicalPersistenceQualified => false;
        public bool RebootQualified => false;
        public bool WholeCaptureQualified => false;
    }
}
