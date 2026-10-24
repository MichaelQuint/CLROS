namespace OS.Tooling;

/// <summary>Host capture ceilings selected explicitly by the original full disk
/// owner. Limits are independent of observed lengths or image asset counts.
/// Selecting them supplies no source/native/process/browser authority.</summary>
public static class DiskStaticCapturePolicy
{
    public const int SerialBytes = 128 * 1024 * 1024;
    public const int SummaryBytes = 256 * 1024 * 1024;
    public const int PacketBytes = 64 * 1024 * 1024;
    public const int StreamBytes = 24 * 1024 * 1024;
    public const int ProtocolRows = 1_000_000;
    public const int DeviceRows = (int)OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumDeviceRecords;
    public const int DefaultTimeoutSeconds = 3000;
    public const int MinimumTimeoutSeconds = 2200;
    public const int MaximumTimeoutSeconds = 3600;
}
