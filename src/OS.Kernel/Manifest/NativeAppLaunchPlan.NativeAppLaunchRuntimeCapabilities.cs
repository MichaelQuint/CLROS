using OS.Boot;

namespace OS.Manifest;
[Flags]
public enum NativeAppLaunchRuntimeCapabilities
{
    None = 0,
    PackageStorage = 1,
    Network = 2
}
