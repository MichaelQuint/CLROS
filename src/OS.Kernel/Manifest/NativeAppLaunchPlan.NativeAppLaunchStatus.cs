using OS.Boot;

namespace OS.Manifest;
public enum NativeAppLaunchStatus
{
    Success = 0,
    UnsupportedManifestVersion,
    MissingAppName,
    MissingAppPath,
    MissingArguments,
    InvalidAppNameField,
    InvalidAppPathField,
    InvalidArgumentsField,
    AppPathNotPackageRelative,
    ArgumentsTooLong,
    UnsupportedAppKind,
    UnsupportedRuntimeKind,
    UnsupportedNetworkMode,
    UnsupportedLogMode,
    InvalidListenEndpoint,
    ListenRequiresNetwork,
    MissingBootCapabilities,
    UnknownBootCapabilities,
    MissingRuntimeCapabilities
}
