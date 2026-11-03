namespace OS.Tooling;

/// <summary>Host capture ceilings selected explicitly by the original full disk
/// owner. Limits are independent of observed lengths or image asset counts.
/// Selecting them supplies no source/native/process/browser authority.</summary>
public static class DiskStaticCapturePolicy
{
    public const int SerialBytes = OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumSerialBytes;
    public const int SummaryBytes = 256 * 1024 * 1024;
    public const int PacketBytes = 64 * 1024 * 1024;
    public const int StreamBytes = 24 * 1024 * 1024;
    // Combined host stdout/stderr collection; guest byte/write quotas are separate.
    public const int CombinedOutputBytes = 128 * 1024;
    public const int ProtocolRows = 1_000_000;
    public const int HttpReadinessTimeoutSeconds = 240;
    public const int HttpMatrixTimeoutSeconds = 1700;
    public const int DeviceRows = (int)OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumDeviceRecords;
    // The human accepts slow but correct QEMU startup. Keep the HTTP matrix
    // deadline separate from the browser and leave time for natural shutdown.
    public const int BrowserTimeoutSeconds = 3600;
    public const int DefaultTimeoutSeconds = 7200;
    public const int MinimumTimeoutSeconds = 6600;
    public const int MaximumTimeoutSeconds = 7200;
}
