namespace OS.Drivers;

public readonly struct PciBusScanRange
{
    public PciBusScanRange(byte startBus, byte endBus, byte startDevice = 0, byte endDevice = PciEcamConfigurationSpace.MaximumDevice, byte maximumFunction = PciEcamConfigurationSpace.MaximumFunction)
    {
        StartBus = startBus;
        EndBus = endBus;
        StartDevice = startDevice;
        EndDevice = endDevice;
        MaximumFunction = maximumFunction;
    }

    public byte StartBus
    {
        get;
    }
    public byte EndBus
    {
        get;
    }
    public byte StartDevice
    {
        get;
    }
    public byte EndDevice
    {
        get;
    }
    public byte MaximumFunction
    {
        get;
    }
}
