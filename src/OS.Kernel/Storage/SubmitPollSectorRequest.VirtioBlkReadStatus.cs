using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlkReadStatus : byte
{
    Uninitialized, Success, InvalidAccess, InvalidDestination, ReadFailed, WriteFailed,
    ConfigurationChanged, InvalidCapacity, SectorOutOfRange, QueueRejected, InvalidNotify,
    StatusRejected, OrderingFailed, BusMasterFailed, InvalidCompletion, DeviceIoError,
    UnsupportedRequest, Timeout, InterruptPrepareFailed, InterruptWaitFailed, InterruptShutdownFailed
}
