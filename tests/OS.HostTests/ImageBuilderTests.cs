using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Manifest;
using OS.Storage;

internal static partial class ImageBuilderTests
{
    private const int SectorSize = 512;
    private const ulong PartitionStartLba = 2048;
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-imagebuilder-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            KestrelPublishedImageFixture? publishedKestrelImageFixture = null;
            KestrelPublishedImageFixture GetPublishedKestrelImageFixture()
            {
                publishedKestrelImageFixture ??= CreatePublishedKestrelImageFixture(tempRoot);
                return publishedKestrelImageFixture;
            }

            Run("imagebuilder emits dry-run raw layout plan without writing image", ref failures, () => TestPlanRawDoesNotWrite(tempRoot));
            Run("imagebuilder plan records default workload profile", ref failures, () => TestPlanRawRecordsDefaultWorkloadProfile(tempRoot));
            Run("imagebuilder plan propagates multi workload profile", ref failures, () => TestPlanRawPropagatesMultiWorkloadProfile(tempRoot));
            Run("imagebuilder rejects invalid workload profile", ref failures, () => TestPlanRawRejectsInvalidWorkloadProfile(tempRoot));
            Run("imagebuilder emits dry-run manifest placement plan without writing image", ref failures, () => TestPlanRawWithManifestDoesNotWrite(tempRoot));
            Run("imagebuilder emits dry-run package placement plan without writing image", ref failures, () => TestPlanRawWithPackageDoesNotWrite(tempRoot));
            Run("imagebuilder creates raw GPT FAT32 ESP with BOOTX64.EFI", ref failures, () => TestCreateRawEsp(tempRoot));
            Run("imagebuilder.fat-capacity default accounting payloads determinism", ref failures, () => TestFatCapacity(tempRoot, 64));
            Run("imagebuilder.fat-capacity nearby image", ref failures, () => TestFatCapacity(tempRoot, 65));
            Run("imagebuilder.fat-capacity larger image", ref failures, () => TestFatCapacity(tempRoot, 128));
            Run("imagebuilder.fat-capacity plan size boundaries", ref failures, () => TestFatCapacityPlans(tempRoot));
            Run("imagebuilder.gpt-parser-readback", ref failures, () => TestGptParserReadback(tempRoot));
            Run("imagebuilder.gpt-backup-parser-readback", ref failures, () => TestBackupGptParserReadback(tempRoot));
            Run("imagebuilder.manifest-file", ref failures, () => TestGeneratedManifestCanBeLoaded(tempRoot));
            Run("imagebuilder creates raw ESP with MANIFEST.JSON", ref failures, () => TestCreateRawEspWithManifest(tempRoot));
            Run("imagebuilder creates raw ESP with manifest and APP.PKG", ref failures, () => TestCreateRawEspWithManifestAndPackage(tempRoot));
            Run("imagebuilder.package-file", ref failures, () => TestPackageFileIsPlacedInGeneratedEsp(tempRoot));
            Run("imagebuilder.package-readback", ref failures, () => TestPackageCanBeReadBackFromGeneratedEsp(tempRoot));
            Run("imagebuilder.package-builds-directory", ref failures, () => TestBuildPackageFromDirectory(tempRoot));
            Run("imagebuilder.package-builds-deterministically", ref failures, () => TestBuildPackageDeterministically(tempRoot));
            Run("imagebuilder.package-build rejects unsafe input", ref failures, () => TestBuildPackageRejectsUnsafeInput(tempRoot));
            Run("imagebuilder.native-payload writes v0 header", ref failures, () => TestBuildNativePayloadWritesHeader(tempRoot));
            Run("imagebuilder.native-payload preserves app bytes", ref failures, () => TestBuildNativePayloadPreservesAppBytes(tempRoot));
            Run("imagebuilder.native-payload rejects missing input", ref failures, () => TestBuildNativePayloadRejectsMissingInput(tempRoot));
            Run("imagebuilder.native-payload rejects empty input", ref failures, () => TestBuildNativePayloadRejectsEmptyInput(tempRoot));
            Run("imagebuilder.package validates manifest app payload", ref failures, () => TestValidatePackageAcceptsManifestAppPayload(tempRoot));
            Run("imagebuilder.package validation rejects missing manifest", ref failures, () => TestValidatePackageRejectsMissingManifest(tempRoot));
            Run("imagebuilder.package validation rejects malformed manifest", ref failures, () => TestValidatePackageRejectsMalformedManifest(tempRoot));
            Run("imagebuilder.package validation rejects missing package", ref failures, () => TestValidatePackageRejectsMissingPackage(tempRoot));
            Run("imagebuilder.package validation rejects missing app file", ref failures, () => TestValidatePackageRejectsMissingAppFile(tempRoot));
            Run("imagebuilder.package validation rejects invalid payload header", ref failures, () => TestValidatePackageRejectsInvalidPayloadHeader(tempRoot));
            Run("imagebuilder.native-app-package builds validated package", ref failures, () => TestBuildNativeAppPackageBuildsValidatedPackage(tempRoot));
            Run("imagebuilder.native-app-package preserves manifest app path", ref failures, () => TestBuildNativeAppPackagePreservesManifestAppPath(tempRoot));
            Run("imagebuilder.native-app-package rejects malformed manifest", ref failures, () => TestBuildNativeAppPackageRejectsMalformedManifest(tempRoot));
            Run("imagebuilder.native-app-package rejects empty app input", ref failures, () => TestBuildNativeAppPackageRejectsEmptyAppInput(tempRoot));
            Run("imagebuilder.native-app-image creates raw image with package", ref failures, () => TestBuildNativeAppImageCreatesRawImageWithPackage(tempRoot));
            Run("imagebuilder.native-app-image rejects missing efi", ref failures, () => TestBuildNativeAppImageRejectsMissingEfi(tempRoot));
            Run("imagebuilder.native-app-image rejects malformed manifest", ref failures, () => TestBuildNativeAppImageRejectsMalformedManifest(tempRoot));
            Run("imagebuilder.kestrel_package stages files", ref failures, () => TestKestrelPackageStagesFiles(tempRoot));
            Run("imagebuilder.kestrel_package rejects invalid contract", ref failures, () => TestKestrelPackageRejectsInvalidContract(tempRoot));
            Run("imagebuilder.kestrel_package records single profile", ref failures, () => TestKestrelPackageRecordsSingleProfile(tempRoot));
            Run("imagebuilder.kestrel_package rejects multi profile", ref failures, () => TestKestrelPackageRejectsMultiProfile(tempRoot));
            Run("imagebuilder.image_metadata emits size and sha256", ref failures, () => TestDescribeImageEmitsSizeAndSha256(tempRoot));
            Run("imagebuilder.image_metadata rejects empty input", ref failures, () => TestDescribeImageRejectsEmptyInput(tempRoot));
            Run("imagebuilder.image_metadata rejects unsupported format", ref failures, () => TestDescribeImageRejectsUnsupportedFormat(tempRoot));
            Run("imagebuilder.kestrel_package stages published package", ref failures, () => TestKestrelPackageStagesPublishedPackage(GetPublishedKestrelImageFixture()));
            Run("imagebuilder.kestrel_package readback preserves published files", ref failures, () => TestKestrelPackageReadbackPreservesPublishedFiles(GetPublishedKestrelImageFixture()));
            Run("imagebuilder.kestrel_package readback preserves manifest json", ref failures, () => TestKestrelPackageReadbackPreservesManifestJson(GetPublishedKestrelImageFixture()));
            Run("imagebuilder rejects missing EFI artifact", ref failures, () => TestMissingEfi(tempRoot));
            Run("imagebuilder rejects missing manifest", ref failures, () => TestMissingManifest(tempRoot));
            Run("imagebuilder rejects empty manifest", ref failures, () => TestEmptyManifest(tempRoot));
            Run("imagebuilder rejects oversized manifest", ref failures, () => TestOversizedManifest(tempRoot));
            Run("imagebuilder rejects missing package", ref failures, () => TestMissingPackage(tempRoot));
            Run("imagebuilder rejects empty package", ref failures, () => TestEmptyPackage(tempRoot));
            Run("imagebuilder rejects oversized package", ref failures, () => TestOversizedPackage(tempRoot));
            Run("imagebuilder rejects oversized raw image size", ref failures, () => TestOversizedRawImageSize(tempRoot));
            Run("imagebuilder emits dry-run qcow2 conversion without qemu-img", ref failures, () => TestConvertQcow2DryRunWithoutQemuImg(tempRoot));
            Run("imagebuilder rejects missing qcow2 input", ref failures, () => TestConvertQcow2MissingInput(tempRoot));
            Run("imagebuilder rejects empty qcow2 input", ref failures, () => TestConvertQcow2EmptyInput(tempRoot));
            Run("imagebuilder rejects missing qemu-img in real qcow2 mode", ref failures, () => TestConvertQcow2MissingQemuImgRealMode(tempRoot));
            Run("imagebuilder.azure.vhd validates aligned fixed disk", ref failures, () => TestConvertAzureVhdDryRunAligned(tempRoot));
            Run("imagebuilder.azure.vhd rejects unaligned virtual size", ref failures, () => TestConvertAzureVhdRejectsUnalignedInput(tempRoot));
            Run("imagebuilder.aws.ami validates nitro hvm metadata", ref failures, () => TestPlanAwsAmiDryRun(tempRoot));
            Run("imagebuilder.aws.ami rejects non hvm virtualization", ref failures, () => TestPlanAwsAmiRejectsNonHvm(tempRoot));
            Run("imagebuilder.aws.ami rejects arm64 import path for first target", ref failures, () => TestPlanAwsAmiRejectsArm64(tempRoot));
            Run("imagebuilder.aws.ami validates boot mode matches layout", ref failures, () => TestPlanAwsAmiRejectsLegacyBootMode(tempRoot));
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            // Cleanup failure should not hide image layout validation results.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("Image builder host tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestPlanRawDoesNotWrite(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT.EFI");
        string imagePath = Path.Combine(tempRoot, "planned.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        bool hasExpectedMarkers = result.Output.Contains("TEST_START imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("IMAGE_PLAN ", StringComparison.Ordinal) && result.Output.Contains("IMAGE_PLAN_NO_WRITE " + imagePath, StringComparison.Ordinal);
        bool hasExpectedEntries = result.Output.Contains("\"kind\":\"efi-system-partition\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"efi-boot-file\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\EFI\\\\BOOT\\\\BOOTX64.EFI\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"fat32-os-directory\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"manifest-file\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\OS\\\\MANIFEST.JSON\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"app-package-placeholder\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\OS\\\\APP.PKG\"", StringComparison.Ordinal) && result.Output.Contains("\"writesSuppressed\":true", StringComparison.Ordinal);
        if (!hasExpectedMarkers || !hasExpectedEntries || File.Exists(imagePath))
        {
            PrintToolFailure(result);
            Console.WriteLine("Plan output file exists: " + File.Exists(imagePath));
            return false;
        }

        return true;
    }

    private static bool TestPlanRawRecordsDefaultWorkloadProfile(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT-PROFILE.EFI");
        string imagePath = Path.Combine(tempRoot, "planned-profile.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        return result.Output.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal) && result.Output.Contains("\"workloadProfile\":\"SingleMinimalApi\"", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestPlanRawPropagatesMultiWorkloadProfile(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT-MULTI-PROFILE.EFI");
        string imagePath = Path.Combine(tempRoot, "planned-multi-profile.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64", "--workload-profile", "MultiMinimalApi");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        return result.Output.Contains("WORKLOAD_PROFILE MultiMinimalApi", StringComparison.Ordinal) && result.Output.Contains("\"workloadProfile\":\"MultiMinimalApi\"", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestPlanRawRejectsInvalidWorkloadProfile(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT-BAD-PROFILE.EFI");
        string imagePath = Path.Combine(tempRoot, "planned-bad-profile.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--workload-profile", "BadProfile");
        return result.ExitCode == 2 && result.Output.Contains("TEST_FAIL imagebuilder.plan invalid_args", StringComparison.Ordinal) && result.Output.Contains("Invalid workload profile 'BadProfile'", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestPlanRawWithManifestDoesNotWrite(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT-MANIFEST.EFI");
        string manifestPath = Path.Combine(tempRoot, "manifest.json");
        string imagePath = Path.Combine(tempRoot, "planned-manifest.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        byte[] manifestBytes = CreateManifestBytes();
        File.WriteAllBytes(manifestPath, manifestBytes);
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--manifest", manifestPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        string escapedManifestPath = manifestPath.Replace("\\", "\\\\", StringComparison.Ordinal);
        bool hasManifestEntry = result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"manifest-file\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\OS\\\\MANIFEST.JSON\"", StringComparison.Ordinal) && result.Output.Contains("\"sourcePath\":\"" + escapedManifestPath + "\"", StringComparison.Ordinal) && result.Output.Contains("\"bytes\":" + manifestBytes.Length, StringComparison.Ordinal) && !result.Output.Contains("\"kind\":\"manifest-placeholder\"", StringComparison.Ordinal);
        if (!hasManifestEntry || File.Exists(imagePath))
        {
            PrintToolFailure(result);
            Console.WriteLine("Plan output file exists: " + File.Exists(imagePath));
            return false;
        }

        return true;
    }

    private static bool TestPlanRawWithPackageDoesNotWrite(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "PLANBOOT-PACKAGE.EFI");
        string packagePath = Path.Combine(tempRoot, "app.pkg");
        string imagePath = Path.Combine(tempRoot, "planned-package.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        byte[] packageBytes = CreatePackageBytes();
        File.WriteAllBytes(packagePath, packageBytes);
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--package", packagePath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        string escapedPackagePath = packagePath.Replace("\\", "\\\\", StringComparison.Ordinal);
        bool hasPackageEntry = result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"fat32-os-directory\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"manifest-file\"", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"app-package-file\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\OS\\\\APP.PKG\"", StringComparison.Ordinal) && result.Output.Contains("\"sourcePath\":\"" + escapedPackagePath + "\"", StringComparison.Ordinal) && result.Output.Contains("\"bytes\":" + packageBytes.Length, StringComparison.Ordinal) && !result.Output.Contains("\"kind\":\"manifest-placeholder\"", StringComparison.Ordinal) && !result.Output.Contains("\"kind\":\"app-package-placeholder\"", StringComparison.Ordinal);
        if (!hasPackageEntry || File.Exists(imagePath))
        {
            PrintToolFailure(result);
            Console.WriteLine("Plan output file exists: " + File.Exists(imagePath));
            return false;
        }

        return true;
    }

    private static bool TestCreateRawEsp(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64.EFI");
        string imagePath = Path.Combine(tempRoot, "os.raw");
        byte[] efiBytes = CreateEfiBytes();
        File.WriteAllBytes(efiPath, efiBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        if (image.Length != 64 * 1024 * 1024)
        {
            Console.WriteLine("Unexpected image length: " + image.Length);
            return false;
        }

        return HasProtectiveMbr(image) && HasPrimaryAndBackupGpt(image) && HasFat32EspBootSector(image) && ContainsBootFile(image, efiBytes);
    }

    private static bool TestFatCapacity(string tempRoot, int sizeMiB)
    {
        string prefix = Path.Combine(tempRoot, "fat-capacity-" + sizeMiB);
        byte[] efi = CreateEfiBytes();
        byte[] manifest = CreateManifestBytes();
        byte[] package = CreatePackageBytes();
        File.WriteAllBytes(prefix + ".efi", efi);
        File.WriteAllBytes(prefix + ".json", manifest);
        File.WriteAllBytes(prefix + ".pkg", package);
        string[] options = ["--efi", prefix + ".efi", "--manifest", prefix + ".json", "--package", prefix + ".pkg", "--output", prefix + ".raw", "--image-size-mib", sizeMiB.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        ToolResult plan = RunImageBuilder(["plan-raw", ..options]);
        ToolResult create = RunImageBuilder(["create-raw", ..options]);
        if (plan.ExitCode != 0 || create.ExitCode != 0)
        {
            PrintToolFailure(plan);
            PrintToolFailure(create);
            return false;
        }

        byte[] image = File.ReadAllBytes(prefix + ".raw");
        Dictionary<string, JsonElement> entries = ReadPlanEntries(plan.Output);
        int bootOffset = checked((int)PartitionStartLba * SectorSize);
        ReadOnlySpan<byte> bpb = image.AsSpan(bootOffset, SectorSize);
        ulong bytesPerSector = BinaryPrimitives.ReadUInt16LittleEndian(bpb[11..]);
        ulong sectorsPerCluster = bpb[13];
        ulong reserved = BinaryPrimitives.ReadUInt16LittleEndian(bpb[14..]);
        ulong fatCount = bpb[16];
        ulong totalSectors = BinaryPrimitives.ReadUInt32LittleEndian(bpb[32..]);
        ulong fatSectors = BinaryPrimitives.ReadUInt32LittleEndian(bpb[36..]);
        if (bytesPerSector != SectorSize || sectorsPerCluster != 1 || fatCount != 2 || reserved != 32)
            return false;
        ulong clusters = (totalSectors - reserved - fatCount * fatSectors) / sectorsPerCluster;
        ulong capacity = fatSectors * bytesPerSector / 4;
        if (capacity < clusters + 2)
        {
            Console.WriteLine($"FAT capacity mismatch: imageMiB={sizeMiB} fatEntries={capacity} requiredEntries={clusters + 2}");
            return false;
        }

        int backupBoot = BinaryPrimitives.ReadUInt16LittleEndian(bpb[50..]);
        int fsInfoSector = BinaryPrimitives.ReadUInt16LittleEndian(bpb[48..]);
        ReadOnlySpan<byte> fsInfo = image.AsSpan(bootOffset + fsInfoSector * SectorSize, SectorSize);
        int fatOffset = checked(bootOffset + (int)(reserved * bytesPerSector));
        int fatBytes = checked((int)(fatSectors * bytesPerSector));
        ReadOnlySpan<byte> fat = image.AsSpan(fatOffset, fatBytes);
        if (!bpb.SequenceEqual(image.AsSpan(bootOffset + backupBoot * SectorSize, SectorSize)) || !fsInfo.SequenceEqual(image.AsSpan(bootOffset + (backupBoot + fsInfoSector) * SectorSize, SectorSize)) || !fat.SequenceEqual(image.AsSpan(fatOffset + fatBytes, fatBytes)) || BinaryPrimitives.ReadUInt32LittleEndian(fsInfo) != 0x41615252 || BinaryPrimitives.ReadUInt32LittleEndian(fsInfo[484..]) != 0x61417272 || BinaryPrimitives.ReadUInt32LittleEndian(fsInfo[508..]) != 0xAA550000)
            return false;
        ulong allocated = 0;
        ulong firstFree = 0;
        for (ulong cluster = 2; cluster < clusters + 2; cluster++)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(fat[checked((int)cluster * 4)..]) & 0x0FFFFFFF;
            if (value != 0)
                allocated++;
            else if (firstFree == 0)
                firstFree = cluster;
        }

        ulong plannedAllocated = 0;
        foreach (string kind in new[]
        {
            "fat32-root-directory",
            "fat32-efi-directory",
            "fat32-boot-directory",
            "efi-boot-file",
            "fat32-os-directory",
            "manifest-file",
            "app-package-file"
        }

        )
            plannedAllocated += entries[kind].GetProperty("sectors").GetUInt64() / sectorsPerCluster;
        if (BinaryPrimitives.ReadUInt32LittleEndian(fsInfo[488..]) != clusters - allocated || BinaryPrimitives.ReadUInt32LittleEndian(fsInfo[492..]) != firstFree || allocated != plannedAllocated || entries["efi-system-partition"].GetProperty("sectors").GetUInt64() != totalSectors || entries["fat32-fat-primary"].GetProperty("lba").GetUInt64() != PartitionStartLba + reserved || entries["fat32-fat-primary"].GetProperty("sectors").GetUInt64() != fatSectors || entries["fat32-fat-backup"].GetProperty("lba").GetUInt64() != PartitionStartLba + reserved + fatSectors || entries["fat32-fat-backup"].GetProperty("sectors").GetUInt64() != fatSectors || entries["fat32-root-directory"].GetProperty("lba").GetUInt64() != PartitionStartLba + reserved + fatCount * fatSectors || !ContainsBootFile(image, efi) || !ContainsManifestFile(image, manifest) || !ContainsPackageFile(image, package))
            return false;
        // The second build uses the same inputs and must preserve every image byte.
        create = RunImageBuilder(["create-raw", ..options]);
        if (create.ExitCode != 0)
        {
            PrintToolFailure(create);
            return false;
        }

        using FileStream rebuilt = File.OpenRead(prefix + ".raw");
        return SHA256.HashData(image).AsSpan().SequenceEqual(SHA256.HashData(rebuilt));
    }

    private static bool TestFatCapacityPlans(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "fat-plan.efi");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        foreach (int sizeMiB in new[]
        {
            64,
            65,
            66,
            67,
            127,
            128,
            129,
            1024,
            32768,
            131073,
            133153
        }

        )
        {
            string imagePath = Path.Combine(tempRoot, "fat-plan-" + sizeMiB + ".raw");
            ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", sizeMiB.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (result.ExitCode != 0 || File.Exists(imagePath))
            {
                PrintToolFailure(result);
                return false;
            }

            Dictionary<string, JsonElement> entries = ReadPlanEntries(result.Output);
            ulong partitionSectors = entries["efi-system-partition"].GetProperty("sectors").GetUInt64();
            ulong reserved = entries["fat32-reserved"].GetProperty("sectors").GetUInt64();
            ulong firstFat = entries["fat32-fat-primary"].GetProperty("sectors").GetUInt64();
            ulong secondFat = entries["fat32-fat-backup"].GetProperty("sectors").GetUInt64();
            ulong dataStart = entries["fat32-root-directory"].GetProperty("lba").GetUInt64();
            // Independent geometry from the public plan, using the fixed 512-byte/one-sector-cluster format.
            ulong clusters = partitionSectors - reserved - firstFat - secondFat;
            if (partitionSectors != (ulong)sizeMiB * 2048 - PartitionStartLba - 33 || firstFat != secondFat || firstFat * SectorSize / 4 < clusters + 2 || clusters + 1 >= 0x0FFFFFF7 || (sizeMiB == 133153 && (clusters != 268434367 || clusters + 1 != 0x0FFFFBC0)) || dataStart != PartitionStartLba + reserved + firstFat + secondFat)
            {
                Console.WriteLine($"FAT plan mismatch: imageMiB={sizeMiB} fatSectors={firstFat} dataClusters={clusters}");
                return false;
            }
        }

        foreach (string rejectedSize in new[]
        {
            "63",
            "0",
            "-1",
            "2147483648",
            "133154",
            "2097151",
            "2097152",
            "2097153",
            "2097154",
            "2147483647"
        }

        )
        {
            bool tooLarge = rejectedSize is "133154" or "2097151" or "2097152" or "2097153" or "2097154" or "2147483647";
            string reason = tooLarge ? "image_too_large" : "invalid_args";
            foreach (string command in new[]
            {
                "plan-raw",
                "create-raw"
            }

            )
            {
                string imagePath = Path.Combine(tempRoot, "fat-" + command + "-rejected-" + rejectedSize + ".raw");
                ToolResult result = RunImageBuilder(command, "--efi", efiPath, "--output", imagePath, "--image-size-mib", rejectedSize);
                string marker = command == "plan-raw" ? "imagebuilder.plan" : "imagebuilder.raw";
                if (result.ExitCode != (tooLarge ? 5 : 2) || !result.Output.Contains("TEST_FAIL " + marker + " " + reason, StringComparison.Ordinal) || File.Exists(imagePath))
                {
                    PrintToolFailure(result);
                    return false;
                }
            }
        }

        return true;
    }

    private static Dictionary<string, JsonElement> ReadPlanEntries(string output)
    {
        Dictionary<string, JsonElement> entries = new(StringComparer.Ordinal);
        foreach (string line in output.Split('\n'))
        {
            const string prefix = "IMAGE_PLAN_ENTRY ";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            using JsonDocument document = JsonDocument.Parse(line[prefix.Length..]);
            JsonElement entry = document.RootElement;
            entries.Add(entry.GetProperty("kind").GetString()!, entry.Clone());
        }

        return entries;
    }

    private static bool TestGptParserReadback(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-GPT-PARSER.EFI");
        string imagePath = Path.Combine(tempRoot, "os-gpt-parser.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        ulong blockCount = (ulong)image.Length / SectorSize;
        if (blockCount < 34)
        {
            return false;
        }

        int primaryHeaderOffset = SectorSize;
        ulong entryLba = ReadUInt64(image, primaryHeaderOffset + 72);
        uint entryCount = ReadUInt32(image, primaryHeaderOffset + 80);
        uint entrySize = ReadUInt32(image, primaryHeaderOffset + 84);
        if (entryCount == 0 || entrySize == 0)
        {
            return false;
        }

        ulong entryBytes = checked((ulong)entryCount * entrySize);
        ulong entrySectors = (entryBytes + SectorSize - 1UL) / SectorSize;
        int entryOffset = checked((int)(entryLba * SectorSize));
        int entryLength = checked((int)(entrySectors * SectorSize));
        if (entryOffset < 0 || entryLength < 0 || entryOffset > image.Length - entryLength)
        {
            return false;
        }

        ReadOnlySpan<byte> primaryHeader = image.AsSpan(primaryHeaderOffset, SectorSize);
        ReadOnlySpan<byte> entrySectorsBytes = image.AsSpan(entryOffset, entryLength);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[4];
        GptPartitionTableParseResult parse = GptPartitionTableParser.ParsePrimary(primaryHeader, entrySectorsBytes, blockCount, SectorSize, descriptors);
        if (!parse.Succeeded || parse.Status != GptPartitionTableStatus.Success || parse.Header.CurrentLba != GptPartitionTableParser.PrimaryHeaderLba || parse.Header.LastUsableLba != blockCount - 34 || parse.Header.PartitionEntryLba != entryLba || parse.PartitionsWritten != 1 || parse.EmptyEntriesSkipped != entryCount - 1 || parse.PartitionEntryBytesRequired != entryBytes || parse.PartitionEntrySectorsRequired != entrySectors)
        {
            Console.WriteLine("GPT parser status=" + parse.Status);
            Console.WriteLine("GPT partitions written=" + parse.PartitionsWritten);
            Console.WriteLine("GPT empty entries skipped=" + parse.EmptyEntriesSkipped);
            return false;
        }

        Guid efiSystemPartitionType = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
        ulong expectedLastLba = blockCount - 34;
        ulong expectedPartitionBlocks = expectedLastLba - PartitionStartLba + 1;
        GptPartitionDescriptor esp = descriptors[0];
        return esp.EntryIndex == 0 && esp.EntryByteOffset == 0 && esp.PartitionTypeGuid == efiSystemPartitionType && esp.FirstLba == PartitionStartLba && esp.LastLba == expectedLastLba && expectedPartitionBlocks == blockCount - PartitionStartLba - 33 && esp.NameIsNullTerminated && esp.NameUtf16CodeUnitLength == "EFI System".Length && PartitionDescriptorNameEquals(entrySectorsBytes, esp, "EFI System");
    }

    private static bool TestBackupGptParserReadback(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-GPT-BACKUP-PARSER.EFI");
        string imagePath = Path.Combine(tempRoot, "os-gpt-backup-parser.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        ulong blockCount = (ulong)image.Length / SectorSize;
        if (blockCount < 34)
        {
            return false;
        }

        int backupHeaderOffset = image.Length - SectorSize;
        ulong entryLba = ReadUInt64(image, backupHeaderOffset + 72);
        uint entryCount = ReadUInt32(image, backupHeaderOffset + 80);
        uint entrySize = ReadUInt32(image, backupHeaderOffset + 84);
        if (entryCount == 0 || entrySize == 0)
        {
            return false;
        }

        ulong entryBytes = checked((ulong)entryCount * entrySize);
        ulong entrySectors = (entryBytes + SectorSize - 1UL) / SectorSize;
        int entryOffset = checked((int)(entryLba * SectorSize));
        int entryLength = checked((int)(entrySectors * SectorSize));
        if (entryOffset < 0 || entryLength < 0 || entryOffset > image.Length - entryLength)
        {
            return false;
        }

        ReadOnlySpan<byte> backupHeader = image.AsSpan(backupHeaderOffset, SectorSize);
        ReadOnlySpan<byte> entrySectorsBytes = image.AsSpan(entryOffset, entryLength);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[4];
        GptPartitionTableParseResult parse = GptPartitionTableParser.ParseBackup(backupHeader, entrySectorsBytes, blockCount, SectorSize, descriptors);
        if (!parse.Succeeded || parse.Status != GptPartitionTableStatus.Success || parse.CrcStatus != GptPartitionTableCrcStatus.Validated || parse.Header.CurrentLba != blockCount - 1 || parse.Header.BackupLba != GptPartitionTableParser.PrimaryHeaderLba || parse.Header.LastUsableLba != blockCount - 34 || parse.Header.PartitionEntryLba != entryLba || entryLba != (blockCount - 1) - entrySectors || parse.PartitionsWritten != 1 || parse.EmptyEntriesSkipped != entryCount - 1 || parse.PartitionEntryBytesRequired != entryBytes || parse.PartitionEntrySectorsRequired != entrySectors)
        {
            Console.WriteLine("Backup GPT parser status=" + parse.Status);
            Console.WriteLine("Backup GPT partitions written=" + parse.PartitionsWritten);
            Console.WriteLine("Backup GPT empty entries skipped=" + parse.EmptyEntriesSkipped);
            return false;
        }

        Guid efiSystemPartitionType = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
        ulong expectedLastLba = blockCount - 34;
        GptPartitionDescriptor esp = descriptors[0];
        return esp.EntryIndex == 0 && esp.EntryByteOffset == 0 && esp.PartitionTypeGuid == efiSystemPartitionType && esp.FirstLba == PartitionStartLba && esp.LastLba == expectedLastLba && esp.NameIsNullTerminated && esp.NameUtf16CodeUnitLength == "EFI System".Length && PartitionDescriptorNameEquals(entrySectorsBytes, esp, "EFI System");
    }

    private static bool TestGeneratedManifestCanBeLoaded(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-DEFAULT-MANIFEST.EFI");
        string imagePath = Path.Combine(tempRoot, "os-default-manifest.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];
        NativeManifestLoadResult load = NativeManifestLoader.LoadFromPrimaryGptEsp(MemoryBlockDevice.FromImage(image, (uint)SectorSize), buffer);
        return load.Succeeded && load.Status == NativeManifestLoadStatus.Success && load.GptStatus == GptStatus.Success && load.Fat32Status == Fat32Status.Success && load.ManifestStatus == NativeManifestParseStatus.Success && FieldEquals(buffer, load.Manifest.AppName, "os.default") && FieldEquals(buffer, load.Manifest.AppPath, "apps/hello.bin") && FieldEquals(buffer, load.Manifest.Args, string.Empty) && load.Manifest.NetworkMode == NativeManifestNetworkMode.None && load.Manifest.LogMode == NativeManifestLogMode.Serial;
    }

    private static bool TestCreateRawEspWithManifest(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-MANIFEST.EFI");
        string manifestPath = Path.Combine(tempRoot, "manifest-to-write.json");
        string imagePath = Path.Combine(tempRoot, "os-manifest.raw");
        byte[] efiBytes = CreateEfiBytes();
        byte[] manifestBytes = CreateManifestBytes();
        File.WriteAllBytes(efiPath, efiBytes);
        File.WriteAllBytes(manifestPath, manifestBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        return ContainsBootFile(image, efiBytes) && ContainsManifestFile(image, manifestBytes);
    }

    private static bool TestCreateRawEspWithManifestAndPackage(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-PACKAGE.EFI");
        string manifestPath = Path.Combine(tempRoot, "manifest-package.json");
        string packagePath = Path.Combine(tempRoot, "app-package.pkg");
        string imagePath = Path.Combine(tempRoot, "os-package.raw");
        byte[] efiBytes = CreateEfiBytes();
        byte[] manifestBytes = CreateManifestBytes();
        byte[] packageBytes = CreatePackageBytes();
        File.WriteAllBytes(efiPath, efiBytes);
        File.WriteAllBytes(manifestPath, manifestBytes);
        File.WriteAllBytes(packagePath, packageBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--package", packagePath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        return ContainsBootFile(image, efiBytes) && ContainsManifestFile(image, manifestBytes) && ContainsPackageFile(image, packageBytes);
    }

    private static bool TestPackageFileIsPlacedInGeneratedEsp(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-PACKAGE-FILE.EFI");
        string manifestPath = Path.Combine(tempRoot, "manifest-package-file.json");
        string packagePath = Path.Combine(tempRoot, "app-package-file.pkg");
        string imagePath = Path.Combine(tempRoot, "os-package-file.raw");
        byte[] packageBytes = CreateValidPackageBytes();
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(packagePath, packageBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--package", packagePath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        return ContainsPackageFile(image, packageBytes);
    }

    private static bool TestPackageCanBeReadBackFromGeneratedEsp(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-PACKAGE-READBACK.EFI");
        string manifestPath = Path.Combine(tempRoot, "manifest-package-readback.json");
        string packagePath = Path.Combine(tempRoot, "app-package-readback.pkg");
        string imagePath = Path.Combine(tempRoot, "os-package-readback.raw");
        byte[] packageBytes = CreateValidPackageBytes();
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(packagePath, packageBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--package", packagePath, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        byte[] packageBuffer = new byte[packageBytes.Length];
        OsPackageLoadResult load = OsPackageLoader.LoadFromPrimaryGptEsp(MemoryBlockDevice.FromImage(image, (uint)SectorSize), packageBuffer);
        if (!load.Succeeded || load.Status != OsPackageLoadStatus.Success || load.GptStatus != GptStatus.Success || load.Fat32Status != Fat32Status.Success || load.PackageStatus != OsPackageStatus.Success || load.PackageHeader.FileCount != 1 || load.BytesRead != packageBytes.Length)
        {
            return false;
        }

        OsPackageFileContentResult content = OsPackageParser.GetFileContent(packageBuffer.AsSpan(0, checked((int)load.BytesRead)), "bin/app.dll"u8);
        return content.Succeeded && content.Content.Length == 4 && content.Content[0] == 0xCA && content.Content[1] == 0xFE && content.Content[2] == 0xBA && content.Content[3] == 0xBE;
    }

    private static bool TestBuildPackageFromDirectory(string tempRoot)
    {
        string inputRoot = Path.Combine(tempRoot, "package-input");
        string binDirectory = Path.Combine(inputRoot, "bin");
        string configDirectory = Path.Combine(inputRoot, "config");
        string packagePath = Path.Combine(tempRoot, "built-package.pkg");
        Directory.CreateDirectory(binDirectory);
        Directory.CreateDirectory(configDirectory);
        byte[] appBytes = new byte[]
        {
            0x01,
            0x02,
            0x03,
            0x04
        };
        byte[] configBytes = System.Text.Encoding.ASCII.GetBytes("mode=test\n");
        File.WriteAllBytes(Path.Combine(configDirectory, "app.txt"), configBytes);
        File.WriteAllBytes(Path.Combine(binDirectory, "app.dll"), appBytes);
        ToolResult result = RunImageBuilder("build-package", "--input", inputRoot, "--output", packagePath);
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.package", StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.package", StringComparison.Ordinal) || !result.Output.Contains("PACKAGE_FILE_COUNT 2", StringComparison.Ordinal) || !File.Exists(packagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] package = File.ReadAllBytes(packagePath);
        OsPackageParseResult parse = OsPackageParser.Parse(package);
        if (!parse.Succeeded || parse.Header.FileCount != 2)
        {
            return false;
        }

        OsPackageFileContentResult app = OsPackageParser.GetFileContent(package, "bin/app.dll"u8);
        OsPackageFileContentResult config = OsPackageParser.GetFileContent(package, "config/app.txt"u8);
        return app.Succeeded && config.Succeeded && app.Content.SequenceEqual(appBytes) && config.Content.SequenceEqual(configBytes);
    }

    private static bool TestBuildPackageDeterministically(string tempRoot)
    {
        string inputRoot = Path.Combine(tempRoot, "package-deterministic-input");
        Directory.CreateDirectory(Path.Combine(inputRoot, "z"));
        Directory.CreateDirectory(Path.Combine(inputRoot, "a"));
        File.WriteAllBytes(Path.Combine(inputRoot, "z", "last.bin"), new byte[] { 0x7A, 0x7B });
        File.WriteAllBytes(Path.Combine(inputRoot, "a", "first.bin"), new byte[] { 0x01, 0x02, 0x03 });
        string firstPackagePath = Path.Combine(tempRoot, "first-deterministic.pkg");
        string secondPackagePath = Path.Combine(tempRoot, "second-deterministic.pkg");
        ToolResult first = RunImageBuilder("build-package", "--input", inputRoot, "--output", firstPackagePath);
        ToolResult second = RunImageBuilder("build-package", "--input", inputRoot, "--output", secondPackagePath);
        if (first.ExitCode != 0 || second.ExitCode != 0 || !first.Output.Contains("TEST_PASS imagebuilder.package", StringComparison.Ordinal) || !second.Output.Contains("TEST_PASS imagebuilder.package", StringComparison.Ordinal))
        {
            PrintToolFailure(first);
            PrintToolFailure(second);
            return false;
        }

        byte[] firstBytes = File.ReadAllBytes(firstPackagePath);
        byte[] secondBytes = File.ReadAllBytes(secondPackagePath);
        return firstBytes.SequenceEqual(secondBytes) && OsPackageParser.Parse(firstBytes).Succeeded;
    }

    private static bool TestBuildPackageRejectsUnsafeInput(string tempRoot)
    {
        string inputRoot = Path.Combine(tempRoot, "package-unsafe-input");
        Directory.CreateDirectory(inputRoot);
        string unsafePath = Path.Combine(inputRoot, "nonascii-" + (char)0x00E9 + ".txt");
        string packagePath = Path.Combine(tempRoot, "unsafe.pkg");
        File.WriteAllBytes(unsafePath, new byte[] { 0x01 });
        ToolResult result = RunImageBuilder("build-package", "--input", inputRoot, "--output", packagePath);
        return result.ExitCode == 18 && result.Output.Contains("TEST_START imagebuilder.package", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package package_input_unsafe_path", StringComparison.Ordinal) && !File.Exists(packagePath);
    }

    private static bool TestBuildNativePayloadWritesHeader(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "native-input.bin");
        string outputPath = Path.Combine(tempRoot, "native-payload.bin");
        byte[] appBytes = new byte[]
        {
            0xC3,
            0x90,
            0xCC
        };
        File.WriteAllBytes(inputPath, appBytes);
        ToolResult result = RunImageBuilder("build-native-payload", "--input", inputPath, "--output", outputPath);
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.native-payload", StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.native-payload", StringComparison.Ordinal) || !result.Output.Contains("NATIVE_PAYLOAD_ENTRY_OFFSET 32", StringComparison.Ordinal) || !File.Exists(outputPath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] payload = File.ReadAllBytes(outputPath);
        NativeAppPayloadHeaderResult header = NativeAppPayloadHeader.Parse(payload);
        return payload.Length == NativeAppPayloadHeader.HeaderSize + appBytes.Length && header.Succeeded && header.Header.Version == NativeAppPayloadHeader.SupportedVersion && header.Header.HeaderSizeValue == NativeAppPayloadHeader.HeaderSize && header.Header.Flags == 0 && header.Header.EntryOffset == NativeAppPayloadHeader.HeaderSize && ReadUInt64(payload, 24) == 0;
    }

    private static bool TestBuildNativePayloadPreservesAppBytes(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "native-preserve-input.bin");
        string outputPath = Path.Combine(tempRoot, "native-preserve-payload.bin");
        byte[] appBytes = new byte[]
        {
            0x00,
            0x7F,
            0x80,
            0xFE,
            0xFF
        };
        File.WriteAllBytes(inputPath, appBytes);
        ToolResult result = RunImageBuilder("build-native-payload", "--input", inputPath, "--output", outputPath);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] payload = File.ReadAllBytes(outputPath);
        return payload.AsSpan(NativeAppPayloadHeader.HeaderSize).SequenceEqual(appBytes);
    }

    private static bool TestBuildNativePayloadRejectsMissingInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "missing-native-input.bin");
        string outputPath = Path.Combine(tempRoot, "missing-native-output.bin");
        ToolResult result = RunImageBuilder("build-native-payload", "--input", inputPath, "--output", outputPath);
        return result.ExitCode == 21 && result.Output.Contains("TEST_START imagebuilder.native-payload", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-payload native_payload_input_not_found", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestBuildNativePayloadRejectsEmptyInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "empty-native-input.bin");
        string outputPath = Path.Combine(tempRoot, "empty-native-output.bin");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("build-native-payload", "--input", inputPath, "--output", outputPath);
        return result.ExitCode == 22 && result.Output.Contains("TEST_START imagebuilder.native-payload", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-payload native_payload_input_empty", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestValidatePackageAcceptsManifestAppPayload(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "validate-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "validate-package.pkg");
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(packagePath, CreateNativePayloadPackageBytes("apps/hello.bin", validPayloadHeader: true));
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        bool hasExpectedOutput = result.ExitCode == 0 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("PACKAGE_APP_PATH apps/hello.bin", StringComparison.Ordinal) && result.Output.Contains("PACKAGE_APP_BYTES 40", StringComparison.Ordinal) && result.Output.Contains("PACKAGE_APP_ENTRY_OFFSET 32", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.package-validation", StringComparison.Ordinal);
        if (!hasExpectedOutput)
        {
            PrintToolFailure(result);
            return false;
        }

        return true;
    }

    private static bool TestValidatePackageRejectsMissingManifest(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "missing-validate-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "missing-manifest-package.pkg");
        File.WriteAllBytes(packagePath, CreateNativePayloadPackageBytes("apps/hello.bin", validPayloadHeader: true));
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        return result.ExitCode == 8 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package-validation manifest_not_found", StringComparison.Ordinal);
    }

    private static bool TestValidatePackageRejectsMalformedManifest(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "malformed-validate-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "malformed-manifest-package.pkg");
        File.WriteAllBytes(manifestPath, System.Text.Encoding.ASCII.GetBytes("format=os-native-manifest\n"));
        File.WriteAllBytes(packagePath, CreateNativePayloadPackageBytes("apps/hello.bin", validPayloadHeader: true));
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        return result.ExitCode == 23 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package-validation manifest_malformed", StringComparison.Ordinal);
    }

    private static bool TestValidatePackageRejectsMissingPackage(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "missing-package-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "missing-validate-package.pkg");
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        return result.ExitCode == 11 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package-validation package_not_found", StringComparison.Ordinal);
    }

    private static bool TestValidatePackageRejectsMissingAppFile(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "missing-app-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "missing-app-package.pkg");
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(packagePath, CreateNativePayloadPackageBytes("apps/other.bin", validPayloadHeader: true));
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        return result.ExitCode == 26 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package-validation package_app_file_missing", StringComparison.Ordinal);
    }

    private static bool TestValidatePackageRejectsInvalidPayloadHeader(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "invalid-payload-manifest.txt");
        string packagePath = Path.Combine(tempRoot, "invalid-payload-package.pkg");
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(packagePath, CreateNativePayloadPackageBytes("apps/hello.bin", validPayloadHeader: false));
        ToolResult result = RunImageBuilder("validate-package", "--manifest", manifestPath, "--package", packagePath);
        return result.ExitCode == 27 && result.Output.Contains("TEST_START imagebuilder.package-validation", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.package-validation package_app_payload_invalid", StringComparison.Ordinal);
    }

    private static bool TestBuildNativeAppPackageBuildsValidatedPackage(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "native-app-package-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-package-input.bin");
        string packagePath = Path.Combine(tempRoot, "native-app-package.pkg");
        byte[] appBytes = new byte[]
        {
            0xC3,
            0x90,
            0xCC,
            0x7F
        };
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(inputPath, appBytes);
        ToolResult result = RunImageBuilder("build-native-app-package", "--manifest", manifestPath, "--input", inputPath, "--output", packagePath);
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.native-app-package", StringComparison.Ordinal) || !result.Output.Contains("NATIVE_APP_PACKAGE_PATH apps/hello.bin", StringComparison.Ordinal) || !result.Output.Contains("NATIVE_APP_PACKAGE_RAW_APP_BYTES " + appBytes.Length, StringComparison.Ordinal) || !result.Output.Contains("NATIVE_APP_PACKAGE_ENTRY_OFFSET 32", StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.native-app-package", StringComparison.Ordinal) || !File.Exists(packagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] package = File.ReadAllBytes(packagePath);
        OsPackageParseResult packageParse = OsPackageParser.Parse(package);
        if (!packageParse.Succeeded || packageParse.Header.FileCount != 1)
        {
            return false;
        }

        byte[] appPath = System.Text.Encoding.ASCII.GetBytes("apps/hello.bin");
        OsPackageFileContentResult content = OsPackageParser.GetFileContent(package, appPath);
        NativeAppPayloadHeaderResult header = content.Succeeded ? NativeAppPayloadHeader.Parse(content.Content) : NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.PayloadTooSmall);
        return content.Succeeded && header.Succeeded && content.Content.Length == NativeAppPayloadHeader.HeaderSize + appBytes.Length && content.Content.Slice(NativeAppPayloadHeader.HeaderSize).SequenceEqual(appBytes);
    }

    private static bool TestBuildNativeAppPackagePreservesManifestAppPath(string tempRoot)
    {
        const string manifestAppPath = "apps/custom/native.bin";
        string manifestPath = Path.Combine(tempRoot, "native-app-package-custom-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-package-custom-input.bin");
        string packagePath = Path.Combine(tempRoot, "native-app-package-custom.pkg");
        byte[] appBytes = new byte[]
        {
            0x01,
            0x02,
            0x03
        };
        File.WriteAllBytes(manifestPath, CreateManifestBytesForAppPath(manifestAppPath));
        File.WriteAllBytes(inputPath, appBytes);
        ToolResult result = RunImageBuilder("build-native-app-package", "--manifest", manifestPath, "--input", inputPath, "--output", packagePath);
        if (result.ExitCode != 0 || !result.Output.Contains("NATIVE_APP_PACKAGE_PATH " + manifestAppPath, StringComparison.Ordinal) || !File.Exists(packagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] package = File.ReadAllBytes(packagePath);
        byte[] manifestPathBytes = System.Text.Encoding.ASCII.GetBytes(manifestAppPath);
        OsPackageFileContentResult selectedContent = OsPackageParser.GetFileContent(package, manifestPathBytes);
        OsPackageFileContentResult defaultContent = OsPackageParser.GetFileContent(package, "apps/hello.bin"u8);
        return selectedContent.Succeeded && selectedContent.Content.Length == NativeAppPayloadHeader.HeaderSize + appBytes.Length && selectedContent.Content.Slice(NativeAppPayloadHeader.HeaderSize).SequenceEqual(appBytes) && !defaultContent.Succeeded && defaultContent.Status == OsPackageStatus.MissingFile;
    }

    private static bool TestBuildNativeAppPackageRejectsMalformedManifest(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "native-app-package-malformed-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-package-malformed-input.bin");
        string packagePath = Path.Combine(tempRoot, "native-app-package-malformed.pkg");
        File.WriteAllBytes(manifestPath, System.Text.Encoding.ASCII.GetBytes("format=os-native-manifest\n"));
        File.WriteAllBytes(inputPath, new byte[] { 0xC3 });
        ToolResult result = RunImageBuilder("build-native-app-package", "--manifest", manifestPath, "--input", inputPath, "--output", packagePath);
        return result.ExitCode == 23 && result.Output.Contains("TEST_START imagebuilder.native-app-package", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-app-package manifest_malformed", StringComparison.Ordinal) && !File.Exists(packagePath);
    }

    private static bool TestBuildNativeAppPackageRejectsEmptyAppInput(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "native-app-package-empty-input-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-package-empty-input.bin");
        string packagePath = Path.Combine(tempRoot, "native-app-package-empty-input.pkg");
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("build-native-app-package", "--manifest", manifestPath, "--input", inputPath, "--output", packagePath);
        return result.ExitCode == 22 && result.Output.Contains("TEST_START imagebuilder.native-app-package", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-app-package native_payload_input_empty", StringComparison.Ordinal) && !File.Exists(packagePath);
    }

    private static bool TestBuildNativeAppImageCreatesRawImageWithPackage(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-NATIVE-APP-IMAGE.EFI");
        string manifestPath = Path.Combine(tempRoot, "native-app-image-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-image-input.bin");
        string imagePath = Path.Combine(tempRoot, "native-app-image.raw");
        string packagePath = Path.GetFullPath(imagePath) + ".app.pkg";
        byte[] efiBytes = CreateEfiBytes();
        byte[] manifestBytes = CreateManifestBytes();
        byte[] appBytes = new byte[]
        {
            0xC3,
            0x90,
            0xCC,
            0x7F,
            0x01
        };
        File.WriteAllBytes(efiPath, efiBytes);
        File.WriteAllBytes(manifestPath, manifestBytes);
        File.WriteAllBytes(inputPath, appBytes);
        ToolResult result = RunImageBuilder("build-native-app-image", "--efi", efiPath, "--manifest", manifestPath, "--input", inputPath, "--output", imagePath);
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.native-app-image", StringComparison.Ordinal) || !result.Output.Contains("NATIVE_APP_IMAGE_PACKAGE_PATH " + packagePath, StringComparison.Ordinal) || !result.Output.Contains("NATIVE_APP_IMAGE_RAW_PATH " + imagePath, StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.native-app-image", StringComparison.Ordinal) || !File.Exists(packagePath) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] package = File.ReadAllBytes(packagePath);
        OsPackageParseResult packageParse = OsPackageParser.Parse(package);
        if (!packageParse.Succeeded || packageParse.Header.FileCount != 1)
        {
            return false;
        }

        OsPackageFileContentResult content = OsPackageParser.GetFileContent(package, "apps/hello.bin"u8);
        NativeAppPayloadHeaderResult header = content.Succeeded ? NativeAppPayloadHeader.Parse(content.Content) : NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.PayloadTooSmall);
        if (!content.Succeeded || !header.Succeeded || content.Content.Length != NativeAppPayloadHeader.HeaderSize + appBytes.Length || !content.Content.Slice(NativeAppPayloadHeader.HeaderSize).SequenceEqual(appBytes))
        {
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        return ContainsBootFile(image, efiBytes) && ContainsManifestFile(image, manifestBytes) && ContainsPackageFile(image, package);
    }

    private static bool TestBuildNativeAppImageRejectsMissingEfi(string tempRoot)
    {
        string manifestPath = Path.Combine(tempRoot, "native-app-image-missing-efi-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-image-missing-efi-input.bin");
        string imagePath = Path.Combine(tempRoot, "native-app-image-missing-efi.raw");
        string packagePath = Path.GetFullPath(imagePath) + ".app.pkg";
        File.WriteAllBytes(manifestPath, CreateManifestBytes());
        File.WriteAllBytes(inputPath, new byte[] { 0xC3 });
        ToolResult result = RunImageBuilder("build-native-app-image", "--efi", Path.Combine(tempRoot, "missing-native-app-image.efi"), "--manifest", manifestPath, "--input", inputPath, "--output", imagePath);
        return result.ExitCode == 3 && result.Output.Contains("TEST_START imagebuilder.native-app-image", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-app-image efi_not_found", StringComparison.Ordinal) && !File.Exists(packagePath) && !File.Exists(imagePath);
    }

    private static bool TestBuildNativeAppImageRejectsMalformedManifest(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-NATIVE-APP-IMAGE-MALFORMED.EFI");
        string manifestPath = Path.Combine(tempRoot, "native-app-image-malformed-manifest.txt");
        string inputPath = Path.Combine(tempRoot, "native-app-image-malformed-input.bin");
        string imagePath = Path.Combine(tempRoot, "native-app-image-malformed.raw");
        string packagePath = Path.GetFullPath(imagePath) + ".app.pkg";
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, System.Text.Encoding.ASCII.GetBytes("format=os-native-manifest\n"));
        File.WriteAllBytes(inputPath, new byte[] { 0xC3 });
        ToolResult result = RunImageBuilder("build-native-app-image", "--efi", efiPath, "--manifest", manifestPath, "--input", inputPath, "--output", imagePath);
        return result.ExitCode == 23 && result.Output.Contains("TEST_START imagebuilder.native-app-image", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.native-app-image manifest_malformed", StringComparison.Ordinal) && !File.Exists(packagePath) && !File.Exists(imagePath);
    }

    private static bool TestKestrelPackageStagesFiles(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-KESTREL.EFI");
        string manifestPath = Path.Combine(tempRoot, "kestrel-manifest.json");
        string packageRoot = CreateKestrelPackageRoot(tempRoot, "kestrel-package-root");
        string imagePath = Path.Combine(tempRoot, "kestrel-package.raw");
        byte[] manifestBytes = CreateKestrelManifestBytes();
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, manifestBytes);
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--kestrel-package", packageRoot, "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0 || !result.Output.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) || !result.Output.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal) || !result.Output.Contains("KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0", StringComparison.Ordinal) || !result.Output.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            PrintToolFailure(result);
            return false;
        }

        byte[] image = File.ReadAllBytes(imagePath);
        byte[] packageBuffer = new byte[64 * 1024];
        OsPackageLoadResult load = OsPackageLoader.LoadFromPrimaryGptEsp(MemoryBlockDevice.FromImage(image, (uint)SectorSize), packageBuffer);
        if (!load.Succeeded || load.Status != OsPackageLoadStatus.Success || load.PackageStatus != OsPackageStatus.Success || load.PackageHeader.FileCount != 4)
        {
            return false;
        }

        ReadOnlySpan<byte> package = packageBuffer.AsSpan(0, checked((int)load.BytesRead));
        OsPackageFileContentResult app = OsPackageParser.GetFileContent(package, "apps/minimal-api/MinimalApiSmoke.dll"u8);
        OsPackageFileContentResult config = OsPackageParser.GetFileContent(package, "apps/minimal-api/appsettings.json"u8);
        return ContainsManifestFile(image, manifestBytes) && app.Succeeded && config.Succeeded && SpanEqualsAscii(app.Content, "fixture:apps/minimal-api/MinimalApiSmoke.dll") && SpanEqualsAscii(config.Content, "fixture:apps/minimal-api/appsettings.json");
    }

    private static bool TestKestrelPackageRejectsInvalidContract(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-KESTREL-BAD.EFI");
        string manifestPath = Path.Combine(tempRoot, "kestrel-manifest-no-env.json");
        string packageRoot = CreateKestrelPackageRoot(tempRoot, "kestrel-package-invalid");
        string imagePath = Path.Combine(tempRoot, "kestrel-invalid.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, CreateKestrelManifestWithoutExpectedBodyBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--manifest", manifestPath, "--kestrel-package", packageRoot, "--output", imagePath, "--image-size-mib", "64");
        return result.ExitCode == 30 && !File.Exists(imagePath) && result.Output.Contains("TEST_START imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.plan kestrel_package_missing_expected_env", StringComparison.Ordinal) && result.Output.Contains("OS_SMOKE_EXPECTED_BODY", StringComparison.Ordinal);
    }

    private static bool TestKestrelPackageRecordsSingleProfile(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-KESTREL-PLAN.EFI");
        string manifestPath = Path.Combine(tempRoot, "kestrel-manifest-plan.json");
        string packageRoot = CreateKestrelPackageRoot(tempRoot, "kestrel-package-plan");
        string imagePath = Path.Combine(tempRoot, "kestrel-plan.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, CreateKestrelManifestBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--manifest", manifestPath, "--kestrel-package", packageRoot, "--workload-profile", "SingleMinimalApi", "--output", imagePath, "--image-size-mib", "64");
        if (result.ExitCode != 0)
        {
            PrintToolFailure(result);
            return false;
        }

        string escapedPackageRoot = packageRoot.Replace("\\", "\\\\", StringComparison.Ordinal);
        return !File.Exists(imagePath) && result.Output.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal) && result.Output.Contains("KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0", StringComparison.Ordinal) && result.Output.Contains("\"kind\":\"app-package-file\"", StringComparison.Ordinal) && result.Output.Contains("\"guestPath\":\"\\\\OS\\\\APP.PKG\"", StringComparison.Ordinal) && result.Output.Contains("\"sourcePath\":\"" + escapedPackageRoot + "\"", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.plan", StringComparison.Ordinal);
    }

    private static bool TestKestrelPackageRejectsMultiProfile(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-KESTREL-MULTI.EFI");
        string manifestPath = Path.Combine(tempRoot, "kestrel-manifest-multi.json");
        string packageRoot = CreateKestrelPackageRoot(tempRoot, "kestrel-package-multi");
        string imagePath = Path.Combine(tempRoot, "kestrel-multi.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, CreateKestrelManifestBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--manifest", manifestPath, "--kestrel-package", packageRoot, "--workload-profile", "MultiMinimalApi", "--output", imagePath, "--image-size-mib", "64");
        return result.ExitCode == 30 && !File.Exists(imagePath) && result.Output.Contains("WORKLOAD_PROFILE MultiMinimalApi", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.plan kestrel_package_unsupported_profile", StringComparison.Ordinal) && result.Output.Contains("SingleMinimalApi only", StringComparison.Ordinal);
    }

    private static bool TestKestrelPackageStagesPublishedPackage(KestrelPublishedImageFixture fixture)
    {
        bool scriptMarkers = fixture.PublishOutput.Contains("TEST_PASS kestrel.smoke_app publishes package", StringComparison.Ordinal) && fixture.PublishOutput.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal) && fixture.PublishOutput.Contains("TEST_PASS kestrel.smoke_app validates package contract", StringComparison.Ordinal) && OutputHasLine(fixture.PublishOutput, "TEST_PASS kestrel.smoke_app");
        bool imageMarkers = fixture.ImageBuilderOutput.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) && fixture.ImageBuilderOutput.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal) && fixture.ImageBuilderOutput.Contains("KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0", StringComparison.Ordinal) && fixture.ImageBuilderOutput.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal);
        if (!scriptMarkers || !imageMarkers || !File.Exists(fixture.ImagePath) || !fixture.PackageLoadSucceeded)
        {
            Console.WriteLine("Publish output=" + fixture.PublishOutput.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
            Console.WriteLine("ImageBuilder output=" + fixture.ImageBuilderOutput.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
            Console.WriteLine("Image exists=" + File.Exists(fixture.ImagePath));
            Console.WriteLine("Package load status=" + fixture.PackageLoadStatus);
            Console.WriteLine("Package parser status=" + fixture.PackageStatus);
            return false;
        }

        return true;
    }

    private static bool TestKestrelPackageReadbackPreservesPublishedFiles(KestrelPublishedImageFixture fixture)
    {
        if (!fixture.PackageLoadSucceeded)
        {
            Console.WriteLine("Package load status=" + fixture.PackageLoadStatus);
            Console.WriteLine("Package parser status=" + fixture.PackageStatus);
            return false;
        }

        return PackageFileEqualsPublishedFile(fixture.PackageBytes, fixture.PackageRootPath, "apps/minimal-api/MinimalApiSmoke.dll") && PackageFileEqualsPublishedFile(fixture.PackageBytes, fixture.PackageRootPath, "apps/minimal-api/MinimalApiSmoke.deps.json") && PackageFileEqualsPublishedFile(fixture.PackageBytes, fixture.PackageRootPath, "apps/minimal-api/MinimalApiSmoke.runtimeconfig.json") && PackageFileEqualsPublishedFile(fixture.PackageBytes, fixture.PackageRootPath, "apps/minimal-api/appsettings.json") && PackageFileContainsAscii(fixture.PackageBytes, "apps/minimal-api/appsettings.json", "OS_SMOKE_EXPECTED_BODY");
    }

    private static bool TestKestrelPackageReadbackPreservesManifestJson(KestrelPublishedImageFixture fixture)
    {
        return fixture.ManifestReadbackSucceeded && fixture.ManifestBytesFromImage.SequenceEqual(fixture.ManifestBytesFromPublish);
    }

    private static KestrelPublishedImageFixture CreatePublishedKestrelImageFixture(string tempRoot)
    {
        string outputRoot = Path.Combine(tempRoot, "published-kestrel-smoke");
        string packageRoot = Path.Combine(outputRoot, "package-root");
        string manifestPath = Path.Combine(outputRoot, "package.json");
        string efiPath = Path.Combine(tempRoot, "BOOTX64-KESTREL-PUBLISHED.EFI");
        string imagePath = Path.Combine(tempRoot, "kestrel-published.raw");
        ToolResult publish = RunKestrelPublish(outputRoot);
        if (publish.ExitCode != 0)
        {
            return KestrelPublishedImageFixture.CreateFailed(publish.Output, imageBuilderOutput: string.Empty, imagePath, packageRoot);
        }

        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult imageBuilder = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--kestrel-package", packageRoot, "--output", imagePath, "--image-size-mib", "64");
        if (imageBuilder.ExitCode != 0 || !File.Exists(imagePath))
        {
            return KestrelPublishedImageFixture.CreateFailed(publish.Output, imageBuilder.Output, imagePath, packageRoot);
        }

        byte[] image = File.ReadAllBytes(imagePath);
        byte[] manifestBytesFromPublish = File.Exists(manifestPath) ? File.ReadAllBytes(manifestPath) : Array.Empty<byte>();
        bool manifestReadbackSucceeded = TryReadOsFileFromEspImage(image, "MANIFEST.JSON", out byte[] manifestBytesFromImage);
        byte[] packageBuffer = new byte[16 * 1024 * 1024];
        OsPackageLoadResult packageLoad = OsPackageLoader.LoadFromPrimaryGptEsp(MemoryBlockDevice.FromImage(image, (uint)SectorSize), packageBuffer);
        byte[] packageBytes = packageLoad.Succeeded ? packageBuffer.AsSpan(0, checked((int)packageLoad.BytesRead)).ToArray() : Array.Empty<byte>();
        return new KestrelPublishedImageFixture(publish.Output, imageBuilder.Output, imagePath, packageRoot, manifestBytesFromPublish, manifestBytesFromImage, manifestReadbackSucceeded, packageBytes, packageLoad.Succeeded, packageLoad.Status.ToString(), packageLoad.PackageStatus.ToString());
    }

    private static ToolResult RunKestrelPublish(string outputRoot)
    {
        var result = HostToolProcess.RunTool("kestrel", "package", "--output-root", outputRoot, "--workload-profile", "SingleMinimalApi", "--skip-negative-validation");
        return new(result.ExitCode, result.Output + result.Error);
    }

    private static bool PackageFileEqualsPublishedFile(byte[] packageBytes, string packageRoot, string packagePath)
    {
        string publishedPath = PackageFilePath(packageRoot, packagePath);
        if (!File.Exists(publishedPath))
        {
            Console.WriteLine("Published file missing: " + publishedPath);
            return false;
        }

        OsPackageFileContentResult content = OsPackageParser.GetFileContent(packageBytes, System.Text.Encoding.ASCII.GetBytes(packagePath));
        if (!content.Succeeded)
        {
            Console.WriteLine("Package file missing: " + packagePath + " status=" + content.Status);
            return false;
        }

        byte[] publishedBytes = File.ReadAllBytes(publishedPath);
        if (!content.Content.SequenceEqual(publishedBytes))
        {
            Console.WriteLine("Package file bytes differ: " + packagePath);
            Console.WriteLine("Published bytes=" + publishedBytes.Length + " package bytes=" + content.Content.Length);
            return false;
        }

        return true;
    }

    private static bool PackageFileContainsAscii(byte[] packageBytes, string packagePath, string expected)
    {
        OsPackageFileContentResult content = OsPackageParser.GetFileContent(packageBytes, System.Text.Encoding.ASCII.GetBytes(packagePath));
        return content.Succeeded && ContainsAscii(content.Content, expected);
    }

    private static string PackageFilePath(string packageRoot, string packagePath)
    {
        string filePath = packageRoot;
        foreach (string segment in packagePath.Split('/'))
        {
            filePath = Path.Combine(filePath, segment);
        }

        return filePath;
    }

    private static bool TryReadOsFileFromEspImage(byte[] image, string fileName, out byte[] content)
    {
        content = Array.Empty<byte>();
        try
        {
            int bootSector = checked((int)PartitionStartLba) * SectorSize;
            uint fatSectors = ReadUInt32(image, bootSector + 36);
            ulong fatOffset = (PartitionStartLba + 32) * SectorSize;
            ulong dataOffset = (PartitionStartLba + 32 + 2UL * fatSectors) * SectorSize;
            ReadOnlySpan<byte> root = Cluster(image, dataOffset, 2);
            DirectoryEntry os = FindEntryByName(root, "OS");
            if (!os.IsDirectory)
            {
                return false;
            }

            ReadOnlySpan<byte> osDirectory = Cluster(image, dataOffset, os.FirstCluster);
            DirectoryEntry file = FindEntryByName(osDirectory, fileName);
            if (file.IsDirectory)
            {
                return false;
            }

            content = ReadFile(image, fatOffset, dataOffset, file.FirstCluster, file.FileSize);
            return true;
        }
        catch (Exception ex)when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return false;
        }
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> content, string expected)
    {
        if (expected.Length == 0)
        {
            return true;
        }

        for (int index = 0; index <= content.Length - expected.Length; index++)
        {
            bool matched = true;
            for (int expectedIndex = 0; expectedIndex < expected.Length; expectedIndex++)
            {
                if (content[index + expectedIndex] != (byte)expected[expectedIndex])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OutputHasLine(string output, string expectedLine)
    {
        foreach (string line in output.Split('\n'))
        {
            if (string.Equals(line.TrimEnd('\r'), expectedLine, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TestMissingEfi(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "missing.raw");
        ToolResult result = RunImageBuilder("create-raw", "--efi", Path.Combine(tempRoot, "does-not-exist.efi"), "--output", imagePath);
        return result.ExitCode == 3 && result.Output.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.raw efi_not_found", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestMissingManifest(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-MISSING-MANIFEST.EFI");
        string imagePath = Path.Combine(tempRoot, "missing-manifest.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", Path.Combine(tempRoot, "missing-manifest.json"), "--output", imagePath);
        return result.ExitCode == 8 && result.Output.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.raw manifest_not_found", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestEmptyManifest(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-EMPTY-MANIFEST.EFI");
        string manifestPath = Path.Combine(tempRoot, "empty-manifest.json");
        string imagePath = Path.Combine(tempRoot, "empty-manifest.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--manifest", manifestPath, "--output", imagePath);
        return result.ExitCode == 9 && result.Output.Contains("TEST_FAIL imagebuilder.raw manifest_empty", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestOversizedManifest(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-OVERSIZED-MANIFEST.EFI");
        string manifestPath = Path.Combine(tempRoot, "oversized-manifest.json");
        string imagePath = Path.Combine(tempRoot, "oversized-manifest.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(manifestPath, new byte[64 * 1024 + 1]);
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--manifest", manifestPath, "--output", imagePath);
        return result.ExitCode == 10 && result.Output.Contains("TEST_START imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.plan manifest_too_large", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestMissingPackage(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-MISSING-PACKAGE.EFI");
        string imagePath = Path.Combine(tempRoot, "missing-package.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--package", Path.Combine(tempRoot, "missing-app.pkg"), "--output", imagePath);
        return result.ExitCode == 11 && result.Output.Contains("TEST_START imagebuilder.raw", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.raw package_not_found", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestEmptyPackage(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-EMPTY-PACKAGE.EFI");
        string packagePath = Path.Combine(tempRoot, "empty-app.pkg");
        string imagePath = Path.Combine(tempRoot, "empty-package.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(packagePath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("create-raw", "--efi", efiPath, "--package", packagePath, "--output", imagePath);
        return result.ExitCode == 12 && result.Output.Contains("TEST_FAIL imagebuilder.raw package_empty", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestOversizedPackage(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-OVERSIZED-PACKAGE.EFI");
        string packagePath = Path.Combine(tempRoot, "oversized-app.pkg");
        string imagePath = Path.Combine(tempRoot, "oversized-package.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        File.WriteAllBytes(packagePath, new byte[16 * 1024 * 1024 + 1]);
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--package", packagePath, "--output", imagePath);
        return result.ExitCode == 13 && result.Output.Contains("TEST_START imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.plan package_too_large", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestOversizedRawImageSize(string tempRoot)
    {
        string efiPath = Path.Combine(tempRoot, "BOOTX64-OVERSIZED-IMAGE.EFI");
        string imagePath = Path.Combine(tempRoot, "oversized-image.raw");
        File.WriteAllBytes(efiPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-raw", "--efi", efiPath, "--output", imagePath, "--image-size-mib", "2097154");
        return result.ExitCode == 5 && result.Output.Contains("TEST_START imagebuilder.plan", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.plan image_too_large", StringComparison.Ordinal) && !File.Exists(imagePath);
    }

    private static bool TestConvertQcow2DryRunWithoutQemuImg(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "qcow2-input.raw");
        string outputPath = Path.Combine(tempRoot, "qcow2-output.qcow2");
        string qemuImgPath = Path.Combine(tempRoot, "missing-qemu-img.exe");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("convert-qcow2", "--input", inputPath, "--output", outputPath, "--qemu-img", qemuImgPath, "--dry-run");
        bool hasExpectedMarkers = result.ExitCode == 0 && result.Output.Contains("TEST_START imagebuilder.qcow2", StringComparison.Ordinal) && result.Output.Contains("QEMU_IMG_DISCOVERY source=explicit", StringComparison.Ordinal) && result.Output.Contains("requested=" + qemuImgPath, StringComparison.Ordinal) && result.Output.Contains("resolved=<not-found> available=false", StringComparison.Ordinal) && result.Output.Contains("QCOW2_COMMAND", StringComparison.Ordinal) && result.Output.Contains(" convert -f raw -O qcow2 ", StringComparison.Ordinal) && result.Output.Contains("QCOW2_NO_WRITE " + outputPath, StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.qcow2", StringComparison.Ordinal);
        if (!hasExpectedMarkers || File.Exists(outputPath))
        {
            PrintToolFailure(result);
            Console.WriteLine("qcow2 output exists: " + File.Exists(outputPath));
            return false;
        }

        return true;
    }

    private static bool TestConvertQcow2MissingInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "missing-qcow2-input.raw");
        string outputPath = Path.Combine(tempRoot, "missing-qcow2-output.qcow2");
        ToolResult result = RunImageBuilder("convert-qcow2", "--input", inputPath, "--output", outputPath, "--dry-run");
        return result.ExitCode == 11 && result.Output.Contains("TEST_START imagebuilder.qcow2", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.qcow2 input_not_found", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestConvertQcow2EmptyInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "empty-qcow2-input.raw");
        string outputPath = Path.Combine(tempRoot, "empty-qcow2-output.qcow2");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("convert-qcow2", "--input", inputPath, "--output", outputPath, "--dry-run");
        return result.ExitCode == 12 && result.Output.Contains("TEST_START imagebuilder.qcow2", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.qcow2 input_empty", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestConvertQcow2MissingQemuImgRealMode(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "real-qcow2-input.raw");
        string outputPath = Path.Combine(tempRoot, "real-qcow2-output.qcow2");
        string qemuImgPath = Path.Combine(tempRoot, "missing-real-qemu-img.exe");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("convert-qcow2", "--input", inputPath, "--output", outputPath, "--qemu-img", qemuImgPath);
        return result.ExitCode == 14 && result.Output.Contains("TEST_START imagebuilder.qcow2", StringComparison.Ordinal) && result.Output.Contains("QEMU_IMG_DISCOVERY source=explicit", StringComparison.Ordinal) && result.Output.Contains("resolved=<not-found> available=false", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.qcow2 qemu_img_not_found", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestConvertAzureVhdDryRunAligned(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "azure-input.raw");
        string outputPath = Path.Combine(tempRoot, "azure-output.vhd");
        string qemuImgPath = Path.Combine(tempRoot, "missing-qemu-img.exe");
        File.WriteAllBytes(inputPath, new byte[1024 * 1024]);
        ToolResult result = RunImageBuilder("convert-azure-vhd", "--input", inputPath, "--output", outputPath, "--qemu-img", qemuImgPath, "--dry-run");
        bool hasExpectedMarkers = result.ExitCode == 0 && result.Output.Contains("TEST_START imagebuilder.azure-vhd", StringComparison.Ordinal) && result.Output.Contains("QEMU_IMG_DISCOVERY source=explicit", StringComparison.Ordinal) && result.Output.Contains("AZURE_VHD_COMMAND", StringComparison.Ordinal) && result.Output.Contains(" convert -f raw -o subformat=fixed,force_size -O vpc ", StringComparison.Ordinal) && result.Output.Contains("AZURE_VHD_NO_WRITE " + outputPath, StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.azure-vhd", StringComparison.Ordinal);
        if (!hasExpectedMarkers || File.Exists(outputPath))
        {
            PrintToolFailure(result);
            Console.WriteLine("azure vhd output exists: " + File.Exists(outputPath));
            return false;
        }

        return true;
    }

    private static bool TestConvertAzureVhdRejectsUnalignedInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "azure-unaligned.raw");
        string outputPath = Path.Combine(tempRoot, "azure-unaligned.vhd");
        File.WriteAllBytes(inputPath, new byte[1024 * 1024 + 1]);
        ToolResult result = RunImageBuilder("convert-azure-vhd", "--input", inputPath, "--output", outputPath, "--dry-run");
        return result.ExitCode == 30 && result.Output.Contains("TEST_START imagebuilder.azure-vhd", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.azure-vhd azure_vhd_unaligned_size", StringComparison.Ordinal) && !File.Exists(outputPath);
    }

    private static bool TestPlanAwsAmiDryRun(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "aws-ami-input.raw");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-aws-ami", "--input", inputPath, "--format", "raw", "--architecture", "x86_64", "--virtualization", "hvm", "--root-device", "ebs", "--boot-mode", "uefi", "--ena-support", "true", "--region", "us-east-1", "--bucket", "os-image-import", "--vmimport-role", "vmimport", "--dry-run");
        bool hasExpectedMarkers = result.ExitCode == 0 && result.Output.Contains("TEST_START imagebuilder.aws-ami", StringComparison.Ordinal) && result.Output.Contains("AWS_AMI_PLAN ", StringComparison.Ordinal) && result.Output.Contains("\"Architecture\":\"x86_64\"", StringComparison.Ordinal) && result.Output.Contains("\"VirtualizationType\":\"hvm\"", StringComparison.Ordinal) && result.Output.Contains("\"RootDeviceType\":\"ebs\"", StringComparison.Ordinal) && result.Output.Contains("\"BootMode\":\"uefi\"", StringComparison.Ordinal) && result.Output.Contains("\"EnaSupport\":true", StringComparison.Ordinal) && result.Output.Contains("AWS_AMI_IMPORT_DRY_RUN format=raw architecture=x86_64 virtualization=hvm rootDevice=ebs bootMode=uefi enaSupport=true", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.aws-ami", StringComparison.Ordinal);
        if (!hasExpectedMarkers)
        {
            PrintToolFailure(result);
            return false;
        }

        return true;
    }

    private static bool TestPlanAwsAmiRejectsNonHvm(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "aws-ami-nonhvm.raw");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-aws-ami", "--input", inputPath, "--virtualization", "paravirtual", "--dry-run");
        return result.ExitCode == 32 && result.Output.Contains("TEST_START imagebuilder.aws-ami", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.aws-ami aws_ami_virtualization_not_hvm", StringComparison.Ordinal);
    }

    private static bool TestPlanAwsAmiRejectsArm64(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "aws-ami-arm64.raw");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-aws-ami", "--input", inputPath, "--architecture", "arm64", "--dry-run");
        return result.ExitCode == 31 && result.Output.Contains("TEST_START imagebuilder.aws-ami", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.aws-ami aws_ami_unsupported_architecture", StringComparison.Ordinal);
    }

    private static bool TestPlanAwsAmiRejectsLegacyBootMode(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "aws-ami-legacy.raw");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("plan-aws-ami", "--input", inputPath, "--boot-mode", "legacy-bios", "--dry-run");
        return result.ExitCode == 37 && result.Output.Contains("TEST_START imagebuilder.aws-ami", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.aws-ami aws_ami_boot_mode_mismatch", StringComparison.Ordinal);
    }

    private static bool TestDescribeImageEmitsSizeAndSha256(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "metadata-input.raw");
        byte[] imageBytes = new byte[]
        {
            0x4f,
            0x53,
            0x00,
            0x10,
            0x20,
            0xff
        };
        File.WriteAllBytes(inputPath, imageBytes);
        string expectedSha256 = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        ToolResult result = RunImageBuilder("describe-image", "--input", inputPath, "--format", "raw");
        bool hasExpectedMarkers = result.ExitCode == 0 && result.Output.Contains("TEST_START imagebuilder.image-metadata", StringComparison.Ordinal) && result.Output.Contains("IMAGE_METADATA ", StringComparison.Ordinal) && result.Output.Contains("\"Format\":\"raw\"", StringComparison.Ordinal) && result.Output.Contains("\"Bytes\":" + imageBytes.Length, StringComparison.Ordinal) && result.Output.Contains("\"Sha256\":\"" + expectedSha256 + "\"", StringComparison.Ordinal) && result.Output.Contains("IMAGE_METADATA_PATH " + inputPath, StringComparison.Ordinal) && result.Output.Contains("IMAGE_METADATA_FORMAT raw", StringComparison.Ordinal) && result.Output.Contains("IMAGE_METADATA_BYTES " + imageBytes.Length, StringComparison.Ordinal) && result.Output.Contains("IMAGE_METADATA_SHA256 " + expectedSha256, StringComparison.Ordinal) && result.Output.Contains("TEST_PASS imagebuilder.image-metadata", StringComparison.Ordinal);
        if (!hasExpectedMarkers)
        {
            PrintToolFailure(result);
            return false;
        }

        return true;
    }

    private static bool TestDescribeImageRejectsEmptyInput(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "metadata-empty.raw");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        ToolResult result = RunImageBuilder("describe-image", "--input", inputPath, "--format", "raw");
        return result.ExitCode == 12 && result.Output.Contains("TEST_START imagebuilder.image-metadata", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.image-metadata input_empty", StringComparison.Ordinal);
    }

    private static bool TestDescribeImageRejectsUnsupportedFormat(string tempRoot)
    {
        string inputPath = Path.Combine(tempRoot, "metadata-input.unsupported");
        File.WriteAllBytes(inputPath, CreateEfiBytes());
        ToolResult result = RunImageBuilder("describe-image", "--input", inputPath, "--format", "bad-format");
        return result.ExitCode == 38 && result.Output.Contains("TEST_START imagebuilder.image-metadata", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL imagebuilder.image-metadata image_metadata_unsupported_format", StringComparison.Ordinal);
    }

    private static bool HasProtectiveMbr(byte[] image)
    {
        return image[446 + 4] == 0xEE && ReadUInt32(image, 446 + 8) == 1 && image[510] == 0x55 && image[511] == 0xAA;
    }

    private static bool HasPrimaryAndBackupGpt(byte[] image)
    {
        int primaryHeader = SectorSize;
        int backupHeader = image.Length - SectorSize;
        int firstEntry = SectorSize * 2;
        Guid efiSystemPartitionType = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
        Guid typeGuid = ReadGuid(image, firstEntry);
        string partitionName = ReadUtf16(image, firstEntry + 56, 72);
        return HasAscii(image, primaryHeader, "EFI PART") && HasAscii(image, backupHeader, "EFI PART") && typeGuid == efiSystemPartitionType && ReadUInt64(image, firstEntry + 32) == PartitionStartLba && partitionName == "EFI System";
    }

    private static bool HasFat32EspBootSector(byte[] image)
    {
        int bootSector = checked((int)PartitionStartLba) * SectorSize;
        return image[bootSector + 510] == 0x55 && image[bootSector + 511] == 0xAA && ReadUInt16(image, bootSector + 11) == SectorSize && image[bootSector + 13] == 1 && ReadUInt16(image, bootSector + 14) == 32 && image[bootSector + 16] == 2 && ReadUInt32(image, bootSector + 28) == PartitionStartLba && ReadUInt32(image, bootSector + 44) == 2 && HasAscii(image, bootSector + 82, "FAT32");
    }

    private static bool ContainsBootFile(byte[] image, byte[] expected)
    {
        int bootSector = checked((int)PartitionStartLba) * SectorSize;
        uint fatSectors = ReadUInt32(image, bootSector + 36);
        ulong fatOffset = (PartitionStartLba + 32) * SectorSize;
        ulong dataOffset = (PartitionStartLba + 32 + 2UL * fatSectors) * SectorSize;
        ReadOnlySpan<byte> root = Cluster(image, dataOffset, 2);
        DirectoryEntry efi = FindEntry(root, "EFI        ");
        if (!efi.IsDirectory || efi.FirstCluster != 3)
        {
            return false;
        }

        ReadOnlySpan<byte> efiDirectory = Cluster(image, dataOffset, efi.FirstCluster);
        DirectoryEntry boot = FindEntry(efiDirectory, "BOOT       ");
        if (!boot.IsDirectory || boot.FirstCluster != 4)
        {
            return false;
        }

        ReadOnlySpan<byte> bootDirectory = Cluster(image, dataOffset, boot.FirstCluster);
        DirectoryEntry bootFile = FindEntry(bootDirectory, "BOOTX64 EFI");
        if (bootFile.IsDirectory || bootFile.FileSize != expected.Length || bootFile.FirstCluster != 5)
        {
            return false;
        }

        byte[] actual = ReadFile(image, fatOffset, dataOffset, bootFile.FirstCluster, bootFile.FileSize);
        return actual.SequenceEqual(expected);
    }

    private static bool ContainsManifestFile(byte[] image, byte[] expected)
    {
        int bootSector = checked((int)PartitionStartLba) * SectorSize;
        uint fatSectors = ReadUInt32(image, bootSector + 36);
        ulong fatOffset = (PartitionStartLba + 32) * SectorSize;
        ulong dataOffset = (PartitionStartLba + 32 + 2UL * fatSectors) * SectorSize;
        ReadOnlySpan<byte> root = Cluster(image, dataOffset, 2);
        DirectoryEntry os = FindEntryByName(root, "OS");
        if (!os.IsDirectory)
        {
            return false;
        }

        ReadOnlySpan<byte> osDirectory = Cluster(image, dataOffset, os.FirstCluster);
        DirectoryEntry manifest = FindEntryByName(osDirectory, "MANIFEST.JSON");
        if (manifest.IsDirectory || manifest.FileSize != expected.Length)
        {
            return false;
        }

        byte[] actual = ReadFile(image, fatOffset, dataOffset, manifest.FirstCluster, manifest.FileSize);
        return actual.SequenceEqual(expected);
    }

    private static bool ContainsPackageFile(byte[] image, byte[] expected)
    {
        int bootSector = checked((int)PartitionStartLba) * SectorSize;
        uint fatSectors = ReadUInt32(image, bootSector + 36);
        ulong fatOffset = (PartitionStartLba + 32) * SectorSize;
        ulong dataOffset = (PartitionStartLba + 32 + 2UL * fatSectors) * SectorSize;
        ReadOnlySpan<byte> root = Cluster(image, dataOffset, 2);
        DirectoryEntry os = FindEntryByName(root, "OS");
        if (!os.IsDirectory)
        {
            return false;
        }

        ReadOnlySpan<byte> osDirectory = Cluster(image, dataOffset, os.FirstCluster);
        DirectoryEntry package = FindEntryByName(osDirectory, "APP.PKG");
        if (package.IsDirectory || package.FileSize != expected.Length)
        {
            return false;
        }

        byte[] actual = ReadFile(image, fatOffset, dataOffset, package.FirstCluster, package.FileSize);
        return actual.SequenceEqual(expected);
    }

    private static bool PartitionDescriptorNameEquals(ReadOnlySpan<byte> partitionEntrySectors, GptPartitionDescriptor descriptor, string expectedName)
    {
        if (descriptor.NameUtf16CodeUnitLength != expectedName.Length || descriptor.NameByteOffset < 0 || descriptor.NameByteOffset + descriptor.NameByteLength > partitionEntrySectors.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> nameBytes = partitionEntrySectors.Slice(descriptor.NameByteOffset, descriptor.NameUtf16CodeUnitLength * sizeof(ushort));
        string actualName = System.Text.Encoding.Unicode.GetString(nameBytes);
        return string.Equals(actualName, expectedName, StringComparison.Ordinal);
    }

    private static byte[] ReadFile(byte[] image, ulong fatOffset, ulong dataOffset, int firstCluster, int length)
    {
        byte[] result = new byte[length];
        int written = 0;
        int cluster = firstCluster;
        while (cluster < 0x0FFFFFF8 && written < result.Length)
        {
            ReadOnlySpan<byte> clusterBytes = Cluster(image, dataOffset, cluster);
            int count = Math.Min(clusterBytes.Length, result.Length - written);
            clusterBytes[..count].CopyTo(result.AsSpan(written, count));
            written += count;
            cluster = (int)(ReadUInt32(image, checked((int)fatOffset) + cluster * 4) & 0x0FFFFFFF);
        }

        return result;
    }

    private static DirectoryEntry FindEntry(ReadOnlySpan<byte> directory, string shortName)
    {
        for (int offset = 0; offset < directory.Length; offset += 32)
        {
            if (directory[offset] == 0)
            {
                break;
            }

            if (MatchesShortName(directory.Slice(offset, 11), shortName))
            {
                byte attributes = directory[offset + 11];
                int high = ReadUInt16(directory, offset + 20);
                int low = ReadUInt16(directory, offset + 26);
                int firstCluster = (high << 16) | low;
                int fileSize = (int)ReadUInt32(directory, offset + 28);
                return new DirectoryEntry((attributes & 0x10) != 0, firstCluster, fileSize);
            }
        }

        throw new InvalidOperationException("Missing FAT directory entry: " + shortName);
    }

    private static DirectoryEntry FindEntryByName(ReadOnlySpan<byte> directory, string expectedName)
    {
        string? pendingLongName = null;
        for (int offset = 0; offset < directory.Length; offset += 32)
        {
            if (directory[offset] == 0)
            {
                break;
            }

            byte attributes = directory[offset + 11];
            if ((attributes & 0x0F) == 0x0F)
            {
                string part = ReadLongFileNamePart(directory.Slice(offset, 32));
                pendingLongName = part + pendingLongName;
                continue;
            }

            string actualName = pendingLongName ?? ReadShortName(directory.Slice(offset, 11));
            pendingLongName = null;
            if (string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                int high = ReadUInt16(directory, offset + 20);
                int low = ReadUInt16(directory, offset + 26);
                int firstCluster = (high << 16) | low;
                int fileSize = (int)ReadUInt32(directory, offset + 28);
                return new DirectoryEntry((attributes & 0x10) != 0, firstCluster, fileSize);
            }
        }

        throw new InvalidOperationException("Missing FAT directory entry: " + expectedName);
    }

    private static string ReadLongFileNamePart(ReadOnlySpan<byte> entry)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(13);
        AppendLongFileNameChars(entry, 1, 5, builder);
        AppendLongFileNameChars(entry, 14, 6, builder);
        AppendLongFileNameChars(entry, 28, 2, builder);
        return builder.ToString();
    }

    private static void AppendLongFileNameChars(ReadOnlySpan<byte> entry, int offset, int count, System.Text.StringBuilder builder)
    {
        for (int i = 0; i < count; i++)
        {
            ushort value = ReadUInt16(entry, offset + i * 2);
            if (value == 0 || value == 0xFFFF)
            {
                return;
            }

            builder.Append((char)value);
        }
    }

    private static string ReadShortName(ReadOnlySpan<byte> shortName)
    {
        string name = ReadShortNamePart(shortName[..8]);
        string extension = ReadShortNamePart(shortName.Slice(8, 3));
        return extension.Length == 0 ? name : name + "." + extension;
    }

    private static string ReadShortNamePart(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.Length;
        while (length > 0 && bytes[length - 1] == 0x20)
        {
            length--;
        }

        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)bytes[i];
        }

        return new string (chars);
    }

    private static bool MatchesShortName(ReadOnlySpan<byte> actual, string expected)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != (byte)expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool FieldEquals(byte[] source, NativeManifestField field, string expected)
    {
        if (!field.TryGetSlice(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SpanEqualsAscii(ReadOnlySpan<byte> value, string expected)
    {
        if (value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static ReadOnlySpan<byte> Cluster(byte[] image, ulong dataOffset, int cluster)
    {
        int offset = checked((int)(dataOffset + (ulong)(cluster - 2) * SectorSize));
        return image.AsSpan(offset, SectorSize);
    }

    private static ToolResult RunImageBuilder(params string[] args)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = GetBuiltImageBuilderPath(repositoryRoot);
        if (!File.Exists(toolPath))
        {
            return new ToolResult(-1, "image builder tool not found: " + toolPath);
        }

        using Process process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.WorkingDirectory = repositoryRoot;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.ArgumentList.Add(toolPath);
        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            // Timeout is the actionable failure.
            }

            return new ToolResult(-1, "image builder timed out");
        }

        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        return new ToolResult(process.ExitCode, output);
    }

    private static string GetBuiltImageBuilderPath(string repositoryRoot)
    {
        string configuration = GetCurrentBuildConfiguration();
        string targetFramework = GetCurrentTargetFramework();
        return Path.Combine(repositoryRoot, "tools", "OS.ImageBuilder", "bin", configuration, targetFramework, "OS.ImageBuilder.dll");
    }

    private static string GetCurrentBuildConfiguration()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.Parent?.Parent?.Name == "bin")
            {
                return directory.Parent.Name;
            }

            directory = directory.Parent;
        }

        return "Debug";
    }

    private static string GetCurrentTargetFramework()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        return string.IsNullOrWhiteSpace(directory.Name) ? "net11.0" : directory.Name;
    }

    private static byte[] CreateEfiBytes()
    {
        byte[] bytes = new byte[1537];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 251);
        }

        return bytes;
    }

    private static byte[] CreateManifestBytes()
    {
        return CreateManifestBytesForAppPath("apps/hello.bin");
    }

    private static byte[] CreateManifestBytesForAppPath(string appPath)
    {
        return System.Text.Encoding.ASCII.GetBytes("format=os-native-manifest\n" + "version=0\n" + "app.name=manifest.test\n" + "app.path=" + appPath + "\n" + "args=--from-image-builder-test\n" + "network=dhcp\n" + "log=serial\n");
    }

    private static byte[] CreateKestrelManifestBytes()
    {
        return System.Text.Encoding.ASCII.GetBytes("""
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Production",
    "OS_SMOKE_EXPECTED_BODY": "OS_MINIMAL_API_OK"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/minimal-api/MinimalApiSmoke.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
""");
    }

    private static byte[] CreateKestrelManifestWithoutExpectedBodyBytes()
    {
        return System.Text.Encoding.ASCII.GetBytes("""
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Production"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/minimal-api/MinimalApiSmoke.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
""");
    }

    private static string CreateKestrelPackageRoot(string tempRoot, string name)
    {
        string packageRoot = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(packageRoot);
        WriteKestrelPackageFile(packageRoot, "apps/minimal-api/MinimalApiSmoke.dll");
        WriteKestrelPackageFile(packageRoot, "apps/minimal-api/MinimalApiSmoke.deps.json");
        WriteKestrelPackageFile(packageRoot, "apps/minimal-api/MinimalApiSmoke.runtimeconfig.json");
        WriteKestrelPackageFile(packageRoot, "apps/minimal-api/appsettings.json");
        return packageRoot;
    }

    private static void WriteKestrelPackageFile(string packageRoot, string packagePath)
    {
        string filePath = packageRoot;
        foreach (string segment in packagePath.Split('/'))
        {
            filePath = Path.Combine(filePath, segment);
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, "fixture:" + packagePath);
    }

    private static byte[] CreatePackageBytes()
    {
        byte[] bytes = new byte[2057];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 17 + 23) % 251);
        }

        return bytes;
    }

    private static byte[] CreateValidPackageBytes()
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        const int pathOffset = tableOffset + tableLength;
        const string path = "bin/app.dll";
        const int contentOffset = pathOffset + 11;
        const int contentLength = 4;
        byte[] package = new byte[contentOffset + contentLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, 1);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        WriteUInt32(package, tableOffset, pathOffset);
        WriteUInt16(package, tableOffset + 4, path.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, (ulong)contentOffset);
        WriteUInt64(package, tableOffset + 16, contentLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, path);
        package[contentOffset] = 0xCA;
        package[contentOffset + 1] = 0xFE;
        package[contentOffset + 2] = 0xBA;
        package[contentOffset + 3] = 0xBE;
        return package;
    }

    private static byte[] CreateNativePayloadPackageBytes(string appPath, bool validPayloadHeader)
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        const int payloadLength = 40;
        int pathOffset = tableOffset + tableLength;
        int contentOffset = pathOffset + appPath.Length;
        byte[] package = new byte[contentOffset + payloadLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, 1);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        WriteUInt32(package, tableOffset, pathOffset);
        WriteUInt16(package, tableOffset + 4, appPath.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, (ulong)contentOffset);
        WriteUInt64(package, tableOffset + 16, payloadLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, appPath);
        package[contentOffset] = validPayloadHeader ? (byte)'O' : (byte)'X';
        package[contentOffset + 1] = (byte)'S';
        package[contentOffset + 2] = (byte)'A';
        package[contentOffset + 3] = (byte)'P';
        package[contentOffset + 4] = (byte)'P';
        package[contentOffset + 5] = (byte)'v';
        package[contentOffset + 6] = (byte)'0';
        package[contentOffset + 7] = 0;
        WriteUInt16(package, contentOffset + 8, NativeAppPayloadHeader.SupportedVersion);
        WriteUInt16(package, contentOffset + 10, NativeAppPayloadHeader.HeaderSize);
        WriteUInt32(package, contentOffset + 12, 0);
        WriteUInt64(package, contentOffset + 16, NativeAppPayloadHeader.HeaderSize);
        WriteUInt64(package, contentOffset + 24, 0);
        package[contentOffset + NativeAppPayloadHeader.HeaderSize] = 0xC3;
        return package;
    }

    private static void WriteAscii(byte[] bytes, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            bytes[offset + i] = (byte)value[i];
        }
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)value);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
    }

    private static void PrintToolFailure(ToolResult result)
    {
        Console.WriteLine("Image builder exit=" + result.ExitCode);
        Console.WriteLine("Image builder output=" + result.Output.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
    }

    private static bool HasAscii(byte[] bytes, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (bytes[offset + i] != (byte)value[i])
            {
                return false;
            }
        }

        return true;
    }

    private static Guid ReadGuid(byte[] bytes, int offset)
    {
        byte[] guidBytes = new byte[16];
        Array.Copy(bytes, offset, guidBytes, 0, guidBytes.Length);
        return new Guid(guidBytes);
    }

    private static string ReadUtf16(byte[] bytes, int offset, int length)
    {
        string value = System.Text.Encoding.Unicode.GetString(bytes, offset, length);
        int terminator = value.IndexOf('\0', StringComparison.Ordinal);
        return terminator >= 0 ? value[..terminator] : value;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static ulong ReadUInt64(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
    }
}
