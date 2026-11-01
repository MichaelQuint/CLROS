namespace OS.Drivers;
public interface IVirtioPciNotifyWriteSink
{
    VirtioPciNotifyWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value);
}
