using OS.Manifest;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppPayloadMappingPlan
{
    public NativeAppPayloadMappingPlan(ulong payloadOffsetInPackage, ulong payloadLength, ulong headerLength, ulong executableOffsetInPayload, ulong executableOffsetInPackage, ulong executableLength, ulong entryOffsetInPayload, ulong entryOffsetInExecutable, ulong entryOffsetInPackage)
    {
        PayloadOffsetInPackage = payloadOffsetInPackage;
        PayloadLength = payloadLength;
        HeaderLength = headerLength;
        ExecutableOffsetInPayload = executableOffsetInPayload;
        ExecutableOffsetInPackage = executableOffsetInPackage;
        ExecutableLength = executableLength;
        EntryOffsetInPayload = entryOffsetInPayload;
        EntryOffsetInExecutable = entryOffsetInExecutable;
        EntryOffsetInPackage = entryOffsetInPackage;
    }

    public ulong PayloadOffsetInPackage { get; }
    public ulong PayloadLength { get; }
    public ulong HeaderLength { get; }
    public ulong ExecutableOffsetInPayload { get; }
    public ulong ExecutableOffsetInPackage { get; }
    public ulong ExecutableLength { get; }
    public ulong EntryOffsetInPayload { get; }
    public ulong EntryOffsetInExecutable { get; }
    public ulong EntryOffsetInPackage { get; }
}
