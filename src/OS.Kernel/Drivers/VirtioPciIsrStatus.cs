namespace OS.Drivers;

public enum VirtioPciIsrReadPlanStatus
{
    Success = 0,
    InvalidApertureCapability = 1,
    ApertureTooSmall = 2
}

public readonly struct VirtioPciIsrStatus
{
    public VirtioPciIsrStatus(byte rawStatus)
    {
        RawStatus = rawStatus;
    }

    public byte RawStatus { get; }

    public bool HasQueueInterrupt => (RawStatus & VirtioPciIsrStatusParser.QueueInterruptMask) != 0;

    public bool HasDeviceConfigurationChange =>
        (RawStatus & VirtioPciIsrStatusParser.DeviceConfigurationChangeMask) != 0;

    public byte ReservedBits => (byte)(RawStatus & VirtioPciIsrStatusParser.ReservedBitsMask);
}

public readonly struct VirtioPciIsrReadPlan
{
    internal VirtioPciIsrReadPlan(VirtioPciBarAperture aperture)
    {
        Aperture = aperture;
    }

    public VirtioPciBarAperture Aperture { get; }

    public uint RelativeOffset => VirtioPciIsrStatusParser.ReadOffset;

    public byte WidthBytes => VirtioPciIsrStatusParser.ReadWidthBytes;

    public bool ReadClearsStatus => VirtioPciIsrStatusParser.IntxReadClearsStatus;
}

public readonly struct VirtioPciIsrReadPlanResult
{
    private VirtioPciIsrReadPlanResult(
        VirtioPciIsrReadPlanStatus status,
        VirtioPciBarAperture aperture,
        VirtioPciIsrReadPlan plan,
        bool isInitialized)
    {
        Status = status;
        Aperture = aperture;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciIsrReadPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioPciBarAperture Aperture { get; }

    public VirtioPciIsrReadPlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciIsrReadPlanStatus.Success;

    public static VirtioPciIsrReadPlanResult Success(VirtioPciIsrReadPlan plan)
    {
        return new VirtioPciIsrReadPlanResult(
            VirtioPciIsrReadPlanStatus.Success,
            plan.Aperture,
            plan,
            isInitialized: true);
    }

    public static VirtioPciIsrReadPlanResult Fail(
        VirtioPciIsrReadPlanStatus status,
        VirtioPciBarAperture aperture)
    {
        return new VirtioPciIsrReadPlanResult(status, aperture, default, isInitialized: true);
    }
}

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
            return VirtioPciIsrReadPlanResult.Fail(
                VirtioPciIsrReadPlanStatus.InvalidApertureCapability,
                isrAperture);
        }

        if (isrAperture.Length < ReadWidthBytes)
        {
            return VirtioPciIsrReadPlanResult.Fail(
                VirtioPciIsrReadPlanStatus.ApertureTooSmall,
                isrAperture);
        }

        return VirtioPciIsrReadPlanResult.Success(new VirtioPciIsrReadPlan(isrAperture));
    }
}
