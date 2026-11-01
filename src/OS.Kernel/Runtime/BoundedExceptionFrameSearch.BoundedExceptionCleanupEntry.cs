using System;

namespace OS.Kernel.Runtime;
public readonly struct BoundedExceptionCleanupEntry
{
    internal BoundedExceptionCleanupEntry(uint frameIndex, ulong handler, X64UnwindContext context)
    {
        FrameIndex = frameIndex;
        Handler = handler;
        Context = context;
    }

    public uint FrameIndex { get; }
    public ulong Handler { get; }
    public X64UnwindContext Context { get; }
}
