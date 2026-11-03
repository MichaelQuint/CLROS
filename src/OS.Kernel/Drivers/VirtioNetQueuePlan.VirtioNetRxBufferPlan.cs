namespace OS.Drivers;

public readonly struct VirtioNetRxBufferPlan
{
    internal VirtioNetRxBufferPlan(ushort bufferCount, uint frameBufferBytes, uint requiredBufferBytes)
    {
        BufferCount = bufferCount;
        FrameBufferBytes = frameBufferBytes;
        RequiredBufferBytes = requiredBufferBytes;
    }

    public ushort BufferCount
    {
        get;
    }
    public uint FrameBufferBytes
    {
        get;
    }
    public uint RequiredBufferBytes
    {
        get;
    }
    public ushort RequiredDescriptorCount => BufferCount;
    public VirtioNetDescriptorAccessDirection DescriptorAccessDirection => VirtioNetDescriptorAccessDirection.DeviceWritable;
}
