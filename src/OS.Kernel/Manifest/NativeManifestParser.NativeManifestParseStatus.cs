using OS.Std;
using OS.Std.Text;

namespace OS.Manifest;
public enum NativeManifestParseStatus
{
    Success = 0,
    EmptyInput,
    InputTooLarge,
    NonAsciiOrControlCharacter,
    MalformedLine,
    UnknownKey,
    DuplicateKey,
    MissingFormat,
    UnsupportedFormat,
    MissingVersion,
    UnsupportedVersion,
    MissingAppName,
    AppNameTooLong,
    MissingAppPath,
    AppPathTooLong,
    MissingArgs,
    ArgsTooLong,
    MissingAppKind,
    UnsupportedAppKind,
    MissingRuntimeKind,
    UnsupportedRuntimeKind,
    MissingNetworkMode,
    InvalidNetworkMode,
    MissingLogMode,
    InvalidLogMode,
    EnvironmentKeyTooLong,
    EnvironmentValueTooLong,
    InvalidEnvironmentKey,
    TooManyEnvironmentFields,
    DuplicateEnvironmentKey,
    InvalidListenEndpoint,
    ListenRequiresNetwork
}
