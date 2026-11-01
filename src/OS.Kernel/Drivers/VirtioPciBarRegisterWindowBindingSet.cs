using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowBindingSet
{
    internal VirtioPciBarRegisterWindowBindingSet(VirtioPciBarRegisterWindowBinding commonConfiguration, VirtioPciBarRegisterWindowBinding notifyConfiguration, VirtioPciBarRegisterWindowBinding isrStatus, VirtioPciBarRegisterWindowBinding deviceConfiguration)
    {
        CommonConfiguration = commonConfiguration;
        NotifyConfiguration = notifyConfiguration;
        IsrStatus = isrStatus;
        DeviceConfiguration = deviceConfiguration;
    }

    public VirtioPciBarRegisterWindowBinding CommonConfiguration { get; }
    public VirtioPciBarRegisterWindowBinding NotifyConfiguration { get; }
    public VirtioPciBarRegisterWindowBinding IsrStatus { get; }
    public VirtioPciBarRegisterWindowBinding DeviceConfiguration { get; }
}
