using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;
public static partial class LinuxWritableDiskProofFixture
{
    /// <summary>Copied protocol inspection only. Boundaries and sample contents
    /// are caller-supplied values; success grants no original-image, process,
    /// source, device, reset/retry, cleanup or persistence authority.</summary>

    public sealed class BlockObservationInspection
    {
        internal BlockObservationInspection(BlockBoundary before, BlockBoundary after, int count, BlockRequestFacts[] requests)
        {
            Before = before;
            After = after;
            SampleCount = count;
            Requests = Array.AsReadOnly(requests);
        }

        public BlockBoundary Before { get; }
        public BlockBoundary After { get; }
        public int SampleCount { get; }
        public IReadOnlyList<BlockRequestFacts> Requests { get; }
        public bool HasUncompletedPublication => Requests.Count != 0 && !Requests[^1].CompletionValidated;
        public bool HasHeldRead => Requests.Count != 0 && Requests[^1].HasHeldRead;
        public bool HasDeviceError => Requests.Any(r => r.CompletionValidated && r.DeviceResult != Block.Status.Success);
        public bool Dirty => After.SubmittedBytes != After.FlushAcknowledgedBytes;
        public bool ObservedProtocolDrained => !HasUncompletedPublication && !HasHeldRead;
        public bool GrantsDeviceOrPersistenceAuthority => false;
    }
}
