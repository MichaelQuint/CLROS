using OS.Std.Text;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotClassConstructorRunnerPreflightFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAotClassConstructorRunnerPreflightFormatResult(NativeAotClassConstructorRunnerPreflightFormatStatus status, NativeAotClassConstructorRunnerPreflightStatus preflightStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        PreflightStatus = preflightStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAotClassConstructorRunnerPreflightFormatStatus Status
    {
        get;
    }
    public NativeAotClassConstructorRunnerPreflightStatus PreflightStatus
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
    public int AvailableBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorRunnerPreflightFormatStatus.Success && RequiredBytes > 0 && (BytesWritten == 0 || (BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes));

    public static NativeAotClassConstructorRunnerPreflightFormatResult Fail(NativeAotClassConstructorRunnerPreflightFormatStatus status, NativeAotClassConstructorRunnerPreflightStatus preflightStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAotClassConstructorRunnerPreflightFormatResult(status, preflightStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
