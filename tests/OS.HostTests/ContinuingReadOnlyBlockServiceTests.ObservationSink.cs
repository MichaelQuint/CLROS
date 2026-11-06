using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    private struct ObservationSink
    {
        internal Block.State* State;
        internal Hardware* Hardware;
        internal Block.Observation* Records;
        internal byte* Payload;
        internal byte* Output;
        internal byte* Metadata;
        internal Block.Admission Input;
        internal ulong Candidate, LastGeneration;
        internal uint Count;
        internal uint ExpectedBytes;
        internal int ReenterOperation;
        internal Block.ObservationKind RejectKind, ReenterKind;
        internal byte RejectValue;
        internal Block.Status NestedStatus;
        internal bool Bad, Reject, NestedUnchanged;
    }
}
