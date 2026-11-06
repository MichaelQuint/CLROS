namespace OS.Boot;
/// <summary>
/// Holds one untrusted transcript of an attempted loaded-image protocol
/// acquisition. It is a compact CLROS observation, not the 96-byte x64
/// EFI_LOADED_IMAGE_PROTOCOL and must never be cast over firmware memory.
/// </summary>

public readonly struct EfiLoadedImageProtocolObservation
{
    private readonly ulong _firmwareStatus;
    private readonly ulong _imageHandleFromUefi;
    private readonly ulong _protocolAddressFromUefi;
    private readonly ulong _imageBaseFromUefi;
    private readonly ulong _imageSizeFromUefi;
    private EfiLoadedImageProtocolObservation(ulong firmwareStatus, ulong imageHandleFromUefi, ulong protocolAddressFromUefi, ulong imageBaseFromUefi, ulong imageSizeFromUefi)
    {
        _firmwareStatus = firmwareStatus;
        _imageHandleFromUefi = imageHandleFromUefi;
        _protocolAddressFromUefi = protocolAddressFromUefi;
        _imageBaseFromUefi = imageBaseFromUefi;
        _imageSizeFromUefi = imageSizeFromUefi;
    }

    internal ulong FirmwareStatus => _firmwareStatus;
    internal ulong ImageHandleFromUefi => _imageHandleFromUefi;
    internal ulong ProtocolAddressFromUefi => _protocolAddressFromUefi;
    internal ulong ImageBaseFromUefi => _imageBaseFromUefi;
    internal ulong ImageSizeFromUefi => _imageSizeFromUefi;

    public static EfiLoadedImageProtocolObservation CaptureFromUefiBoundary(ulong firmwareStatus, ulong imageHandle, ulong protocolAddress, ulong imageBase, ulong imageSize)
    {
        return new EfiLoadedImageProtocolObservation(firmwareStatus, imageHandle, protocolAddress, imageBase, imageSize);
    }

    public bool Equals(EfiLoadedImageProtocolObservation other)
    {
        return _firmwareStatus == other._firmwareStatus && _imageHandleFromUefi == other._imageHandleFromUefi && _protocolAddressFromUefi == other._protocolAddressFromUefi && _imageBaseFromUefi == other._imageBaseFromUefi && _imageSizeFromUefi == other._imageSizeFromUefi;
    }
}
