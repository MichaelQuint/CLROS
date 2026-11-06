using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Optional synchronous supervisor observation; null disables it.
    /// Return exactly1 to accept. Context/code remain mapped and stable through
    /// Stop. Perform bounded work in the serialized supervisor context; never
    /// throw, reenter or mutate this owner or the borrowed bytes. Metadata and
    /// payload are borrowed only until return; copy facts or hash synchronously.
    /// Payload is null except CopyReady, which borrows exactlyObservation.Bytes validated DMA
    /// bytes before any destination write. Rejection faults without releasing DMA
    /// or publishing success. Fault/Stop never invoke this callback.</summary>
    public struct ObservationAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, Observation*, byte*, byte> Observe;
    }
}
