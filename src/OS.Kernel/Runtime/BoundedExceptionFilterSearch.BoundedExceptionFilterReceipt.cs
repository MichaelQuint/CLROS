using System;

namespace OS.Kernel.Runtime;
/// <summary>A caller-supplied result of one synchronous original filter invocation.</summary>
/// <remarks>Default means pending. Numeric fields do not authenticate invocation or exception identity.</remarks>

public readonly struct BoundedExceptionFilterReceipt
{
    public BoundedExceptionFilterReceipt(uint clauseIndex, ulong filterAddress, int returnValue)
    {
        HasValue = true;
        ClauseIndex = clauseIndex;
        FilterAddress = filterAddress;
        ReturnValue = returnValue;
    }

    public bool HasValue { get; }
    public uint ClauseIndex { get; }
    public ulong FilterAddress { get; }
    public int ReturnValue { get; }
}
