using System;

namespace OS.Kernel.Runtime;
public readonly struct BoundedExceptionDispatchResult
{
    internal BoundedExceptionDispatchResult(BoundedExceptionDispatchStatus status)
    {
        this = default;
        Status = status;
    }

    internal BoundedExceptionDispatchResult(uint index, ulong handler, uint cleanups, ulong first, ulong second)
    {
        Status = BoundedExceptionDispatchStatus.Success;
        CatchIndex = index;
        CatchHandler = handler;
        CleanupCount = cleanups;
        FirstCleanup = first;
        SecondCleanup = second;
    }

    public BoundedExceptionDispatchStatus Status { get; }
    public bool Succeeded => Status == BoundedExceptionDispatchStatus.Success;
    public uint CatchIndex { get; }
    public ulong CatchHandler { get; }
    public uint CleanupCount { get; }
    public ulong FirstCleanup { get; }
    public ulong SecondCleanup { get; }
}
