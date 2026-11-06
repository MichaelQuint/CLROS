using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Optional accumulating receipt for the latest actual request.
    /// Exclusive ordinary RAM belongs to the serialized supervisor until Stop
    /// succeeds. Sample/copy it after EVERY block call or single engine Pump:
    /// it is one slot, not a log, and the next publication replaces it. One
    /// engine Pump can submit one generation OR poll/copy its existing one,
    /// never both. Missing samples grant no evidence; consumers reject gaps.
    /// Data is exact published OUT bytes, or successful committed IN copy bytes;
    /// FLUSH/errors/uncopied IN have no payload. CompletionValidated is the
    /// validated device result, distinct from cancellation and caller delivery.
    /// FLUSH frontiers acknowledge the device protocol, not end-to-end storage
    /// durability. No callback, execution authority or concurrent reader exists.
    /// Stop preserves this past receipt; its owner scrubs/releases it afterward.</summary>
    public struct Observation
    {
        public RequestToken Request;
        public Operation Kind;
        public ulong Sector, SubmittedCount, CompletedCount, SubmittedBytes, CompletedBytes, FlushTargetBytes, FlushAcknowledgedBytes;
        public ushort AvailableIndex, ConsumedIndex;
        public uint PayloadBytes, CompletionBytes;
        public Status DeviceResult;
        public ObservationFlags Flags;
        public bool Cancelled;
        public fixed byte Data[512];
    }
}
