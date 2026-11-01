namespace OS.Drivers;
[Flags]
public enum VirtioDeviceStatus : byte
{
    None = 0,
    Acknowledge = 1,
    Driver = 2,
    DriverOk = 4,
    FeaturesOk = 8,
    Failed = 128
}
