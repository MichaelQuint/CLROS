namespace OS.Drivers;

public interface IVirtioPciRegisterAccessor
{
    VirtioPciRegisterRead16Result Read16(VirtioPciBarAperture aperture, uint relativeOffset);
    VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value);
    VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value);
}
