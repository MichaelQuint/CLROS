using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupCommand
{
    internal VirtioBlockQueueMmioSetupCommand(
        VirtioBlockQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        Kind = kind;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
        Value = value;
    }

    public VirtioBlockQueueMmioSetupCommandKind Kind
    {
        get;
    }

    public ushort QueueIndex
    {
        get;
    }

    public uint RelativeOffset
    {
        get;
    }

    public uint WidthBytes
    {
        get;
    }

    public ulong Value
    {
        get;
    }

    public bool ReadsRegister =>
        Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize ||
        Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset;

    public bool WritesRegister => !ReadsRegister;
}
