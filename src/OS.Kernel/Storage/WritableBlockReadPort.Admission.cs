using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    /// <summary>Stable original engine/device and independently authorized
    /// inclusive readonly partition bounds. All epochs are nonzero and Epoch
    /// is unique for this zero-State lifetime. The caller holds engine/block,
    /// pool/DMA/MMIO/observer and port backing until port Stop succeeds, before
    /// engine Stop. There is no retained destination or hidden allocation.</summary>
    public struct Admission
    {
        public C.State* Engine;
        public B.State* Device;
        public ulong Epoch, EngineEpoch, DeviceEpoch, FirstLba, LastLba;
    }
}
