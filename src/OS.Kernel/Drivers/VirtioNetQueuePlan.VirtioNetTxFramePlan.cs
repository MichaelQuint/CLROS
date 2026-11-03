namespace OS.Drivers;

public readonly struct VirtioNetTxFramePlan
{
    internal VirtioNetTxFramePlan(uint frameBufferBytes, uint ethernetFrameBytes)
    {
        HeaderBufferBytes = VirtioNetQueuePlan.VirtioNetHeaderLength;
        EthernetFrameBytes = ethernetFrameBytes;
        RequiredBufferBytes = frameBufferBytes;
    }

    public uint HeaderBufferBytes
    {
        get;
    }
    public uint EthernetFrameBytes
    {
        get;
    }
    public uint RequiredBufferBytes
    {
        get;
    }
    public ushort RequiredDescriptorCount => VirtioNetQueuePlan.RequiredTxDescriptorCount;
    public VirtioNetDescriptorAccessDirection HeaderDescriptorAccessDirection => VirtioNetDescriptorAccessDirection.DeviceReadable;
    public VirtioNetDescriptorAccessDirection FrameDescriptorAccessDirection => VirtioNetDescriptorAccessDirection.DeviceReadable;
}
