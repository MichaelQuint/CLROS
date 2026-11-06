namespace OS.Drivers;

public readonly struct VirtioPciIsrReadPlan
{
    internal VirtioPciIsrReadPlan(VirtioPciBarAperture aperture)
    {
        Aperture = aperture;
    }

    public VirtioPciBarAperture Aperture
    {
        get;
    }
    public uint RelativeOffset => VirtioPciIsrStatusParser.ReadOffset;
    public byte WidthBytes => VirtioPciIsrStatusParser.ReadWidthBytes;
    public bool ReadClearsStatus => VirtioPciIsrStatusParser.IntxReadClearsStatus;
}
