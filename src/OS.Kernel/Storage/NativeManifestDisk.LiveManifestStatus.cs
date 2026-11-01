using System;
using OS.Std.Text;

namespace OS.Storage;

public enum LiveManifestStatus : byte
{
    Uninitialized = 0, Success, InvalidLength, InvalidCharacter, MalformedLine,
    UnsupportedField, DuplicateField, MissingField, InvalidValue, InvalidPath
}
