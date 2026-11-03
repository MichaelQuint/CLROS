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

internal static partial class RawEspImageBuilder
{
    public const int SectorSize = 512;
    public const int SectorsPerCluster = 1;
    public const int ReservedSectors = 32;
    public const int FatCount = 2;
    private const int GptEntryCount = 128;
    private const int GptEntrySize = 128;
    public const long PartitionStartLba = 2048;
    public const int RootCluster = 2;
    public const int EfiDirectoryCluster = 3;
    public const int BootDirectoryCluster = 4;
    public const int BootFileFirstCluster = 5;
    private const string DefaultManifestText = "format=os-native-manifest\n" + "version=0\n" + "app.name=os.default\n" + "app.path=apps/hello.bin\n" + "args=\n" + "network=none\n" + "log=serial\n";
    private const string ManifestLongFileName = "MANIFEST.JSON";
    private const string ManifestShortName = "MANIFE~1";
    private const string ManifestShortExtension = "JSO";
    private const string PackageShortName = "APP";
    private const string PackageShortExtension = "PKG";
    private static readonly Guid EfiSystemPartitionType = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid DiskGuid = new Guid("3e6023df-5f57-49db-b556-1bffec7b5f5f");
    private static readonly Guid PartitionGuid = new Guid("9b25aa95-bb50-4d2f-9c8f-7cf06e2e4c1e");
    public static void Build(ImageBuilderOptions options)
    {
        RawImageLayout layout = Plan(options);
        byte[] efiBytes = File.ReadAllBytes(options.EfiPath);
        byte[] manifestBytes = layout.ManifestPath is null ? DefaultManifestBytes() : File.ReadAllBytes(layout.ManifestPath);
        byte[]? packageBytes = LoadPackageBytes(options);
        FatLayout fat = FatLayout.Create(layout.PartitionSectorCount, efiBytes.Length, manifestBytes.Length, packageBytes?.Length ?? 0);
        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using FileStream image = new FileStream(options.OutputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        image.SetLength(layout.ImageBytes);
        WriteProtectiveMbr(image, layout.TotalSectors);
        WriteGpt(image, layout.TotalSectors, PartitionStartLba, layout.PartitionLastLba);
        WriteFat32Esp(image, PartitionStartLba, layout.PartitionSectorCount, fat, efiBytes, manifestBytes, packageBytes);
    }

    public static RawImageLayout Plan(ImageBuilderOptions options)
    {
        FileInfo efiFile = ValidateEfiPath(options.EfiPath);
        FileInfo? manifestFile = ValidateManifestPath(options.ManifestPath);
        RawPackagePlan packagePlan = PlanPackageInput(options);
        ValidateOutputPath(options.OutputPath);
        if (efiFile.Length == 0)
        {
            throw new ImageBuilderException(4, "efi_empty", "EFI artifact is empty: " + options.EfiPath);
        }

        if (efiFile.Length > int.MaxValue)
        {
            throw new ImageBuilderException(6, "efi_too_large", "EFI artifact is too large for the current FAT32 ESP writer.");
        }

        long imageBytes = options.ImageSizeMiB * 1024L * 1024L;
        long totalSectors = imageBytes / SectorSize;
        long lastUsableLba = totalSectors - 34;
        if (PartitionStartLba > lastUsableLba)
        {
            throw new ImageBuilderException(5, "image_too_small", "Image is too small for the default GPT/ESP layout.");
        }

        long partitionLastLba = lastUsableLba;
        long partitionSectorCount64 = partitionLastLba - PartitionStartLba + 1;
        if (partitionSectorCount64 > uint.MaxValue)
        {
            throw new ImageBuilderException(5, "image_too_large", "Image is too large for the current FAT32 ESP layout.");
        }

        uint partitionSectorCount = (uint)partitionSectorCount64;
        FatLayout fat = FatLayout.Create(partitionSectorCount, checked((int)efiFile.Length), manifestFile is null ? DefaultManifestBytes().Length : checked((int)manifestFile.Length), checked((int)packagePlan.SizeBytes));
        return new RawImageLayout(options.EfiPath, options.OutputPath, manifestFile is null ? null : options.ManifestPath, packagePlan.SourcePath, efiFile.Length, manifestFile?.Length ?? DefaultManifestBytes().Length, packagePlan.SizeBytes, options.ImageSizeMiB, imageBytes, totalSectors, PartitionStartLba, partitionLastLba, partitionSectorCount, fat.FatSectors, fat.ClusterCount, fat.FileClusterCount, fat.ManifestFileClusterCount, fat.PackageFileClusterCount, fat.OsDirectoryCluster, fat.ManifestFileFirstCluster, fat.PackageFileFirstCluster);
    }

    private static RawPackagePlan PlanPackageInput(ImageBuilderOptions options)
    {
        if (options.KestrelPackageRootPath is not null)
        {
            byte[] packageBytes = BuildKestrelPackageBytes(options);
            return new RawPackagePlan(options.KestrelPackageRootPath, packageBytes.Length);
        }

        FileInfo? packageFile = ValidatePackagePath(options.PackagePath);
        return packageFile is null ? RawPackagePlan.None : new RawPackagePlan(options.PackagePath, packageFile.Length);
    }

    private static byte[]? LoadPackageBytes(ImageBuilderOptions options)
    {
        if (options.KestrelPackageRootPath is not null)
        {
            return BuildKestrelPackageBytes(options);
        }

        return options.PackagePath is null ? null : File.ReadAllBytes(options.PackagePath);
    }

    private static byte[] BuildKestrelPackageBytes(ImageBuilderOptions options)
    {
        if (options.ManifestPath is null)
        {
            throw new ImageBuilderException(30, "kestrel_package_missing_manifest", "--kestrel-package requires --manifest.");
        }

        string packageRootPath = options.KestrelPackageRootPath ?? throw new ImageBuilderException(30, "kestrel_package_invalid_root", "--kestrel-package root is required.");
        ValidationResult validation = KestrelSmokePackageContractValidator.Validate(options.ManifestPath, packageRootPath, options.WorkloadProfile, KestrelSmokePackageContractValidator.ContractId);
        if (!validation.Succeeded)
        {
            throw new ImageBuilderException(30, validation.Code, validation.Message);
        }

        byte[] packageBytes = OsPackageBuilder.CreatePackageFromDirectory(packageRootPath);
        OsPackageParseResult package = OsPackageParser.Parse(packageBytes);
        if (!package.Succeeded)
        {
            throw new ImageBuilderException(30, "kestrel_package_output_invalid", "Generated Kestrel package failed OSPKG validation: " + package.Status + ".");
        }

        return packageBytes;
    }

    private static FileInfo ValidateEfiPath(string efiPath)
    {
        if (string.IsNullOrWhiteSpace(efiPath))
        {
            throw new ImageBuilderException(3, "efi_not_found", "EFI artifact path is empty.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(efiPath);
        }
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(3, "efi_not_found", "EFI artifact path is invalid: " + efiPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(3, "efi_not_found", "EFI artifact was not found: " + efiPath);
        }

        return file;
    }

    private static FileInfo? ValidateManifestPath(string? manifestPath)
    {
        if (manifestPath is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ImageBuilderException(8, "manifest_not_found", "Manifest path is empty.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(manifestPath);
        }
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(8, "manifest_not_found", "Manifest path is invalid: " + manifestPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(8, "manifest_not_found", "Manifest was not found: " + manifestPath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(9, "manifest_empty", "Manifest is empty: " + manifestPath);
        }

        if (file.Length > ImageBuilderOptions.ManifestMaxBytes)
        {
            throw new ImageBuilderException(10, "manifest_too_large", "Manifest is larger than 64 KiB: " + manifestPath);
        }

        return file;
    }

    private static FileInfo? ValidatePackagePath(string? packagePath)
    {
        if (packagePath is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ImageBuilderException(11, "package_not_found", "Package path is empty.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(packagePath);
        }
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(11, "package_not_found", "Package path is invalid: " + packagePath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(11, "package_not_found", "Package was not found: " + packagePath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(12, "package_empty", "Package is empty: " + packagePath);
        }

        if (file.Length > ImageBuilderOptions.PackageMaxBytes)
        {
            throw new ImageBuilderException(13, "package_too_large", "Package is larger than 16 MiB: " + packagePath);
        }

        return file;
    }

    private static void ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            _ = Path.GetFullPath(outputPath);
        }
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
    }

    private static byte[] DefaultManifestBytes()
    {
        return Encoding.ASCII.GetBytes(DefaultManifestText);
    }

    private static void WriteProtectiveMbr(Stream image, long totalSectors)
    {
        byte[] sector = new byte[SectorSize];
        sector[446 + 4] = 0xEE;
        WriteUInt32(sector, 446 + 8, 1);
        WriteUInt32(sector, 446 + 12, totalSectors - 1 > uint.MaxValue ? uint.MaxValue : (uint)(totalSectors - 1));
        sector[510] = 0x55;
        sector[511] = 0xAA;
        WriteAt(image, 0, sector);
    }

    private static void WriteGpt(Stream image, long totalSectors, long partitionStartLba, long partitionLastLba)
    {
        byte[] entries = new byte[GptEntryCount * GptEntrySize];
        WriteGuid(entries, 0, EfiSystemPartitionType);
        WriteGuid(entries, 16, PartitionGuid);
        WriteUInt64(entries, 32, (ulong)partitionStartLba);
        WriteUInt64(entries, 40, (ulong)partitionLastLba);
        WriteUtf16Name(entries, 56, "EFI System");
        uint entriesCrc = Crc32.Compute(entries);
        long backupEntriesLba = totalSectors - 33;
        long backupHeaderLba = totalSectors - 1;
        long lastUsableLba = totalSectors - 34;
        WriteAt(image, SectorSize * 2L, entries);
        WriteAt(image, backupEntriesLba * SectorSize, entries);
        WriteAt(image, SectorSize, CreateGptHeader(1, backupHeaderLba, 34, lastUsableLba, 2, entriesCrc));
        WriteAt(image, backupHeaderLba * SectorSize, CreateGptHeader(backupHeaderLba, 1, 34, lastUsableLba, backupEntriesLba, entriesCrc));
    }

    private static byte[] CreateGptHeader(long currentLba, long backupLba, long firstUsableLba, long lastUsableLba, long entriesLba, uint entriesCrc)
    {
        byte[] header = new byte[SectorSize];
        Encoding.ASCII.GetBytes("EFI PART").CopyTo(header, 0);
        WriteUInt32(header, 8, 0x00010000);
        WriteUInt32(header, 12, 92);
        WriteUInt64(header, 24, (ulong)currentLba);
        WriteUInt64(header, 32, (ulong)backupLba);
        WriteUInt64(header, 40, (ulong)firstUsableLba);
        WriteUInt64(header, 48, (ulong)lastUsableLba);
        WriteGuid(header, 56, DiskGuid);
        WriteUInt64(header, 72, (ulong)entriesLba);
        WriteUInt32(header, 80, GptEntryCount);
        WriteUInt32(header, 84, GptEntrySize);
        WriteUInt32(header, 88, entriesCrc);
        uint headerCrc = Crc32.Compute(header.AsSpan(0, 92));
        WriteUInt32(header, 16, headerCrc);
        return header;
    }

    private static void WriteFat32Esp(Stream image, long partitionStartLba, uint partitionSectorCount, FatLayout fat, byte[] efiBytes, byte[]? manifestBytes, byte[]? packageBytes)
    {
        WriteAt(image, partitionStartLba * SectorSize, CreateFat32BootSector(partitionStartLba, partitionSectorCount, fat));
        WriteAt(image, (partitionStartLba + 1) * SectorSize, CreateFsInfoSector(fat));
        WriteAt(image, (partitionStartLba + 6) * SectorSize, CreateFat32BootSector(partitionStartLba, partitionSectorCount, fat));
        WriteAt(image, (partitionStartLba + 7) * SectorSize, CreateFsInfoSector(fat));
        byte[] fatBytes = CreateFat(fat);
        long firstFatLba = partitionStartLba + ReservedSectors;
        WriteAt(image, firstFatLba * SectorSize, fatBytes);
        WriteAt(image, (firstFatLba + fat.FatSectors) * SectorSize, fatBytes);
        WriteDirectoryCluster(image, partitionStartLba, fat, RootCluster, CreateRootDirectory(fat));
        WriteDirectoryCluster(image, partitionStartLba, fat, EfiDirectoryCluster, CreateEfiDirectory());
        WriteDirectoryCluster(image, partitionStartLba, fat, BootDirectoryCluster, CreateBootDirectory(efiBytes.Length));
        WriteFileClusters(image, partitionStartLba, fat, BootFileFirstCluster, efiBytes);
        if (fat.HasOsDirectory)
        {
            WriteDirectoryCluster(image, partitionStartLba, fat, fat.OsDirectoryCluster, CreateOsDirectory(fat, manifestBytes?.Length ?? 0, packageBytes?.Length ?? 0));
        }

        if (manifestBytes is not null)
        {
            WriteFileClusters(image, partitionStartLba, fat, fat.ManifestFileFirstCluster, manifestBytes);
        }

        if (packageBytes is not null)
        {
            WriteFileClusters(image, partitionStartLba, fat, fat.PackageFileFirstCluster, packageBytes);
        }
    }

    private static byte[] CreateFat32BootSector(long partitionStartLba, uint partitionSectorCount, FatLayout fat)
    {
        byte[] sector = new byte[SectorSize];
        sector[0] = 0xEB;
        sector[1] = 0x58;
        sector[2] = 0x90;
        WriteAscii(sector, 3, 8, "MSDOS5.0");
        WriteUInt16(sector, 11, SectorSize);
        sector[13] = SectorsPerCluster;
        WriteUInt16(sector, 14, ReservedSectors);
        sector[16] = FatCount;
        WriteUInt16(sector, 17, 0);
        WriteUInt16(sector, 19, 0);
        sector[21] = 0xF8;
        WriteUInt16(sector, 22, 0);
        WriteUInt16(sector, 24, 63);
        WriteUInt16(sector, 26, 255);
        WriteUInt32(sector, 28, (uint)partitionStartLba);
        WriteUInt32(sector, 32, partitionSectorCount);
        WriteUInt32(sector, 36, fat.FatSectors);
        WriteUInt16(sector, 40, 0);
        WriteUInt16(sector, 42, 0);
        WriteUInt32(sector, 44, RootCluster);
        WriteUInt16(sector, 48, 1);
        WriteUInt16(sector, 50, 6);
        sector[64] = 0x80;
        sector[66] = 0x29;
        WriteUInt32(sector, 67, 0x1010_0001);
        WriteAscii(sector, 71, 11, "OS ESP");
        WriteAscii(sector, 82, 8, "FAT32");
        sector[510] = 0x55;
        sector[511] = 0xAA;
        return sector;
    }

    private static byte[] CreateFsInfoSector(FatLayout fat)
    {
        byte[] sector = new byte[SectorSize];
        WriteUInt32(sector, 0, 0x41615252);
        WriteUInt32(sector, 484, 0x61417272);
        uint usedClusters = (uint)fat.UsedClusterCount;
        uint freeClusters = fat.ClusterCount > usedClusters ? fat.ClusterCount - usedClusters : 0;
        WriteUInt32(sector, 488, freeClusters);
        WriteUInt32(sector, 492, (uint)fat.NextFreeCluster);
        WriteUInt32(sector, 508, 0xAA550000);
        return sector;
    }

    private static byte[] CreateFat(FatLayout fat)
    {
        byte[] bytes = new byte[checked((int)fat.FatSectors) * SectorSize];
        WriteFatEntry(bytes, 0, 0x0FFFFFF8);
        WriteFatEntry(bytes, 1, 0x0FFFFFFF);
        WriteFatEntry(bytes, RootCluster, 0x0FFFFFFF);
        WriteFatEntry(bytes, EfiDirectoryCluster, 0x0FFFFFFF);
        WriteFatEntry(bytes, BootDirectoryCluster, 0x0FFFFFFF);
        int fileClusters = fat.FileClusterCount;
        for (int i = 0; i < fileClusters; i++)
        {
            int cluster = BootFileFirstCluster + i;
            uint value = i == fileClusters - 1 ? 0x0FFFFFFF : (uint)(cluster + 1);
            WriteFatEntry(bytes, cluster, value);
        }

        if (fat.HasOsDirectory)
        {
            WriteFatEntry(bytes, fat.OsDirectoryCluster, 0x0FFFFFFF);
        }

        if (fat.HasManifest)
        {
            int manifestClusters = fat.ManifestFileClusterCount;
            for (int i = 0; i < manifestClusters; i++)
            {
                int cluster = fat.ManifestFileFirstCluster + i;
                uint value = i == manifestClusters - 1 ? 0x0FFFFFFF : (uint)(cluster + 1);
                WriteFatEntry(bytes, cluster, value);
            }
        }

        if (fat.HasPackage)
        {
            int packageClusters = fat.PackageFileClusterCount;
            for (int i = 0; i < packageClusters; i++)
            {
                int cluster = fat.PackageFileFirstCluster + i;
                uint value = i == packageClusters - 1 ? 0x0FFFFFFF : (uint)(cluster + 1);
                WriteFatEntry(bytes, cluster, value);
            }
        }

        return bytes;
    }

    private static byte[] CreateRootDirectory(FatLayout fat)
    {
        byte[] directory = new byte[SectorSize * SectorsPerCluster];
        WriteDirectoryEntry(directory, 0, "EFI", string.Empty, 0x10, EfiDirectoryCluster, 0);
        if (fat.HasOsDirectory)
        {
            WriteDirectoryEntry(directory, 32, "OS", string.Empty, 0x10, fat.OsDirectoryCluster, 0);
        }

        return directory;
    }

    private static byte[] CreateEfiDirectory()
    {
        byte[] directory = new byte[SectorSize * SectorsPerCluster];
        WriteDirectoryEntry(directory, 0, ".", string.Empty, 0x10, EfiDirectoryCluster, 0);
        WriteDirectoryEntry(directory, 32, "..", string.Empty, 0x10, RootCluster, 0);
        WriteDirectoryEntry(directory, 64, "BOOT", string.Empty, 0x10, BootDirectoryCluster, 0);
        return directory;
    }

    private static byte[] CreateBootDirectory(int efiLength)
    {
        byte[] directory = new byte[SectorSize * SectorsPerCluster];
        WriteDirectoryEntry(directory, 0, ".", string.Empty, 0x10, BootDirectoryCluster, 0);
        WriteDirectoryEntry(directory, 32, "..", string.Empty, 0x10, EfiDirectoryCluster, 0);
        WriteDirectoryEntry(directory, 64, "BOOTX64", "EFI", 0x20, BootFileFirstCluster, (uint)efiLength);
        return directory;
    }

    private static byte[] CreateOsDirectory(FatLayout fat, int manifestLength, int packageLength)
    {
        byte[] directory = new byte[SectorSize * SectorsPerCluster];
        WriteDirectoryEntry(directory, 0, ".", string.Empty, 0x10, fat.OsDirectoryCluster, 0);
        WriteDirectoryEntry(directory, 32, "..", string.Empty, 0x10, RootCluster, 0);
        int offset = 64;
        if (fat.HasManifest)
        {
            byte[] manifestShortName = CreateShortName(ManifestShortName, ManifestShortExtension);
            WriteLongFileNameEntry(directory, offset, ManifestLongFileName, manifestShortName);
            offset += 32;
            WriteDirectoryEntry(directory, offset, ManifestShortName, ManifestShortExtension, 0x20, fat.ManifestFileFirstCluster, (uint)manifestLength);
            offset += 32;
        }

        if (fat.HasPackage)
        {
            WriteDirectoryEntry(directory, offset, PackageShortName, PackageShortExtension, 0x20, fat.PackageFileFirstCluster, (uint)packageLength);
        }

        return directory;
    }

    private static void WriteDirectoryCluster(Stream image, long partitionStartLba, FatLayout fat, int cluster, byte[] directory)
    {
        WriteAt(image, ClusterOffset(partitionStartLba, fat, cluster), directory);
    }

    private static void WriteFileClusters(Stream image, long partitionStartLba, FatLayout fat, int firstCluster, byte[] bytes)
    {
        int written = 0;
        int cluster = firstCluster;
        while (written < bytes.Length)
        {
            int count = Math.Min(SectorSize * SectorsPerCluster, bytes.Length - written);
            byte[] clusterBytes = new byte[SectorSize * SectorsPerCluster];
            bytes.AsSpan(written, count).CopyTo(clusterBytes);
            WriteAt(image, ClusterOffset(partitionStartLba, fat, cluster), clusterBytes);
            written += count;
            cluster++;
        }
    }

    private static long ClusterOffset(long partitionStartLba, FatLayout fat, int cluster)
    {
        long dataStartLba = partitionStartLba + ReservedSectors + FatCount * fat.FatSectors;
        return (dataStartLba + (cluster - 2L) * SectorsPerCluster) * SectorSize;
    }

    private static void WriteLongFileNameEntry(byte[] directory, int offset, string fileName, ReadOnlySpan<byte> shortName)
    {
        directory[offset] = 0x41;
        directory[offset + 11] = 0x0F;
        directory[offset + 12] = 0;
        directory[offset + 13] = ComputeLongFileNameChecksum(shortName);
        WriteUInt16(directory, offset + 26, 0);
        int fileNameIndex = 0;
        bool wroteTerminator = false;
        WriteLongFileNameChars(directory, offset + 1, 5, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(directory, offset + 14, 6, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(directory, offset + 28, 2, fileName, ref fileNameIndex, ref wroteTerminator);
    }

    private static void WriteLongFileNameChars(byte[] directory, int offset, int count, string fileName, ref int fileNameIndex, ref bool wroteTerminator)
    {
        for (int i = 0; i < count; i++)
        {
            ushort value;
            if (fileNameIndex < fileName.Length)
            {
                value = fileName[fileNameIndex++];
            }
            else if (!wroteTerminator)
            {
                value = 0;
                wroteTerminator = true;
            }
            else
            {
                value = 0xFFFF;
            }

            WriteUInt16(directory, offset + i * 2, value);
        }
    }

    private static byte ComputeLongFileNameChecksum(ReadOnlySpan<byte> shortName)
    {
        byte sum = 0;
        for (int i = 0; i < shortName.Length; i++)
        {
            sum = (byte)(((sum & 1) == 1 ? 0x80 : 0) + (sum >> 1) + shortName[i]);
        }

        return sum;
    }

    private static byte[] CreateShortName(string name, string extension)
    {
        byte[] shortName = new byte[11];
        shortName.AsSpan().Fill(0x20);
        WriteShortNamePart(shortName.AsSpan(0, 8), name);
        WriteShortNamePart(shortName.AsSpan(8, 3), extension);
        return shortName;
    }

    private static void WriteDirectoryEntry(byte[] directory, int offset, string name, string extension, byte attributes, int firstCluster, uint fileSize)
    {
        Span<byte> shortName = directory.AsSpan(offset, 11);
        shortName.Fill(0x20);
        WriteShortNamePart(shortName[..8], name);
        WriteShortNamePart(shortName.Slice(8, 3), extension);
        directory[offset + 11] = attributes;
        WriteUInt16(directory, offset + 20, (ushort)(firstCluster >> 16));
        WriteUInt16(directory, offset + 26, (ushort)(firstCluster & 0xFFFF));
        WriteUInt32(directory, offset + 28, fileSize);
    }

    private static void WriteShortNamePart(Span<byte> destination, string value)
    {
        for (int i = 0; i < value.Length && i < destination.Length; i++)
        {
            destination[i] = (byte)value[i];
        }
    }

    private static void WriteFatEntry(byte[] bytes, int cluster, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cluster * 4, 4), value);
    }

    private static void WriteAt(Stream stream, long offset, byte[] bytes)
    {
        stream.Position = offset;
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteGuid(byte[] bytes, int offset, Guid value)
    {
        value.TryWriteBytes(bytes.AsSpan(offset, 16));
    }

    private static void WriteUtf16Name(byte[] bytes, int offset, string name)
    {
        Encoding.Unicode.GetBytes(name, bytes.AsSpan(offset, 72));
    }

    private static void WriteAscii(byte[] bytes, int offset, int length, string value)
    {
        Span<byte> destination = bytes.AsSpan(offset, length);
        destination.Fill(0x20);
        Encoding.ASCII.GetBytes(value, destination);
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
    }
}
