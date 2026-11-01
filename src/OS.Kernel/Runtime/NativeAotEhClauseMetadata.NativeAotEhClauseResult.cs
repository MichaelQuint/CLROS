using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;
public readonly struct NativeAotEhClauseResult
{
    internal NativeAotEhClauseResult(NativeAotEhClauseStatus status)
    {
        this = default;
        Status = status;
    }

    internal NativeAotEhClauseResult(uint index, uint count, uint start, uint end, NativeAotEhClauseKind kind, ulong handler, ulong filter, ulong type, bool boundary)
    {
        Status = NativeAotEhClauseStatus.Success;
        Index = index;
        Count = count;
        TryStartOffset = start;
        TryEndOffset = end;
        Kind = kind;
        HandlerAddress = handler;
        FilterAddress = filter;
        TypeAddress = type;
        IsTryGroupBoundary = boundary;
    }

    public NativeAotEhClauseStatus Status { get; }
    public bool Succeeded => Status == NativeAotEhClauseStatus.Success;
    public uint Index { get; }
    public uint Count { get; }
    public uint TryStartOffset { get; }
    public uint TryEndOffset { get; }
    public NativeAotEhClauseKind Kind { get; }
    public ulong HandlerAddress { get; }
    public ulong FilterAddress { get; }
    public ulong TypeAddress { get; }
    public bool IsTryGroupBoundary { get; }

    public bool ContainsOffset(uint offset) => Succeeded && offset >= TryStartOffset && offset < TryEndOffset;
}
