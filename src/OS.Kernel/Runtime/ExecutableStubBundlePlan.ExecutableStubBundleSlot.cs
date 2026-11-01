namespace OS.Kernel.Runtime;
public readonly struct ExecutableStubBundleSlot
{
    public ExecutableStubBundleSlot(ExecutableStubSlotKind kind, nuint byteLength)
    {
        Kind = kind;
        ByteLength = byteLength;
    }

    public ExecutableStubSlotKind Kind { get; }
    public nuint ByteLength { get; }
}
