namespace OS.Kernel.Diagnostics;
/// <summary>Scalar intent only. Neither ranges nor prior observations grant readable/executable authority.</summary>

public readonly struct UnexpectedVectorPanicRequest(ulong codeAddress, ulong dataAddress, ulong imageAddress, ulong imageBytes, ulong callbackAddress, ulong firmwareCs, ulong firmwareCr3, bool optInRequested)
{
    public ulong CodeAddress { get; } = codeAddress;
    public ulong DataAddress { get; } = dataAddress;
    public ulong ImageAddress { get; } = imageAddress;
    public ulong ImageBytes { get; } = imageBytes;
    public ulong CallbackAddress { get; } = callbackAddress;
    public ulong FirmwareCs { get; } = firmwareCs;
    public ulong FirmwareCr3 { get; } = firmwareCr3;
    public bool OptInRequested { get; } = optInRequested;
}
