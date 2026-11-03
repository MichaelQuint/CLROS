using System;

namespace OS.Kernel.Runtime;
/// <summary>One caller-authenticated physical frame and its contiguous decoded EH rows.</summary>

public readonly struct BoundedExceptionSearchFrame
{
    public BoundedExceptionSearchFrame(X64UnwindContext context, uint protectedOffset, uint codeBytes, uint rowOffset, uint rowCount)
    {
        Context = context;
        ProtectedOffset = protectedOffset;
        CodeBytes = codeBytes;
        RowOffset = rowOffset;
        RowCount = rowCount;
    }

    public X64UnwindContext Context
    {
        get;
    }
    public uint ProtectedOffset
    {
        get;
    }
    public uint CodeBytes
    {
        get;
    }
    public uint RowOffset
    {
        get;
    }
    public uint RowCount
    {
        get;
    }
}
