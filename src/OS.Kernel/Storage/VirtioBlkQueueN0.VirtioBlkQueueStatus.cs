using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlkQueueStatus : byte
{
    Uninitialized, Success, InvalidAccess, ReadFailed, WriteFailed, StatusRejected,
    InvalidQueue, DmaRejected, Overlap, ReadbackMismatch, OrderingFailed, BusMasterFailed, ResetFailed
}
