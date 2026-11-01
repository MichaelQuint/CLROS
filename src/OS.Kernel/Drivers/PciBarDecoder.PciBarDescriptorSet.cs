namespace OS.Drivers;
public readonly struct PciBarDescriptorSet
{
    public PciBarDescriptorSet(PciBarDescriptor bar0, PciBarDescriptor bar1, PciBarDescriptor bar2, PciBarDescriptor bar3, PciBarDescriptor bar4, PciBarDescriptor bar5)
    {
        Bar0 = bar0;
        Bar1 = bar1;
        Bar2 = bar2;
        Bar3 = bar3;
        Bar4 = bar4;
        Bar5 = bar5;
    }

    public PciBarDescriptor Bar0 { get; }
    public PciBarDescriptor Bar1 { get; }
    public PciBarDescriptor Bar2 { get; }
    public PciBarDescriptor Bar3 { get; }
    public PciBarDescriptor Bar4 { get; }
    public PciBarDescriptor Bar5 { get; }

    public bool TryGet(byte index, out PciBarDescriptor descriptor)
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

    public bool TryCopyTo(Span<PciBarDescriptor> destination)
    {
        if (destination.Length < PciBarDecoder.BarCount)
        {
            return false;
        }

        destination[0] = Bar0;
        destination[1] = Bar1;
        destination[2] = Bar2;
        destination[3] = Bar3;
        destination[4] = Bar4;
        destination[5] = Bar5;
        return true;
    }
}
