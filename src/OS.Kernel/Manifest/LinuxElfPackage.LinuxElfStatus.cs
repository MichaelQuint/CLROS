using OS.Std.Hash;

namespace OS.Manifest;

public enum LinuxElfStatus : byte
{
    Uninitialized,
    Success,
    InvalidLength,
    InvalidHeader,
    UnsupportedProfile,
    InvalidCharacter,
    MalformedLine,
    UnknownField,
    DuplicateField,
    MissingField,
    InvalidValue,
    InvalidPath,
    InvalidEndpoint,
    InvalidEnvironment,
    DigestMismatch,
    UnsupportedElf,
    InvalidDestination
}
