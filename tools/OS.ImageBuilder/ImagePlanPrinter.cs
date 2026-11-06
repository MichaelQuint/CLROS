using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal static partial class ImagePlanPrinter
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public static void Print(RawImageLayout layout, TextWriter writer, string workloadProfile)
    {
        writer.Write("IMAGE_PLAN ");
        writer.WriteLine(JsonSerializer.Serialize(new PlanSummary("dry-run", "raw", workloadProfile, layout.OutputPath, layout.ImageSizeMiB, layout.ImageBytes, RawEspImageBuilder.SectorSize, layout.TotalSectors, writesSuppressed: true), SerializerOptions));
        foreach (PlanEntry entry in CreateEntries(layout))
        {
            writer.Write("IMAGE_PLAN_ENTRY ");
            writer.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
        }
    }

    private static IEnumerable<PlanEntry> CreateEntries(RawImageLayout layout)
    {
        yield return PlanEntry.Planned(1, "protective-mbr", "Protective MBR", 0, 1);
        yield return PlanEntry.Planned(2, "primary-gpt-header", "Primary GPT header", 1, 1);
        yield return PlanEntry.Planned(3, "primary-gpt-entries", "Primary GPT entries", layout.PrimaryGptEntriesLba, 32);
        yield return PlanEntry.Planned(4, "efi-system-partition", "EFI System Partition", layout.PartitionStartLba, layout.PartitionSectorCount);
        yield return PlanEntry.Planned(5, "fat32-reserved", "FAT32 reserved sectors", layout.PartitionStartLba, RawEspImageBuilder.ReservedSectors);
        yield return PlanEntry.Planned(6, "fat32-fat-primary", "FAT32 primary FAT", layout.FirstFatLba, layout.FatSectors);
        yield return PlanEntry.Planned(7, "fat32-fat-backup", "FAT32 backup FAT", layout.SecondFatLba, layout.FatSectors);
        yield return PlanEntry.Planned(8, "fat32-root-directory", "FAT32 root directory", layout.RootDirectoryLba, RawEspImageBuilder.SectorsPerCluster);
        yield return PlanEntry.Planned(9, "fat32-efi-directory", "FAT32 EFI directory", layout.EfiDirectoryLba, RawEspImageBuilder.SectorsPerCluster, guestPath: @"\EFI");
        yield return PlanEntry.Planned(10, "fat32-boot-directory", "FAT32 BOOT directory", layout.BootDirectoryLba, RawEspImageBuilder.SectorsPerCluster, guestPath: @"\EFI\BOOT");
        yield return PlanEntry.Planned(11, "efi-boot-file", "UEFI removable-media boot file", layout.BootFileLba, layout.FileClusterCount, bytes: layout.EfiSizeBytes, guestPath: @"\EFI\BOOT\BOOTX64.EFI", sourcePath: layout.EfiPath);
        int order = 12;
        if (layout.HasOsDirectory)
        {
            yield return PlanEntry.Planned(order++, "fat32-os-directory", "FAT32 OS directory", layout.OsDirectoryLba, RawEspImageBuilder.SectorsPerCluster, guestPath: @"\OS");
        }

        if (layout.HasManifest)
        {
            yield return PlanEntry.Planned(order++, "manifest-file", "CloudReady manifest", layout.ManifestFileLba, layout.ManifestFileClusterCount, bytes: layout.ManifestSizeBytes, guestPath: @"\OS\MANIFEST.JSON", sourcePath: layout.ManifestPath);
        }
        else
        {
            yield return PlanEntry.Placeholder(order++, "manifest-placeholder", "CloudReady manifest placeholder", guestPath: @"\OS\MANIFEST.JSON", reason: "manifest_package_placement_pending");
        }

        if (layout.HasPackage)
        {
            yield return PlanEntry.Planned(order++, "app-package-file", "CloudReady app package", layout.PackageFileLba, layout.PackageFileClusterCount, bytes: layout.PackageSizeBytes, guestPath: @"\OS\APP.PKG", sourcePath: layout.PackagePath);
        }
        else
        {
            yield return PlanEntry.Placeholder(order++, "app-package-placeholder", "CloudReady app package placeholder", guestPath: @"\OS\APP.PKG", reason: "app_package_placement_pending");
        }

        yield return PlanEntry.Planned(order++, "backup-gpt-entries", "Backup GPT entries", layout.BackupGptEntriesLba, 32);
        yield return PlanEntry.Planned(order, "backup-gpt-header", "Backup GPT header", layout.BackupGptHeaderLba, 1);
    }
}
