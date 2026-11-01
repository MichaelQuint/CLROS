namespace OS.Kernel.Runtime;
[Flags]
public enum NativeAotCompletionGate : uint
{
    None = 0,
    CleanBuild = 1u << 0,
    Boot = 1u << 1,
    FaultDiagnostics = 1u << 2,
    StdRuntime = 1u << 3,
    UefiEncapsulation = 1u << 4,
    HostedDotNetKestrel = 1u << 5,
    ExtendedHostedTier = 1u << 6
}
