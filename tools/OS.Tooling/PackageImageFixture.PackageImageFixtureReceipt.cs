using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;

public sealed record PackageImageFixtureReceipt(string Variant, string OriginalSha256, string FinalSha256, string EfiSha256, ulong PartitionFirstLba, ulong PartitionLastLba, uint RootCluster, uint RootExtension, uint OsDirectoryCluster, uint ManifestCluster, uint PackageCluster, uint ManifestBytes, uint PackageBytes, uint PackageStoredBytes, uint ManifestCrc32, uint PackageCrc32, uint PayloadCrc32);
