namespace OS.Drivers;
public enum VirtioPciCapabilityType : byte
{
    CommonConfiguration = 1,
    NotifyConfiguration = 2,
    IsrStatus = 3,
    DeviceConfiguration = 4,
    PciConfiguration = 5
}
