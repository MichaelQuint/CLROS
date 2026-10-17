using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;

public sealed record LinuxElfImageReceipt(string Variant, string OriginalSha256, string FinalSha256,
    string EfiSha256, string PackageSha256, int ChangedOffset, int ChangedBytes);

/// <summary>Controlled faults in a freshly generated bounded FAT32 image.
/// Admit complete original EFI/package bytes and both contiguous FAT copies
/// before changing only the payload's last byte or its directory size field.</summary>
public static class LinuxElfImageFixture
{
    /// <summary>Byte offsets of the exact boot EFI, package and package directory
    /// entry in an inspected image. Borrowed bytes are never executed or retained;
    /// these locations grant no native provenance or guest authority.</summary>
    public readonly record struct Locations(int EfiStart,int PackageStart,int PackageEntry);

    /// <summary>Inspect a borrowed 64-MiB raw image against complete expected EFI
    /// and validated Linux package bytes. Requires the bounded FAT32 directory
    /// route, sizes and both contiguous FAT copies; mismatch throws ToolFailure.
    /// This neither mutates inputs nor qualifies boot, provenance or execution.</summary>
    public static Locations Inspect(byte[] image,byte[] efi,byte[] package)
    {
        if(image.Length!=64*1024*1024)throw new ToolFailure("m16_002_fixture_image_size");
        if(efi.Length is <=0 or >16*1024*1024)throw new ToolFailure("m16_002_fixture_input_size");
        if(!LinuxElfPackage.Validate(package).Succeeded)throw new ToolFailure("m16_002_fixture_package");
        return Locate(image,efi,package);
    }

    public static LinuxElfImageReceipt Apply(string imagePath, string efiPath, string packagePath, string variant)
    {
        if (variant is not ("DigestFault" or "Oversized")) throw new ToolFailure("m16_002_image_variant");
        byte[] efi = Read(efiPath, 16 * 1024 * 1024), package = Read(packagePath, (int)LinuxElfPackage.MaximumBytes);
        var view = LinuxElfPackage.Validate(package);
        if (!view.Succeeded) throw new ToolFailure("m16_002_fixture_package");
        using var file = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (file.Length != 64 * 1024 * 1024) throw new ToolFailure("m16_002_fixture_image_size");
        byte[] image = new byte[(int)file.Length]; file.ReadExactly(image);
        var locations=Locate(image,efi,package);
        string before = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        int change = variant == "DigestFault" ? locations.PackageStart + (int)view.PayloadOffset + (int)view.PayloadBytes - 1 : locations.PackageEntry + 28;
        byte[] original = image.AsSpan(change, variant == "DigestFault" ? 1 : 4).ToArray();
        if (variant == "DigestFault") image[change] ^= 1;
        else BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(change, 4), LinuxElfPackage.MaximumBytes + 1);
        int changed = 0; for (int i = 0; i < original.Length; i++) if (original[i] != image[change + i]) changed++;
        if(changed==0)throw new ToolFailure("m16_002_fixture_geometry");
        file.Position = 0; file.Write(image); file.Flush(true); file.Position = 0;
        byte[] readback = new byte[image.Length]; file.ReadExactly(readback);
        if(!readback.AsSpan().SequenceEqual(image))throw new ToolFailure("m16_002_fixture_geometry");
        return new(variant, before, Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(efi)).ToLowerInvariant(), Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(), change, changed);
    }

    private static Locations Locate(byte[] image,byte[] efi,byte[] package)
    {
        const int partition = 2048 * 512;
        int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(at, 2));
        uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at, 4));
        void Need(bool okay) { if (!okay) throw new ToolFailure("m16_002_fixture_geometry"); }
        int spc = image[partition + 13], reserved = U16(partition + 14);
        uint fatSectors = U32(partition + 36), root = U32(partition + 44);
        Need(U16(partition + 11) == 512 && U16(partition + 510) == 0xaa55 && spc > 0 && spc <= 128 && (spc & (spc - 1)) == 0 &&
            reserved > 0 && image[partition + 16] == 2 && fatSectors > 0 && fatSectors < 8192 && root >= 2);
        int clusterBytes = spc * 512, fat = partition + reserved * 512;
        long dataLong = (long)fat + fatSectors * 1024;
        Need(dataLong < image.Length); int data = (int)dataLong;
        int Cluster(uint cluster)
        {
            long at = dataLong + ((long)cluster - 2) * clusterBytes;
            Need(cluster >= 2 && at >= data && at <= image.Length - clusterBytes); return (int)at;
        }
        int Entry(uint cluster, string name, bool directory)
        {
            int found = -1, start = Cluster(cluster);
            for (int at = start; at < start + clusterBytes; at += 32)
            {
                if (image[at] == 0) break;
                bool same = true; for (int i = 0; i < 11; i++) if (image[at + i] != name[i]) same = false;
                if (!same) continue;
                Need(found == -1 && ((image[at + 11] & 0x10) != 0) == directory); found = at;
            }
            Need(found != -1); return found;
        }
        uint First(int entry) => (uint)(U16(entry + 26) | (U16(entry + 20) << 16));
        int FileStart(int entry, byte[] expected)
        {
            Need(U32(entry + 28) == expected.Length);
            uint first = First(entry), count = ((uint)expected.Length + (uint)clusterBytes - 1) / (uint)clusterBytes;
            for (uint i = 0; i < count; i++)
            {
                uint cluster = first + i; Need(cluster < fatSectors * 128);
                int at = fat + (int)cluster * 4;
                uint next = i + 1 == count ? 0x0fffffffu : cluster + 1;
                Need(U32(at) == next && U32(at + (int)fatSectors * 512) == next);
            }
            int start = Cluster(first); Need(expected.Length <= image.Length - start);
            Need(image.AsSpan(start, expected.Length).SequenceEqual(expected)); return start;
        }
        uint boot = First(Entry(First(Entry(root, "EFI        ", true)), "BOOT       ", true));
        int efiStart = FileStart(Entry(boot, "BOOTX64 EFI", false), efi);
        uint os = First(Entry(root, "OS         ", true)); int packageEntry = Entry(os, "APP     PKG", false);
        int packageStart = FileStart(packageEntry, package);
        return new(efiStart,packageStart,packageEntry);
    }
    private static byte[] Read(string path, int maximum)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length <= 0 || file.Length > maximum) throw new ToolFailure("m16_002_fixture_input_size");
        byte[] bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new ToolFailure("m16_002_fixture_input_changed"); return bytes;
    }
}
