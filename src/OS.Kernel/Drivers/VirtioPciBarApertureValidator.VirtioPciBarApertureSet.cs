namespace OS.Drivers;
public readonly struct VirtioPciBarApertureSet
{
    internal VirtioPciBarApertureSet(VirtioPciBarAperture commonConfiguration, VirtioPciBarAperture notifyConfiguration, VirtioPciBarAperture isrStatus, VirtioPciBarAperture deviceConfiguration)
    {
        CommonConfiguration = commonConfiguration;
        NotifyConfiguration = notifyConfiguration;
        IsrStatus = isrStatus;
        DeviceConfiguration = deviceConfiguration;
    }

    public VirtioPciBarAperture CommonConfiguration { get; }
    public VirtioPciBarAperture NotifyConfiguration { get; }
    public VirtioPciBarAperture IsrStatus { get; }
    public VirtioPciBarAperture DeviceConfiguration { get; }
}
