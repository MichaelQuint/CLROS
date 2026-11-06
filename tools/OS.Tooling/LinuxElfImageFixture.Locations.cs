using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;

public static partial class LinuxElfImageFixture
{
    /// <summary>Byte offsets of the exact boot EFI, package and package directory
    /// entry in an inspected image. Borrowed bytes are never executed or retained;
    /// these locations grant no native provenance or guest authority.</summary>

    public readonly record struct Locations(int EfiStart, int PackageStart, int PackageEntry);
}
