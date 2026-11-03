using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotLayoutResult
{
    internal NativeAotLayoutResult(NativeAotLayoutStatus status, NativeAotLayoutShape shape, ushort componentSize, bool containsGcPointers, int lengthOffsetFromReference, int dataOffsetFromReference, ulong rawSizeBytes, ulong alignedSizeBytes)
    {
        Status = status;
        Shape = shape;
        ComponentSize = componentSize;
        ContainsGcPointers = containsGcPointers;
        LengthOffsetFromReference = lengthOffsetFromReference;
        DataOffsetFromReference = dataOffsetFromReference;
        RawSizeBytes = rawSizeBytes;
        AlignedSizeBytes = alignedSizeBytes;
    }

    public NativeAotLayoutStatus Status
    {
        get;
    }
    public NativeAotLayoutShape Shape
    {
        get;
    }
    public ushort ComponentSize
    {
        get;
    }
    public bool ContainsGcPointers
    {
        get;
    }
    public int LengthOffsetFromReference
    {
        get;
    }
    public int DataOffsetFromReference
    {
        get;
    }
    public ulong RawSizeBytes
    {
        get;
    }
    public ulong AlignedSizeBytes
    {
        get;
    }
    public bool Succeeded => Status == NativeAotLayoutStatus.Success;
}
