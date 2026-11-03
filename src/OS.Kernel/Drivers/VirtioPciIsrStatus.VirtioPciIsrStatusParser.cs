namespace OS.Drivers;

public static class VirtioPciIsrStatusParser
{
    public const uint ReadOffset = 0;
    public const byte ReadWidthBytes = 1;
    public const byte QueueInterruptMask = 0x01;
    public const byte DeviceConfigurationChangeMask = 0x02;
    public const byte ReservedBitsMask = 0xFC;
    public const bool IntxReadClearsStatus = true;
    public const bool ConfiguresMsix = false;
    public const bool ConfiguresApicOrIoApic = false;
    public const bool DrainsQueues = false;
    public const bool DispatchesPackets = false;
    public static VirtioPciIsrStatus Parse(byte rawStatus)
    {
        return new VirtioPciIsrStatus(rawStatus);
    }

    public static VirtioPciIsrReadPlanResult CreateReadPlan(VirtioPciBarAperture isrAperture)
    {
        if (isrAperture.CapabilityType != VirtioPciCapabilityType.IsrStatus)
        {
            return VirtioPciIsrReadPlanResult.Fail(VirtioPciIsrReadPlanStatus.InvalidApertureCapability, isrAperture);
        }

        if (isrAperture.Length < ReadWidthBytes)
        {
            return VirtioPciIsrReadPlanResult.Fail(VirtioPciIsrReadPlanStatus.ApertureTooSmall, isrAperture);
        }

        return VirtioPciIsrReadPlanResult.Success(new VirtioPciIsrReadPlan(isrAperture));
    }
}
