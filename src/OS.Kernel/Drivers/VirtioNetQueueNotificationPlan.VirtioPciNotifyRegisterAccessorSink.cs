namespace OS.Drivers;
public sealed class VirtioPciNotifyRegisterAccessorSink : IVirtioPciNotifyWriteSink
{
    private readonly IVirtioPciRegisterAccessor? _accessor;
    public const bool RequiresInjectedAccessor = true;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsLiveMmioImplementation = false;
    public const bool MapsBars = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool NotifiesLiveDevice = false;
    public VirtioPciNotifyRegisterAccessorSink(IVirtioPciRegisterAccessor? accessor)
    {
        _accessor = accessor;
    }

    public bool HasAccessor => _accessor is not null;

    public VirtioPciNotifyWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
    {
        if (_accessor is null)
        {
            return VirtioPciNotifyWriteStatus.NotReady;
        }

        return Map(_accessor.Write16(aperture, relativeOffset, value));
    }

    private static VirtioPciNotifyWriteStatus Map(VirtioPciRegisterWriteStatus status)
    {
        return status == VirtioPciRegisterWriteStatus.Success ? VirtioPciNotifyWriteStatus.Success : status == VirtioPciRegisterWriteStatus.RangeOutsideAperture ? VirtioPciNotifyWriteStatus.RangeOutsideAperture : status == VirtioPciRegisterWriteStatus.NotReady ? VirtioPciNotifyWriteStatus.NotReady : VirtioPciNotifyWriteStatus.WriteFailed;
    }
}
