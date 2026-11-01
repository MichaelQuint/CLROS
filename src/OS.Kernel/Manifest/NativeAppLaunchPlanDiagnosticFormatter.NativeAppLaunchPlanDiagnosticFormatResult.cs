using OS.Boot;
using OS.Std.Text;

namespace OS.Manifest;
public readonly struct NativeAppLaunchPlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAppLaunchPlanDiagnosticFormatResult(NativeAppLaunchPlanDiagnosticFormatStatus status, NativeAppLaunchStatus launchStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        LaunchStatus = launchStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAppLaunchPlanDiagnosticFormatStatus Status { get; }
    public NativeAppLaunchStatus LaunchStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAppLaunchPlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static NativeAppLaunchPlanDiagnosticFormatResult Fail(NativeAppLaunchPlanDiagnosticFormatStatus status, NativeAppLaunchStatus launchStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAppLaunchPlanDiagnosticFormatResult(status, launchStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
