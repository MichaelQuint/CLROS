using System.Globalization;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using B = OS.Storage.WritableBlockSession;
using Fixture = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public sealed class WritableBlockValueReport
    {
        internal WritableBlockValueReport(Fixture.BlockBoundary before, Fixture.BlockBoundary after, List<WritableDiskIo> io, List<WritableDiskRow> states, bool failed)
        {
            Before = before;
            After = after;
            Io = Array.AsReadOnly(io.ToArray());
            States = Array.AsReadOnly(states.ToArray());
            HasStateFailure = failed;
        }

        public Fixture.BlockBoundary Before { get; }
        public Fixture.BlockBoundary After { get; }
        public IReadOnlyList<WritableDiskIo> Io { get; }
        public IReadOnlyList<WritableDiskRow> States { get; }
        public bool HasCancelled => Io.Any(x => x.Cancelled);
        public bool HasDeviceError => Io.Any(x => x.IsComplete && x.Status != B.Status.Success);
        public bool HasStateFailure { get; }
        public bool HasUncompletedPublication => Io.Count != 0 && !Io[^1].IsComplete;
        public bool HasHeldRead => Io.Count != 0 && Io[^1] is { Kind: B.Operation.Read, Status: B.Status.Success, IsComplete: true, IsCopied: false, Cancelled: false };
        public bool Dirty => After.SubmittedBytes != After.FlushAcknowledgedBytes;
        public bool GrantsDeviceOrPersistenceAuthority => false;
    }
}
