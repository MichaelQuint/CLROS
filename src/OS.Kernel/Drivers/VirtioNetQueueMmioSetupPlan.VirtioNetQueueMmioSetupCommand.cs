using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioNetQueueMmioSetupCommand
{
    internal VirtioNetQueueMmioSetupCommand(VirtioNetQueueMmioSetupCommandKind kind, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong value)
    {
        Kind = kind;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
        Value = value;
    }

    public VirtioNetQueueMmioSetupCommandKind Kind { get; }
    public ushort QueueIndex { get; }
    public uint RelativeOffset { get; }
    public uint WidthBytes { get; }
    public ulong Value { get; }
    public bool ReadsRegister => Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize || Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset;
    public bool WritesRegister => !ReadsRegister;
}
