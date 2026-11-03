using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    /// <summary>Copied current port geometry and identity for a typed filesystem
    /// admission. Success authenticates the original clean owner, including this
    /// port's pending/held IN. It grants no mapping, partition provenance or user
    /// copy authority. Busy/fault/retired inspection publishes no facts.</summary>
    public readonly struct Inspection
    {
        public readonly ulong Owner, Epoch, EngineOwner, EngineEpoch, DeviceOwner, DeviceEpoch, SectorCount, FirstLba, LastLba;
        public readonly bool RequestActive, ReadReady;
        internal Inspection(State* s)
        {
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            EngineOwner = (ulong)s->Input.Engine;
            EngineEpoch = s->Input.EngineEpoch;
            DeviceOwner = (ulong)s->Input.Device;
            DeviceEpoch = s->Input.DeviceEpoch;
            SectorCount = s->Input.Device->SectorCount;
            FirstLba = s->Input.FirstLba;
            LastLba = s->Input.LastLba;
            RequestActive = s->HasIo;
            ReadReady = s->Phase == ReadyPhase;
        }
    }
}
