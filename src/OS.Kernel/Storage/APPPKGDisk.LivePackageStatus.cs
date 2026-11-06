using System;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Text;

namespace OS.Storage;

public enum LivePackageStatus : byte
{
    Uninitialized = 0, Success, InvalidLength, InvalidHeader, UnsupportedVersion,
    InvalidTable, InvalidEntry, InvalidPath, DuplicatePath, Overlap, MissingFile, ChecksumMismatch
}
