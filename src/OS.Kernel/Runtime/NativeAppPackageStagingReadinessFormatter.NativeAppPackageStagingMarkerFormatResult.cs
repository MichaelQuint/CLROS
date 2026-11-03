using OS.Manifest;
using OS.Std.Text;
using OS.Storage;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppPackageStagingMarkerFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus status, int bytesWritten, int requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public NativeAppPackageStagingMarkerFormatStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAppPackageStagingMarkerFormatStatus.Success && RequiredBytes != 0 && BytesWritten == RequiredBytes;
}
