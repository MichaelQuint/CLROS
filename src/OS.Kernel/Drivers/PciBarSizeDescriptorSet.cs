namespace OS.Drivers;

public readonly struct PciBarSizeDescriptorSet
{
    public PciBarSizeDescriptorSet(PciBarSizeDescriptor bar0, PciBarSizeDescriptor bar1, PciBarSizeDescriptor bar2, PciBarSizeDescriptor bar3, PciBarSizeDescriptor bar4, PciBarSizeDescriptor bar5)
    {
        Bar0 = bar0;
        Bar1 = bar1;
        Bar2 = bar2;
        Bar3 = bar3;
        Bar4 = bar4;
        Bar5 = bar5;
    }

    public PciBarSizeDescriptor Bar0
    {
        get;
    }
    public PciBarSizeDescriptor Bar1
    {
        get;
    }
    public PciBarSizeDescriptor Bar2
    {
        get;
    }
    public PciBarSizeDescriptor Bar3
    {
        get;
    }
    public PciBarSizeDescriptor Bar4
    {
        get;
    }
    public PciBarSizeDescriptor Bar5
    {
        get;
    }

    public bool TryGet(byte index, out PciBarSizeDescriptor descriptor)
    {
        switch (index)
        {
            case 0:
                descriptor = Bar0;
                return true;
            case 1:
                descriptor = Bar1;
                return true;
            case 2:
                descriptor = Bar2;
                return true;
            case 3:
                descriptor = Bar3;
                return true;
            case 4:
                descriptor = Bar4;
                return true;
            case 5:
                descriptor = Bar5;
                return true;
            default:
                descriptor = default;
                return false;
        }
    }

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        if (destination.Length < PciBarDecoder.BarCount)
        {
            return false;
        }

        destination[0] = Bar0.SizeBytes;
        destination[1] = Bar1.SizeBytes;
        destination[2] = Bar2.SizeBytes;
        destination[3] = Bar3.SizeBytes;
        destination[4] = Bar4.SizeBytes;
        destination[5] = Bar5.SizeBytes;
        return true;
    }
}
