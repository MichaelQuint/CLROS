using System;
using OS.Boot;

namespace OS.Kernel.Runtime;

public readonly struct BoundedExceptionCallerWindowResult
{
    internal BoundedExceptionCallerWindowResult(BoundedExceptionCallerWindowStatus status)
    {
        this = default;
        Status = status;
    }

    internal BoundedExceptionCallerWindowResult(uint frames, uint rows)
    {
        Status = BoundedExceptionCallerWindowStatus.Success;
        FrameCount = frames;
        RowCount = rows;
    }

    public BoundedExceptionCallerWindowStatus Status
    {
        get;
    }
    public bool Succeeded => Status == BoundedExceptionCallerWindowStatus.Success;
    public uint FrameCount
    {
        get;
    }
    public uint RowCount
    {
        get;
    }
}
