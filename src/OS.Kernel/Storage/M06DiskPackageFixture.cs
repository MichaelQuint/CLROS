namespace OS.Storage;

/// <summary>Reviewed finite smoke fixture metadata, never device authority.
/// CRC32 detects accidental corruption and is not authentication.</summary>
public static class M06DiskPackageFixture
{
    public const uint ManifestBytes = 104, PackageBytes = 132, PayloadBytes = 36;
    public const uint PackageCrc32 = 0x8680772e, PayloadCrc32 = 0x8ad44af4;
    public const uint ManifestCrc32 = 0x67a2f958, AppPathOffset = 60, AppPathLength = 13;
    public const uint EntryOffset = 32;
}
