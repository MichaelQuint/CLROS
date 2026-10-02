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

ImageBuilderOptions options;
try
{
    options = ImageBuilderOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("TEST_FAIL " + ImageBuilderOptions.GetMarkerName(args) + " invalid_args " + ex.Message);
    return 2;
}

if (options.ShowHelp)
{
    ImageBuilderOptions.PrintHelp();
    return 0;
}

string markerName = ImageBuilderOptions.GetMarkerName(options.Command);
Console.WriteLine("TEST_START " + markerName);
Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);

try
{
    if (options.Command == ImageBuilderCommand.PlanRaw)
    {
        RawImageLayout layout = RawEspImageBuilder.Plan(options);
        PrintKestrelPackageEvidence(options);
        ImagePlanPrinter.Print(layout, Console.Out, options.WorkloadProfile);
        Console.WriteLine("IMAGE_PLAN_NO_WRITE " + options.OutputPath);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ConvertQcow2)
    {
        Qcow2Converter.Convert(options, Console.Out, Console.Error);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ConvertAzureVhd)
    {
        AzureVhdConverter.Convert(options, Console.Out, Console.Error);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.PlanAwsAmi)
    {
        AwsAmiImportPlanner.Plan(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.DescribeImage)
    {
        ImageArtifactMetadataWriter.Describe(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildPackage)
    {
        OsPackageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativePayload)
    {
        NativePayloadBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativeAppPackage)
    {
        NativeAppPackageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativeAppImage)
    {
        NativeAppImageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ValidatePackage)
    {
        PackagePayloadValidator.Validate(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    RawEspImageBuilder.Build(options);
    PrintKestrelPackageEvidence(options);
    Console.WriteLine("IMAGE_ARTIFACT " + options.OutputPath);
    Console.WriteLine("TEST_PASS " + markerName);
    return 0;
}
catch (ImageBuilderException ex)
{
    Console.Error.WriteLine("TEST_FAIL " + markerName + " " + ex.Reason);
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}

static void PrintKestrelPackageEvidence(ImageBuilderOptions options)
{
    if (options.KestrelPackageRootPath is null)
    {
        return;
    }

    Console.WriteLine("KESTREL_PACKAGE_CONTRACT " + KestrelSmokePackageContractValidator.ContractId);
    Console.WriteLine("KESTREL_PACKAGE_ROOT " + options.KestrelPackageRootPath);
}

internal enum ImageBuilderCommand
{
    CreateRaw,
    PlanRaw,
    ConvertQcow2,
    ConvertAzureVhd,
    PlanAwsAmi,
    DescribeImage,
    BuildPackage,
    BuildNativePayload,
    BuildNativeAppPackage,
    BuildNativeAppImage,
    ValidatePackage
}

internal sealed class ImageBuilderOptions
{
    public const int DefaultImageSizeMiB = 64;
    public const int ManifestMaxBytes = 64 * 1024;
    public const int PackageMaxBytes = 16 * 1024 * 1024;

    public ImageBuilderCommand Command { get; private set; } = ImageBuilderCommand.CreateRaw;

    public string EfiPath { get; private set; } = Path.Combine("artifacts", "efi", "BOOTX64.EFI");

    public string OutputPath { get; private set; } = Path.Combine("artifacts", "images", "os.raw");

    public string? ManifestPath { get; private set; }

    public string? PackagePath { get; private set; }

    public string? KestrelPackageRootPath { get; private set; }

    public string InputPath { get; private set; } = Path.Combine("artifacts", "images", "os.raw");

    public string? QemuImgPath { get; private set; }

    public string ImageFormat { get; private set; } = "raw";

    public string Architecture { get; private set; } = "x86_64";

    public string VirtualizationType { get; private set; } = "hvm";

    public string RootDeviceType { get; private set; } = "ebs";

    public string BootMode { get; private set; } = "uefi";

    public string? Region { get; private set; }

    public string? BucketName { get; private set; }

    public string? VmImportRole { get; private set; }

    public bool EnaSupport { get; private set; } = true;

    public int ImageSizeMiB { get; private set; } = DefaultImageSizeMiB;

    public bool DryRun { get; private set; }

    public bool ShowHelp { get; private set; }

    public string WorkloadProfile { get; private set; } = OS.Kernel.Build.WorkloadProfile.SelectedName;

    public static ImageBuilderOptions Parse(string[] args)
    {
        ImageBuilderOptions options = new ImageBuilderOptions();
        int index = 0;

        if (args.Length > 0)
        {
            if (string.Equals(args[0], "create-raw", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.CreateRaw;
                index = 1;
            }
            else if (string.Equals(args[0], "plan-raw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "plan", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.PlanRaw;
                index = 1;
            }
            else if (string.Equals(args[0], "convert-qcow2", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.ConvertQcow2;
                options.OutputPath = Path.Combine("artifacts", "images", "os.qcow2");
                index = 1;
            }
            else if (string.Equals(args[0], "convert-azure-vhd", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.ConvertAzureVhd;
                options.OutputPath = Path.Combine("artifacts", "images", "os.vhd");
                index = 1;
            }
            else if (string.Equals(args[0], "plan-aws-ami", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.PlanAwsAmi;
                index = 1;
            }
            else if (string.Equals(args[0], "describe-image", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.DescribeImage;
                index = 1;
            }
            else if (string.Equals(args[0], "build-package", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.BuildPackage;
                options.InputPath = string.Empty;
                options.OutputPath = Path.Combine("artifacts", "packages", "app.pkg");
                index = 1;
            }
            else if (string.Equals(args[0], "build-native-payload", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.BuildNativePayload;
                options.InputPath = string.Empty;
                options.OutputPath = Path.Combine("artifacts", "payloads", "app.bin");
                index = 1;
            }
            else if (string.Equals(args[0], "build-native-app-package", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.BuildNativeAppPackage;
                options.InputPath = string.Empty;
                options.OutputPath = Path.Combine("artifacts", "packages", "app.pkg");
                index = 1;
            }
            else if (string.Equals(args[0], "build-native-app-image", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.BuildNativeAppImage;
                options.InputPath = string.Empty;
                options.OutputPath = Path.Combine("artifacts", "images", "os.raw");
                index = 1;
            }
            else if (string.Equals(args[0], "validate-package", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.ValidatePackage;
                index = 1;
            }
            else if (!args[0].StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unknown command: " + args[0]);
            }
        }

        while (index < args.Length)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    index++;
                    break;
                case "--efi":
                    options.EfiPath = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                case "--input":
                    options.InputPath = ReadValue(args, ref index, arg);
                    break;
                case "--qemu-img":
                    options.QemuImgPath = ReadValue(args, ref index, arg);
                    break;
                case "--format":
                    options.ImageFormat = ReadValue(args, ref index, arg);
                    break;
                case "--architecture":
                    options.Architecture = ReadValue(args, ref index, arg);
                    break;
                case "--virtualization":
                    options.VirtualizationType = ReadValue(args, ref index, arg);
                    break;
                case "--root-device":
                    options.RootDeviceType = ReadValue(args, ref index, arg);
                    break;
                case "--boot-mode":
                    options.BootMode = ReadValue(args, ref index, arg);
                    break;
                case "--workload-profile":
                    options.WorkloadProfile = NormalizeWorkloadProfile(ReadValue(args, ref index, arg));
                    break;
                case "--region":
                    options.Region = ReadValue(args, ref index, arg);
                    break;
                case "--bucket":
                    options.BucketName = ReadValue(args, ref index, arg);
                    break;
                case "--vmimport-role":
                    options.VmImportRole = ReadValue(args, ref index, arg);
                    break;
                case "--ena-support":
                    options.EnaSupport = ParseBool(ReadValue(args, ref index, arg), arg);
                    break;
                case "--manifest":
                    options.ManifestPath = ReadValue(args, ref index, arg);
                    break;
                case "--package":
                    options.PackagePath = ReadValue(args, ref index, arg);
                    break;
                case "--kestrel-package":
                    options.KestrelPackageRootPath = ReadValue(args, ref index, arg);
                    break;
                case "--dry-run":
                    if (options.Command == ImageBuilderCommand.ConvertQcow2
                        || options.Command == ImageBuilderCommand.ConvertAzureVhd
                        || options.Command == ImageBuilderCommand.PlanAwsAmi)
                    {
                        options.DryRun = true;
                    }
                    else
                    {
                        options.Command = ImageBuilderCommand.PlanRaw;
                    }

                    index++;
                    break;
                case "--image-size-mib":
                    string value = ReadValue(args, ref index, arg);
                    if (!int.TryParse(value, out int sizeMiB) || sizeMiB < 64)
                    {
                        throw new ArgumentException("--image-size-mib must be an integer >= 64.");
                    }

                    options.ImageSizeMiB = sizeMiB;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        ValidatePackageInputShape(options);
        return options;
    }

    private static void ValidatePackageInputShape(ImageBuilderOptions options)
    {
        if (options.KestrelPackageRootPath is null)
        {
            return;
        }

        if (options.Command != ImageBuilderCommand.CreateRaw
            && options.Command != ImageBuilderCommand.PlanRaw)
        {
            throw new ArgumentException("--kestrel-package is supported only by create-raw and plan-raw.");
        }

        if (options.PackagePath is not null)
        {
            throw new ArgumentException("--kestrel-package cannot be combined with --package.");
        }

        if (options.ManifestPath is null)
        {
            throw new ArgumentException("--kestrel-package requires --manifest so the package contract can be validated.");
        }
    }

    public static string GetMarkerName(string[] args)
    {
        if (args.Length == 0)
        {
            return GetMarkerName(ImageBuilderCommand.CreateRaw);
        }

        if (string.Equals(args[0], "plan-raw", StringComparison.OrdinalIgnoreCase)
            || string.Equals(args[0], "plan", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.PlanRaw);
        }

        if (string.Equals(args[0], "convert-qcow2", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.ConvertQcow2);
        }

        if (string.Equals(args[0], "convert-azure-vhd", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.ConvertAzureVhd);
        }

        if (string.Equals(args[0], "plan-aws-ami", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.PlanAwsAmi);
        }

        if (string.Equals(args[0], "describe-image", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.DescribeImage);
        }

        if (string.Equals(args[0], "build-package", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.BuildPackage);
        }

        if (string.Equals(args[0], "build-native-payload", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.BuildNativePayload);
        }

        if (string.Equals(args[0], "build-native-app-package", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.BuildNativeAppPackage);
        }

        if (string.Equals(args[0], "build-native-app-image", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.BuildNativeAppImage);
        }

        if (string.Equals(args[0], "validate-package", StringComparison.OrdinalIgnoreCase))
        {
            return GetMarkerName(ImageBuilderCommand.ValidatePackage);
        }

        return GetMarkerName(ImageBuilderCommand.CreateRaw);
    }

    public static string GetMarkerName(ImageBuilderCommand command)
    {
        return command switch
        {
            ImageBuilderCommand.PlanRaw => "imagebuilder.plan",
            ImageBuilderCommand.ConvertQcow2 => "imagebuilder.qcow2",
            ImageBuilderCommand.ConvertAzureVhd => "imagebuilder.azure-vhd",
            ImageBuilderCommand.PlanAwsAmi => "imagebuilder.aws-ami",
            ImageBuilderCommand.DescribeImage => "imagebuilder.image-metadata",
            ImageBuilderCommand.BuildPackage => "imagebuilder.package",
            ImageBuilderCommand.BuildNativePayload => "imagebuilder.native-payload",
            ImageBuilderCommand.BuildNativeAppPackage => "imagebuilder.native-app-package",
            ImageBuilderCommand.BuildNativeAppImage => "imagebuilder.native-app-image",
            ImageBuilderCommand.ValidatePackage => "imagebuilder.package-validation",
            _ => "imagebuilder.raw"
        };
    }

    public static void PrintHelp()
    {
        Console.WriteLine("OS.ImageBuilder create-raw [--dry-run] [--efi artifacts/efi/BOOTX64.EFI] [--manifest manifest.json] [--package app.pkg|--kestrel-package package-root] [--output artifacts/images/os.raw] [--image-size-mib 64]");
        Console.WriteLine("OS.ImageBuilder plan-raw [--efi artifacts/efi/BOOTX64.EFI] [--manifest manifest.json] [--package app.pkg|--kestrel-package package-root] [--output artifacts/images/os.raw] [--image-size-mib 64] [--workload-profile SingleMinimalApi|MultiMinimalApi]");
        Console.WriteLine("OS.ImageBuilder build-package --input app-directory --output artifacts/packages/app.pkg");
        Console.WriteLine("OS.ImageBuilder build-native-payload --input raw-app.bin --output artifacts/payloads/app.bin");
        Console.WriteLine("OS.ImageBuilder build-native-app-package --manifest native-manifest --input raw-app.bin --output artifacts/packages/app.pkg");
        Console.WriteLine("OS.ImageBuilder build-native-app-image --efi artifacts/efi/BOOTX64.EFI --manifest native-manifest --input raw-app.bin --output artifacts/images/os.raw");
        Console.WriteLine("OS.ImageBuilder validate-package --manifest native-manifest --package artifacts/packages/app.pkg");
        Console.WriteLine("OS.ImageBuilder convert-qcow2 --input artifacts/images/os.raw --output artifacts/images/os.qcow2 [--qemu-img qemu-img] [--dry-run]");
        Console.WriteLine("OS.ImageBuilder convert-azure-vhd --input artifacts/images/os.raw --output artifacts/images/os.vhd [--qemu-img qemu-img] [--dry-run]");
        Console.WriteLine("OS.ImageBuilder plan-aws-ami --input artifacts/images/os.raw [--format raw] [--architecture x86_64] [--virtualization hvm] [--root-device ebs] [--boot-mode uefi] [--ena-support true] [--region us-east-1] [--bucket bucket] [--vmimport-role vmimport] [--dry-run]");
        Console.WriteLine("OS.ImageBuilder describe-image --input artifacts/images/os.raw [--format raw|qcow2|vhd|vhdx|vmdk|ova|iso]");
    }

    public ImageBuilderOptions CreateNativeAppPackageOptions(string packageOutputPath)
    {
        return new ImageBuilderOptions
        {
            Command = ImageBuilderCommand.BuildNativeAppPackage,
            EfiPath = EfiPath,
            OutputPath = packageOutputPath,
            ManifestPath = ManifestPath,
            PackagePath = PackagePath,
            InputPath = InputPath,
            QemuImgPath = QemuImgPath,
            ImageFormat = ImageFormat,
            Architecture = Architecture,
            VirtualizationType = VirtualizationType,
            RootDeviceType = RootDeviceType,
            BootMode = BootMode,
            Region = Region,
            BucketName = BucketName,
            VmImportRole = VmImportRole,
            EnaSupport = EnaSupport,
            ImageSizeMiB = ImageSizeMiB,
            DryRun = DryRun,
            ShowHelp = ShowHelp,
            WorkloadProfile = WorkloadProfile
        };
    }

    public ImageBuilderOptions CreateRawImageOptions(string? packagePath)
    {
        return new ImageBuilderOptions
        {
            Command = ImageBuilderCommand.CreateRaw,
            EfiPath = EfiPath,
            OutputPath = OutputPath,
            ManifestPath = ManifestPath,
            PackagePath = packagePath,
            InputPath = InputPath,
            QemuImgPath = QemuImgPath,
            ImageFormat = ImageFormat,
            Architecture = Architecture,
            VirtualizationType = VirtualizationType,
            RootDeviceType = RootDeviceType,
            BootMode = BootMode,
            Region = Region,
            BucketName = BucketName,
            VmImportRole = VmImportRole,
            EnaSupport = EnaSupport,
            ImageSizeMiB = ImageSizeMiB,
            DryRun = DryRun,
            ShowHelp = ShowHelp,
            WorkloadProfile = WorkloadProfile
        };
    }

    private static string NormalizeWorkloadProfile(string value)
    {
        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
        }

        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName;
        }

        throw new ArgumentException("Invalid workload profile '" + value + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for " + option);
        }

        index += 2;
        return args[index - 1];
    }

    private static bool ParseBool(string value, string option)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.Ordinal))
        {
            return false;
        }

        throw new ArgumentException(option + " must be true or false.");
    }
}

internal static class PackagePayloadValidator
{
    public static void Validate(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo manifestFile = ValidateManifestPath(options.ManifestPath);
        FileInfo packageFile = ValidatePackagePath(options.PackagePath);
        byte[] manifestBytes = File.ReadAllBytes(manifestFile.FullName);
        byte[] packageBytes = File.ReadAllBytes(packageFile.FullName);
        PackagePayloadValidation validation = ValidateBytes(manifestBytes, packageBytes);

        WriteValidationOutput(validation, output);
    }

    public static PackagePayloadValidation ValidateBytes(byte[] manifestBytes, byte[] packageBytes)
    {
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        if (!manifest.Succeeded)
        {
            throw new ImageBuilderException(
                23,
                "manifest_malformed",
                "Native manifest parse failed: status=" + manifest.Status + " offset=" + manifest.ErrorOffset + ".");
        }

        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(
            manifestBytes,
            manifest.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        if (!launchPlan.Succeeded)
        {
            throw new ImageBuilderException(
                24,
                "manifest_launch_plan_invalid",
                "Native manifest launch plan failed: status=" + launchPlan.Status + ".");
        }

        OsPackageParseResult package = OsPackageParser.Parse(packageBytes);
        if (!package.Succeeded)
        {
            throw new ImageBuilderException(
                25,
                "package_malformed",
                "Package parse failed: status=" + package.Status + ".");
        }

        NativeAppLaunchDescriptorResult descriptor =
            NativeAppLaunchDescriptorBuilder.Create(manifestBytes, packageBytes, launchPlan);
        if (!descriptor.Succeeded)
        {
            ThrowDescriptorFailure(descriptor);
        }

        if (!descriptor.Descriptor.Plan.TryGetAppPath(manifestBytes, out ReadOnlySpan<byte> appPath))
        {
            throw new ImageBuilderException(
                24,
                "manifest_launch_plan_invalid",
                "Native manifest launch plan did not expose app.path.");
        }

        return new PackagePayloadValidation(
            Encoding.ASCII.GetString(appPath),
            descriptor.Descriptor.AppBinaryLength,
            descriptor.Descriptor.PayloadHeader.EntryOffset);
    }

    private static void WriteValidationOutput(PackagePayloadValidation validation, TextWriter output)
    {
        output.WriteLine("PACKAGE_APP_PATH " + validation.AppPath);
        output.WriteLine("PACKAGE_APP_BYTES " + validation.AppBytes);
        output.WriteLine("PACKAGE_APP_ENTRY_OFFSET " + validation.EntryOffset);
    }

    public static FileInfo ValidateManifestPath(string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ImageBuilderException(8, "manifest_not_found", "Manifest path is required.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(manifestPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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

    private static FileInfo ValidatePackagePath(string? packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ImageBuilderException(11, "package_not_found", "Package path is required.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(packagePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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

    private static void ThrowDescriptorFailure(NativeAppLaunchDescriptorResult descriptor)
    {
        string message =
            "Package app validation failed: status=" + descriptor.Status
            + " launch=" + descriptor.LaunchStatus
            + " packageFile=" + descriptor.PackageFileStatus
            + " package=" + descriptor.PackageStatus
            + " payload=" + descriptor.PayloadHeaderStatus + ".";

        if (descriptor.Status == NativeAppLaunchDescriptorStatus.PackageFileMissing)
        {
            throw new ImageBuilderException(26, "package_app_file_missing", message);
        }

        if (descriptor.Status == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader)
        {
            throw new ImageBuilderException(27, "package_app_payload_invalid", message);
        }

        if (descriptor.Status == NativeAppLaunchDescriptorStatus.PackageFileInvalidPath)
        {
            throw new ImageBuilderException(28, "package_app_file_invalid_path", message);
        }

        throw new ImageBuilderException(29, "package_app_validation_failed", message);
    }
}

internal readonly struct PackagePayloadValidation
{
    public PackagePayloadValidation(string appPath, ulong appBytes, ulong entryOffset)
    {
        AppPath = appPath;
        AppBytes = appBytes;
        EntryOffset = entryOffset;
    }

    public string AppPath { get; }

    public ulong AppBytes { get; }

    public ulong EntryOffset { get; }
}

internal static class NativeAppPackageBuilder
{
    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo manifestFile = PackagePayloadValidator.ValidateManifestPath(options.ManifestPath);
        FileInfo appInput = NativePayloadBuilder.ValidateInputPath(options.InputPath);
        string outputPath = NativePayloadBuilder.ValidateOutputPath(options.OutputPath);

        byte[] manifestBytes = File.ReadAllBytes(manifestFile.FullName);
        string appPath = ReadValidatedAppPath(manifestBytes);
        byte[] appBytes = File.ReadAllBytes(appInput.FullName);
        byte[] payloadBytes = NativePayloadBuilder.CreatePayload(appBytes);
        byte[] packageBytes = OsPackageBuilder.CreateSingleFilePackage(appPath, payloadBytes);
        PackagePayloadValidation validation = PackagePayloadValidator.ValidateBytes(manifestBytes, packageBytes);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(outputPath, packageBytes);
        output.WriteLine("NATIVE_APP_PACKAGE_PATH " + validation.AppPath);
        output.WriteLine("NATIVE_APP_PACKAGE_RAW_APP_BYTES " + appBytes.Length);
        output.WriteLine("NATIVE_APP_PACKAGE_PAYLOAD_BYTES " + validation.AppBytes);
        output.WriteLine("NATIVE_APP_PACKAGE_ENTRY_OFFSET " + validation.EntryOffset);
        output.WriteLine("NATIVE_APP_PACKAGE_BYTES " + packageBytes.Length);
        output.WriteLine("NATIVE_APP_PACKAGE_ARTIFACT " + options.OutputPath);
    }

    private static string ReadValidatedAppPath(byte[] manifestBytes)
    {
        NativeManifestParseResult manifest = NativeManifestParser.Parse(manifestBytes);
        if (!manifest.Succeeded)
        {
            throw new ImageBuilderException(
                23,
                "manifest_malformed",
                "Native manifest parse failed: status=" + manifest.Status + " offset=" + manifest.ErrorOffset + ".");
        }

        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(
            manifestBytes,
            manifest.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        if (!launchPlan.Succeeded)
        {
            throw new ImageBuilderException(
                24,
                "manifest_launch_plan_invalid",
                "Native manifest launch plan failed: status=" + launchPlan.Status + ".");
        }

        if (!launchPlan.Plan.TryGetAppPath(manifestBytes, out ReadOnlySpan<byte> appPath)
            || appPath.Length == 0)
        {
            throw new ImageBuilderException(
                24,
                "manifest_launch_plan_invalid",
                "Native manifest launch plan did not expose app.path.");
        }

        return Encoding.ASCII.GetString(appPath);
    }
}

internal static class NativeAppImageBuilder
{
    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        ImageBuilderOptions rawPreflightOptions = options.CreateRawImageOptions(packagePath: null);
        RawEspImageBuilder.Plan(rawPreflightOptions);

        string rawOutputPath = NativePayloadBuilder.ValidateOutputPath(options.OutputPath);
        string packageOutputPath = ResolvePackageOutputPath(options.PackagePath, rawOutputPath);
        ImageBuilderOptions packageOptions = options.CreateNativeAppPackageOptions(packageOutputPath);
        NativeAppPackageBuilder.Build(packageOptions, TextWriter.Null);

        ImageBuilderOptions rawOptions = options.CreateRawImageOptions(packageOutputPath);
        RawEspImageBuilder.Build(rawOptions);

        output.WriteLine("NATIVE_APP_IMAGE_PACKAGE_PATH " + packageOutputPath);
        output.WriteLine("NATIVE_APP_IMAGE_RAW_PATH " + options.OutputPath);
    }

    private static string ResolvePackageOutputPath(string? packagePath, string rawOutputPath)
    {
        string candidate = string.IsNullOrWhiteSpace(packagePath)
            ? rawOutputPath + ".app.pkg"
            : packagePath;
        string outputPath = NativePayloadBuilder.ValidateOutputPath(candidate);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(outputPath, rawOutputPath, comparison))
        {
            throw new ImageBuilderException(
                7,
                "invalid_output_path",
                "Native app image package output must not match raw image output.");
        }

        return outputPath;
    }
}

internal static class NativePayloadBuilder
{
    private const int HeaderSize = 32;

    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo input = ValidateInputPath(options.InputPath);
        string outputPath = ValidateOutputPath(options.OutputPath);
        byte[] inputBytes = File.ReadAllBytes(input.FullName);
        byte[] payload = CreatePayload(inputBytes);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(outputPath, payload);
        output.WriteLine("NATIVE_PAYLOAD_BYTES " + payload.Length);
        output.WriteLine("NATIVE_PAYLOAD_ENTRY_OFFSET " + HeaderSize);
        output.WriteLine("NATIVE_PAYLOAD_ARTIFACT " + options.OutputPath);
    }

    public static byte[] CreatePayload(byte[] inputBytes)
    {
        byte[] payload = new byte[checked(HeaderSize + inputBytes.Length)];

        WriteAscii(payload, 0, "OSAPPv0");
        payload[7] = 0;
        WriteUInt16(payload, 8, 0);
        WriteUInt16(payload, 10, HeaderSize);
        WriteUInt32(payload, 12, 0);
        WriteUInt64(payload, 16, HeaderSize);
        WriteUInt64(payload, 24, 0);
        inputBytes.CopyTo(payload.AsSpan(HeaderSize));
        return payload;
    }

    public static FileInfo ValidateInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input path is required.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input path is invalid: " + inputPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input was not found: " + inputPath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(22, "native_payload_input_empty", "Native payload input is empty: " + inputPath);
        }

        return file;
    }

    public static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            return Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
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

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
    }
}

internal static class Qcow2Converter
{
    public static void Convert(ImageBuilderOptions options, TextWriter output, TextWriter error)
    {
        FileInfo input = ValidateInputPath(options.InputPath);
        string outputPath = ValidateOutputPath(options.OutputPath);
        QemuImgResolution qemuImg = QemuImgResolution.Resolve(options.QemuImgPath);

        PrintDiscovery(qemuImg, output);
        PrintCommand(qemuImg.CommandPath, input.FullName, outputPath, output);

        if (options.DryRun)
        {
            output.WriteLine("QCOW2_NO_WRITE " + options.OutputPath);
            return;
        }

        if (!qemuImg.IsAvailable || qemuImg.ResolvedPath is null)
        {
            throw new ImageBuilderException(14, "qemu_img_not_found", "qemu-img was not found. Pass --qemu-img <path> or add qemu-img to PATH.");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using Process process = new Process();
        process.StartInfo.FileName = qemuImg.ResolvedPath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.ArgumentList.Add("convert");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("raw");
        process.StartInfo.ArgumentList.Add("-O");
        process.StartInfo.ArgumentList.Add("qcow2");
        process.StartInfo.ArgumentList.Add(input.FullName);
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            if (stdout.Length != 0)
            {
                error.WriteLine(stdout.TrimEnd());
            }

            if (stderr.Length != 0)
            {
                error.WriteLine(stderr.TrimEnd());
            }

            throw new ImageBuilderException(15, "qemu_img_failed", "qemu-img convert failed with exit code " + process.ExitCode + ".");
        }

        output.WriteLine("QCOW2_ARTIFACT " + options.OutputPath);
    }

    private static FileInfo ValidateInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(11, "input_not_found", "Raw input image path is empty.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(11, "input_not_found", "Raw input image path is invalid: " + inputPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(11, "input_not_found", "Raw input image was not found: " + inputPath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(12, "input_empty", "Raw input image is empty: " + inputPath);
        }

        return file;
    }

    private static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            return Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
    }

    private static void PrintDiscovery(QemuImgResolution qemuImg, TextWriter output)
    {
        output.Write("QEMU_IMG_DISCOVERY source=");
        output.Write(qemuImg.Source);
        output.Write(" requested=");
        output.Write(qemuImg.RequestedPath);
        output.Write(" resolved=");
        output.Write(qemuImg.ResolvedPath ?? "<not-found>");
        output.Write(" available=");
        output.WriteLine(qemuImg.IsAvailable ? "true" : "false");
    }

    private static void PrintCommand(string qemuImgPath, string inputPath, string outputPath, TextWriter output)
    {
        output.Write("QCOW2_COMMAND ");
        output.Write(Quote(qemuImgPath));
        output.Write(" convert -f raw -O qcow2 ");
        output.Write(Quote(inputPath));
        output.Write(' ');
        output.WriteLine(Quote(outputPath));
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}

internal static class AzureVhdConverter
{
    private const long OneMiB = 1024L * 1024L;

    public static void Convert(ImageBuilderOptions options, TextWriter output, TextWriter error)
    {
        FileInfo input = ImageArtifactInput.ValidateRawInputPath(options.InputPath);
        string outputPath = ImageArtifactInput.ValidateOutputPath(options.OutputPath);
        ValidateAlignedSize(input);

        QemuImgResolution qemuImg = QemuImgResolution.Resolve(options.QemuImgPath);
        PrintDiscovery(qemuImg, output);
        PrintCommand(qemuImg.CommandPath, input.FullName, outputPath, output);

        if (options.DryRun)
        {
            output.WriteLine("AZURE_VHD_NO_WRITE " + options.OutputPath);
            return;
        }

        if (!qemuImg.IsAvailable || qemuImg.ResolvedPath is null)
        {
            throw new ImageBuilderException(14, "qemu_img_not_found", "qemu-img was not found. Pass --qemu-img <path> or add qemu-img to PATH.");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using Process process = new Process();
        process.StartInfo.FileName = qemuImg.ResolvedPath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.ArgumentList.Add("convert");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("raw");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("subformat=fixed,force_size");
        process.StartInfo.ArgumentList.Add("-O");
        process.StartInfo.ArgumentList.Add("vpc");
        process.StartInfo.ArgumentList.Add(input.FullName);
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            if (stdout.Length != 0)
            {
                error.WriteLine(stdout.TrimEnd());
            }

            if (stderr.Length != 0)
            {
                error.WriteLine(stderr.TrimEnd());
            }

            throw new ImageBuilderException(15, "qemu_img_failed", "qemu-img convert failed with exit code " + process.ExitCode + ".");
        }

        output.WriteLine("AZURE_VHD_ARTIFACT " + options.OutputPath);
    }

    private static void ValidateAlignedSize(FileInfo input)
    {
        if (input.Length % OneMiB != 0)
        {
            throw new ImageBuilderException(30, "azure_vhd_unaligned_size", "Azure VHD virtual size must be aligned to 1 MiB.");
        }
    }

    private static void PrintDiscovery(QemuImgResolution qemuImg, TextWriter output)
    {
        output.Write("QEMU_IMG_DISCOVERY source=");
        output.Write(qemuImg.Source);
        output.Write(" requested=");
        output.Write(qemuImg.RequestedPath);
        output.Write(" resolved=");
        output.Write(qemuImg.ResolvedPath ?? "<not-found>");
        output.Write(" available=");
        output.WriteLine(qemuImg.IsAvailable ? "true" : "false");
    }

    private static void PrintCommand(string qemuImgPath, string inputPath, string outputPath, TextWriter output)
    {
        output.Write("AZURE_VHD_COMMAND ");
        output.Write(ImageArtifactInput.Quote(qemuImgPath));
        output.Write(" convert -f raw -o subformat=fixed,force_size -O vpc ");
        output.Write(ImageArtifactInput.Quote(inputPath));
        output.Write(' ');
        output.WriteLine(ImageArtifactInput.Quote(outputPath));
    }
}

internal static class AwsAmiImportPlanner
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw",
        "vhd",
        "vhdx",
        "vmdk",
        "ova"
    };

    public static void Plan(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo input = ImageArtifactInput.ValidateExistingInputPath(options.InputPath);
        string format = NormalizeFormat(options.ImageFormat);
        string architecture = NormalizeRequired(options.Architecture, "architecture");
        string virtualization = NormalizeRequired(options.VirtualizationType, "virtualization");
        string rootDevice = NormalizeRequired(options.RootDeviceType, "root_device");
        string bootMode = NormalizeRequired(options.BootMode, "boot_mode");

        if (!string.Equals(architecture, "x86_64", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(31, "aws_ami_unsupported_architecture", "The first AWS AMI target supports x86_64 only.");
        }

        if (!string.Equals(virtualization, "hvm", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(32, "aws_ami_virtualization_not_hvm", "The first AWS AMI target requires HVM virtualization.");
        }

        if (!string.Equals(rootDevice, "ebs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(33, "aws_ami_root_device_not_ebs", "The first AWS AMI target requires an EBS-backed root volume.");
        }

        if (!string.Equals(bootMode, "uefi", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(bootMode, "legacy-bios", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(34, "aws_ami_invalid_boot_mode", "Boot mode must be uefi or legacy-bios.");
        }

        if (string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase)
            && string.Equals(bootMode, "legacy-bios", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(37, "aws_ami_boot_mode_mismatch", "The current OS raw image layout is GPT/ESP/BOOTX64.EFI and must use uefi metadata.");
        }

        if (!options.EnaSupport)
        {
            throw new ImageBuilderException(35, "aws_ami_ena_required", "Nitro/HVM first target requires ENA support metadata.");
        }

        output.WriteLine("AWS_AMI_PLAN " + JsonSerializer.Serialize(new AwsAmiPlan(
            input.FullName,
            format,
            architecture,
            virtualization,
            rootDevice,
            bootMode,
            options.EnaSupport,
            options.Region ?? string.Empty,
            options.BucketName ?? string.Empty,
            options.VmImportRole ?? string.Empty,
            input.Length)));

        output.Write("AWS_AMI_IMPORT_DRY_RUN ");
        output.Write("format=");
        output.Write(format);
        output.Write(" architecture=");
        output.Write(architecture);
        output.Write(" virtualization=");
        output.Write(virtualization);
        output.Write(" rootDevice=");
        output.Write(rootDevice);
        output.Write(" bootMode=");
        output.Write(bootMode);
        output.Write(" enaSupport=");
        output.WriteLine(options.EnaSupport ? "true" : "false");
    }

    private static string NormalizeFormat(string format)
    {
        string normalized = NormalizeRequired(format, "format");
        if (!SupportedFormats.Contains(normalized))
        {
            throw new ImageBuilderException(36, "aws_ami_unsupported_format", "AWS VM Import dry-run format must be raw, vhd, vhdx, vmdk, or ova.");
        }

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ImageBuilderException(2, "invalid_args", "Missing AWS AMI " + fieldName + ".");
        }

        return value.Trim();
    }

    private readonly record struct AwsAmiPlan(
        string InputPath,
        string Format,
        string Architecture,
        string VirtualizationType,
        string RootDeviceType,
        string BootMode,
        bool EnaSupport,
        string Region,
        string BucketName,
        string VmImportRole,
        long InputBytes);
}

internal static class ImageArtifactMetadataWriter
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw",
        "qcow2",
        "vhd",
        "vhdx",
        "vmdk",
        "ova",
        "iso"
    };

    public static void Describe(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo input = ImageArtifactInput.ValidateExistingInputPath(options.InputPath);
        string format = NormalizeFormat(options.ImageFormat);
        string sha256 = ComputeSha256Hex(input);

        output.WriteLine("IMAGE_METADATA " + JsonSerializer.Serialize(new ImageArtifactMetadata(
            input.FullName,
            format,
            input.Length,
            sha256)));
        output.WriteLine("IMAGE_METADATA_PATH " + input.FullName);
        output.WriteLine("IMAGE_METADATA_FORMAT " + format);
        output.WriteLine("IMAGE_METADATA_BYTES " + input.Length);
        output.WriteLine("IMAGE_METADATA_SHA256 " + sha256);
    }

    private static string NormalizeFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ImageBuilderException(2, "invalid_args", "Image metadata format is required.");
        }

        string normalized = format.Trim().ToLowerInvariant();
        if (!SupportedFormats.Contains(normalized))
        {
            throw new ImageBuilderException(38, "image_metadata_unsupported_format", "Image metadata format must be raw, qcow2, vhd, vhdx, vmdk, ova, or iso.");
        }

        return normalized;
    }

    private static string ComputeSha256Hex(FileInfo input)
    {
        using FileStream stream = input.OpenRead();
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private readonly record struct ImageArtifactMetadata(
        string InputPath,
        string Format,
        long Bytes,
        string Sha256);
}

internal static class ImageArtifactInput
{
    public static FileInfo ValidateRawInputPath(string inputPath)
    {
        return ValidateExistingInputPath(inputPath, "Raw input image");
    }

    public static FileInfo ValidateExistingInputPath(string inputPath)
    {
        return ValidateExistingInputPath(inputPath, "Input image");
    }

    public static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            return Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
    }

    public static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static FileInfo ValidateExistingInputPath(string inputPath, string label)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(11, "input_not_found", label + " path is empty.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(11, "input_not_found", label + " path is invalid: " + inputPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(11, "input_not_found", label + " was not found: " + inputPath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(12, "input_empty", label + " is empty: " + inputPath);
        }

        return file;
    }
}

internal sealed class QemuImgResolution
{
    private QemuImgResolution(string source, string requestedPath, string? resolvedPath, string commandPath)
    {
        Source = source;
        RequestedPath = requestedPath;
        ResolvedPath = resolvedPath;
        CommandPath = commandPath;
    }

    public string Source { get; }

    public string RequestedPath { get; }

    public string? ResolvedPath { get; }

    public string CommandPath { get; }

    public bool IsAvailable => ResolvedPath is not null;

    public static QemuImgResolution Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            string? resolved = ResolveExplicit(explicitPath);
            return new QemuImgResolution("explicit", explicitPath, resolved, resolved ?? explicitPath);
        }

        string? pathResolved = ResolveFromPath();
        return new QemuImgResolution("PATH", "qemu-img", pathResolved, pathResolved ?? "qemu-img");
    }

    private static string? ResolveExplicit(string explicitPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(explicitPath);
            return File.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? ResolveFromPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string[] names = OperatingSystem.IsWindows()
            ? new[] { "qemu-img.exe", "qemu-img" }
            : new[] { "qemu-img" };

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string name in names)
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(Path.Combine(directory, name));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}

internal static class OsPackageBuilder
{
    private const int HeaderSize = 32;
    private const int FileTableEntrySize = 32;
    private const int MaximumFileCount = 128;
    private const int MaximumPathLength = 255;

    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        DirectoryInfo inputDirectory = ValidateInputDirectory(options.InputPath);
        string outputPath = ValidateOutputPath(options.OutputPath);
        PackageSourceFile[] files = LoadSourceFiles(inputDirectory);
        byte[] package = CreatePackage(files);

        ValidatePackageSize(package);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(outputPath, package);
        output.WriteLine("PACKAGE_FILE_COUNT " + files.Length);
        output.WriteLine("PACKAGE_BYTES " + package.Length);
        output.WriteLine("PACKAGE_ARTIFACT " + options.OutputPath);
    }

    public static byte[] CreateSingleFilePackage(string packagePath, byte[] content)
    {
        if (string.IsNullOrEmpty(packagePath))
        {
            throw new ImageBuilderException(
                24,
                "manifest_launch_plan_invalid",
                "Native manifest launch plan did not expose a package-relative app.path.");
        }

        byte[] pathBytes = Encoding.ASCII.GetBytes(packagePath);
        PackageSourceFile[] files = new[]
        {
            new PackageSourceFile(packagePath, pathBytes, content)
        };
        byte[] package = CreatePackage(files);
        ValidatePackageSize(package);
        return package;
    }

    public static byte[] CreatePackageFromDirectory(string inputPath)
    {
        DirectoryInfo inputDirectory = ValidateInputDirectory(inputPath);
        PackageSourceFile[] files = LoadSourceFiles(inputDirectory);
        byte[] package = CreatePackage(files);
        ValidatePackageSize(package);
        return package;
    }

    private static void ValidatePackageSize(byte[] package)
    {
        if (package.Length > ImageBuilderOptions.PackageMaxBytes)
        {
            throw new ImageBuilderException(20, "package_output_too_large", "Package output is larger than 16 MiB.");
        }
    }

    private static DirectoryInfo ValidateInputDirectory(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory is required.");
        }

        DirectoryInfo directory;
        try
        {
            directory = new DirectoryInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory path is invalid: " + inputPath);
        }

        if (!directory.Exists)
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory was not found: " + inputPath);
        }

        return directory;
    }

    private static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            return Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
    }

    private static PackageSourceFile[] LoadSourceFiles(DirectoryInfo inputDirectory)
    {
        EnumerationOptions enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = 0
        };

        List<PackageSourceFile> files = new List<PackageSourceFile>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.Ordinal);
        string root = inputDirectory.FullName;

        foreach (string filePath in Directory.EnumerateFiles(root, "*", enumeration))
        {
            FileInfo file = new FileInfo(filePath);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ImageBuilderException(18, "package_input_unsafe_path", "Package input file is a reparse point: " + filePath);
            }

            string packagePath = NormalizePackagePath(root, file.FullName);
            if (!IsSafePackagePath(packagePath))
            {
                throw new ImageBuilderException(18, "package_input_unsafe_path", "Package input file has an unsafe package path: " + packagePath);
            }

            if (!seenPaths.Add(packagePath))
            {
                throw new ImageBuilderException(19, "package_input_duplicate_path", "Package input has a duplicate normalized path: " + packagePath);
            }

            byte[] content = File.ReadAllBytes(file.FullName);
            files.Add(new PackageSourceFile(packagePath, Encoding.ASCII.GetBytes(packagePath), content));
        }

        files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        if (files.Count == 0)
        {
            throw new ImageBuilderException(17, "package_input_empty", "Package input directory contains no files.");
        }

        if (files.Count > MaximumFileCount)
        {
            throw new ImageBuilderException(20, "package_output_too_large", "Package file count exceeds " + MaximumFileCount + ".");
        }

        return files.ToArray();
    }

    private static string NormalizePackagePath(string root, string filePath)
    {
        string relativePath = Path.GetRelativePath(root, filePath);
        return relativePath
            .Replace(Path.DirectorySeparatorChar.ToString(), "/", StringComparison.Ordinal)
            .Replace(Path.AltDirectorySeparatorChar.ToString(), "/", StringComparison.Ordinal);
    }

    private static bool IsSafePackagePath(string path)
    {
        if (path.Length == 0 || path.Length > MaximumPathLength)
        {
            return false;
        }

        if (path[0] == '/' || path[^1] == '/' || path.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = path.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (int index = 0; index < path.Length; index++)
        {
            char value = path[index];
            if (value < 0x20
                || value >= 0x7F
                || value == '\\'
                || value == ':'
                || value == '*'
                || value == '?'
                || value == '"'
                || value == '<'
                || value == '>'
                || value == '|')
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] CreatePackage(PackageSourceFile[] files)
    {
        int tableOffset = HeaderSize;
        int tableLength = checked(files.Length * FileTableEntrySize);
        int pathOffset = checked(tableOffset + tableLength);
        int contentOffset = pathOffset;

        foreach (PackageSourceFile file in files)
        {
            contentOffset = checked(contentOffset + file.PathBytes.Length);
        }

        int totalLength = contentOffset;
        foreach (PackageSourceFile file in files)
        {
            totalLength = checked(totalLength + file.Content.Length);
        }

        byte[] package = new byte[totalLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, 0);
        WriteUInt16(package, 10, HeaderSize);
        WriteUInt16(package, 12, files.Length);
        WriteUInt16(package, 14, FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);

        int currentPathOffset = pathOffset;
        int currentContentOffset = contentOffset;
        for (int index = 0; index < files.Length; index++)
        {
            PackageSourceFile file = files[index];
            int entryOffset = tableOffset + index * FileTableEntrySize;
            WriteUInt32(package, entryOffset, currentPathOffset);
            WriteUInt16(package, entryOffset + 4, file.PathBytes.Length);
            WriteUInt16(package, entryOffset + 6, 0);
            WriteUInt64(package, entryOffset + 8, (ulong)currentContentOffset);
            WriteUInt64(package, entryOffset + 16, (ulong)file.Content.Length);
            WriteUInt64(package, entryOffset + 24, 0);

            file.PathBytes.CopyTo(package.AsSpan(currentPathOffset, file.PathBytes.Length));
            file.Content.CopyTo(package.AsSpan(currentContentOffset, file.Content.Length));
            currentPathOffset += file.PathBytes.Length;
            currentContentOffset += file.Content.Length;
        }

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

    private sealed class PackageSourceFile
    {
        public PackageSourceFile(string path, byte[] pathBytes, byte[] content)
        {
            Path = path;
            PathBytes = pathBytes;
            Content = content;
        }

        public string Path { get; }

        public byte[] PathBytes { get; }

        public byte[] Content { get; }
    }
}

internal sealed class RawImageLayout
{
    public RawImageLayout(
        string efiPath,
        string outputPath,
        string? manifestPath,
        string? packagePath,
        long efiSizeBytes,
        long manifestSizeBytes,
        long packageSizeBytes,
        int imageSizeMiB,
        long imageBytes,
        long totalSectors,
        long partitionStartLba,
        long partitionLastLba,
        uint partitionSectorCount,
        uint fatSectors,
        uint clusterCount,
        int fileClusterCount,
        int manifestFileClusterCount,
        int packageFileClusterCount,
        int osDirectoryCluster,
        int manifestFileFirstCluster,
        int packageFileFirstCluster)
    {
        EfiPath = efiPath;
        OutputPath = outputPath;
        ManifestPath = manifestPath;
        PackagePath = packagePath;
        EfiSizeBytes = efiSizeBytes;
        ManifestSizeBytes = manifestSizeBytes;
        PackageSizeBytes = packageSizeBytes;
        ImageSizeMiB = imageSizeMiB;
        ImageBytes = imageBytes;
        TotalSectors = totalSectors;
        PartitionStartLba = partitionStartLba;
        PartitionLastLba = partitionLastLba;
        PartitionSectorCount = partitionSectorCount;
        FatSectors = fatSectors;
        ClusterCount = clusterCount;
        FileClusterCount = fileClusterCount;
        ManifestFileClusterCount = manifestFileClusterCount;
        PackageFileClusterCount = packageFileClusterCount;
        OsDirectoryCluster = osDirectoryCluster;
        ManifestFileFirstCluster = manifestFileFirstCluster;
        PackageFileFirstCluster = packageFileFirstCluster;
    }

    public string EfiPath { get; }

    public string OutputPath { get; }

    public string? ManifestPath { get; }

    public string? PackagePath { get; }

    public long EfiSizeBytes { get; }

    public long ManifestSizeBytes { get; }

    public long PackageSizeBytes { get; }

    public bool HasManifest => ManifestSizeBytes != 0;

    public bool HasPackage => PackageSizeBytes != 0;

    public bool HasOsDirectory => HasManifest || HasPackage;

    public int ImageSizeMiB { get; }

    public long ImageBytes { get; }

    public long TotalSectors { get; }

    public long PartitionStartLba { get; }

    public long PartitionLastLba { get; }

    public uint PartitionSectorCount { get; }

    public uint FatSectors { get; }

    public uint ClusterCount { get; }

    public int FileClusterCount { get; }

    public int ManifestFileClusterCount { get; }

    public int PackageFileClusterCount { get; }

    public int OsDirectoryCluster { get; }

    public int ManifestFileFirstCluster { get; }

    public int PackageFileFirstCluster { get; }

    public long PrimaryGptEntriesLba => 2;

    public long BackupGptEntriesLba => TotalSectors - 33;

    public long BackupGptHeaderLba => TotalSectors - 1;

    public long FirstFatLba => PartitionStartLba + RawEspImageBuilder.ReservedSectors;

    public long SecondFatLba => FirstFatLba + FatSectors;

    public long DataStartLba => PartitionStartLba + RawEspImageBuilder.ReservedSectors + RawEspImageBuilder.FatCount * FatSectors;

    public long RootDirectoryLba => ClusterLba(RawEspImageBuilder.RootCluster);

    public long EfiDirectoryLba => ClusterLba(RawEspImageBuilder.EfiDirectoryCluster);

    public long BootDirectoryLba => ClusterLba(RawEspImageBuilder.BootDirectoryCluster);

    public long BootFileLba => ClusterLba(RawEspImageBuilder.BootFileFirstCluster);

    public long OsDirectoryLba => ClusterLba(OsDirectoryCluster);

    public long ManifestFileLba => ClusterLba(ManifestFileFirstCluster);

    public long PackageFileLba => ClusterLba(PackageFileFirstCluster);

    public long ClusterLba(int cluster)
    {
        return DataStartLba + (cluster - 2L) * RawEspImageBuilder.SectorsPerCluster;
    }
}

internal readonly struct RawPackagePlan
{
    public static RawPackagePlan None => new RawPackagePlan(null, 0);

    public RawPackagePlan(string? sourcePath, long sizeBytes)
    {
        SourcePath = sourcePath;
        SizeBytes = sizeBytes;
    }

    public string? SourcePath { get; }

    public long SizeBytes { get; }
}

internal static class ImagePlanPrinter
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Print(RawImageLayout layout, TextWriter writer, string workloadProfile)
    {
        writer.Write("IMAGE_PLAN ");
        writer.WriteLine(JsonSerializer.Serialize(
            new PlanSummary(
                "dry-run",
                "raw",
                workloadProfile,
                layout.OutputPath,
                layout.ImageSizeMiB,
                layout.ImageBytes,
                RawEspImageBuilder.SectorSize,
                layout.TotalSectors,
                writesSuppressed: true),
            SerializerOptions));

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
        yield return PlanEntry.Planned(
            11,
            "efi-boot-file",
            "UEFI removable-media boot file",
            layout.BootFileLba,
            layout.FileClusterCount,
            bytes: layout.EfiSizeBytes,
            guestPath: @"\EFI\BOOT\BOOTX64.EFI",
            sourcePath: layout.EfiPath);

        int order = 12;
        if (layout.HasOsDirectory)
        {
            yield return PlanEntry.Planned(
                order++,
                "fat32-os-directory",
                "FAT32 OS directory",
                layout.OsDirectoryLba,
                RawEspImageBuilder.SectorsPerCluster,
                guestPath: @"\OS");
        }

        if (layout.HasManifest)
        {
            yield return PlanEntry.Planned(
                order++,
                "manifest-file",
                "CloudReady manifest",
                layout.ManifestFileLba,
                layout.ManifestFileClusterCount,
                bytes: layout.ManifestSizeBytes,
                guestPath: @"\OS\MANIFEST.JSON",
                sourcePath: layout.ManifestPath);
        }
        else
        {
            yield return PlanEntry.Placeholder(
                order++,
                "manifest-placeholder",
                "CloudReady manifest placeholder",
                guestPath: @"\OS\MANIFEST.JSON",
                reason: "manifest_package_placement_pending");
        }

        if (layout.HasPackage)
        {
            yield return PlanEntry.Planned(
                order++,
                "app-package-file",
                "CloudReady app package",
                layout.PackageFileLba,
                layout.PackageFileClusterCount,
                bytes: layout.PackageSizeBytes,
                guestPath: @"\OS\APP.PKG",
                sourcePath: layout.PackagePath);
        }
        else
        {
            yield return PlanEntry.Placeholder(
                order++,
                "app-package-placeholder",
                "CloudReady app package placeholder",
                guestPath: @"\OS\APP.PKG",
                reason: "app_package_placement_pending");
        }

        yield return PlanEntry.Planned(order++, "backup-gpt-entries", "Backup GPT entries", layout.BackupGptEntriesLba, 32);
        yield return PlanEntry.Planned(order, "backup-gpt-header", "Backup GPT header", layout.BackupGptHeaderLba, 1);
    }

    private sealed class PlanSummary
    {
        public PlanSummary(
            string mode,
            string format,
            string workloadProfile,
            string outputPath,
            int imageSizeMiB,
            long imageBytes,
            int sectorSize,
            long totalSectors,
            bool writesSuppressed)
        {
            Mode = mode;
            Format = format;
            WorkloadProfile = workloadProfile;
            OutputPath = outputPath;
            ImageSizeMiB = imageSizeMiB;
            ImageBytes = imageBytes;
            SectorSize = sectorSize;
            TotalSectors = totalSectors;
            WritesSuppressed = writesSuppressed;
        }

        public string Mode { get; }

        public string Format { get; }

        public string WorkloadProfile { get; }

        public string OutputPath { get; }

        public int ImageSizeMiB { get; }

        public long ImageBytes { get; }

        public int SectorSize { get; }

        public long TotalSectors { get; }

        public bool WritesSuppressed { get; }
    }

    private sealed class PlanEntry
    {
        private PlanEntry(
            int order,
            string kind,
            string name,
            string status,
            long? lba,
            long? offsetBytes,
            long? sectors,
            long? bytes,
            string? guestPath,
            string? sourcePath,
            string? reason)
        {
            Order = order;
            Kind = kind;
            Name = name;
            Status = status;
            Lba = lba;
            OffsetBytes = offsetBytes;
            Sectors = sectors;
            Bytes = bytes;
            GuestPath = guestPath;
            SourcePath = sourcePath;
            Reason = reason;
        }

        public int Order { get; }

        public string Kind { get; }

        public string Name { get; }

        public string Status { get; }

        public long? Lba { get; }

        public long? OffsetBytes { get; }

        public long? Sectors { get; }

        public long? Bytes { get; }

        public string? GuestPath { get; }

        public string? SourcePath { get; }

        public string? Reason { get; }

        public static PlanEntry Planned(
            int order,
            string kind,
            string name,
            long lba,
            long sectors,
            long? bytes = null,
            string? guestPath = null,
            string? sourcePath = null)
        {
            return new PlanEntry(
                order,
                kind,
                name,
                "planned",
                lba,
                lba * RawEspImageBuilder.SectorSize,
                sectors,
                bytes ?? sectors * RawEspImageBuilder.SectorSize,
                guestPath,
                sourcePath,
                reason: null);
        }

        public static PlanEntry Placeholder(
            int order,
            string kind,
            string name,
            string guestPath,
            string reason)
        {
            return new PlanEntry(
                order,
                kind,
                name,
                "placeholder",
                lba: null,
                offsetBytes: null,
                sectors: null,
                bytes: null,
                guestPath,
                sourcePath: null,
                reason);
        }
    }
}

internal static class RawEspImageBuilder
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
    private const string DefaultManifestText =
        "format=os-native-manifest\n" +
        "version=0\n" +
        "app.name=os.default\n" +
        "app.path=apps/hello.bin\n" +
        "args=\n" +
        "network=none\n" +
        "log=serial\n";
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
        FatLayout fat = FatLayout.Create(
            partitionSectorCount,
            checked((int)efiFile.Length),
            manifestFile is null ? DefaultManifestBytes().Length : checked((int)manifestFile.Length),
            checked((int)packagePlan.SizeBytes));

        return new RawImageLayout(
            options.EfiPath,
            options.OutputPath,
            manifestFile is null ? null : options.ManifestPath,
            packagePlan.SourcePath,
            efiFile.Length,
            manifestFile?.Length ?? DefaultManifestBytes().Length,
            packagePlan.SizeBytes,
            options.ImageSizeMiB,
            imageBytes,
            totalSectors,
            PartitionStartLba,
            partitionLastLba,
            partitionSectorCount,
            fat.FatSectors,
            fat.ClusterCount,
            fat.FileClusterCount,
            fat.ManifestFileClusterCount,
            fat.PackageFileClusterCount,
            fat.OsDirectoryCluster,
            fat.ManifestFileFirstCluster,
            fat.PackageFileFirstCluster);
    }

    private static RawPackagePlan PlanPackageInput(ImageBuilderOptions options)
    {
        if (options.KestrelPackageRootPath is not null)
        {
            byte[] packageBytes = BuildKestrelPackageBytes(options);
            return new RawPackagePlan(options.KestrelPackageRootPath, packageBytes.Length);
        }

        FileInfo? packageFile = ValidatePackagePath(options.PackagePath);
        return packageFile is null
            ? RawPackagePlan.None
            : new RawPackagePlan(options.PackagePath, packageFile.Length);
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
            throw new ImageBuilderException(
                30,
                "kestrel_package_missing_manifest",
                "--kestrel-package requires --manifest.");
        }

        string packageRootPath = options.KestrelPackageRootPath
            ?? throw new ImageBuilderException(
                30,
                "kestrel_package_invalid_root",
                "--kestrel-package root is required.");

        ValidationResult validation = KestrelSmokePackageContractValidator.Validate(
            options.ManifestPath,
            packageRootPath,
            options.WorkloadProfile,
            KestrelSmokePackageContractValidator.ContractId);
        if (!validation.Succeeded)
        {
            throw new ImageBuilderException(30, validation.Code, validation.Message);
        }

        byte[] packageBytes = OsPackageBuilder.CreatePackageFromDirectory(packageRootPath);
        OsPackageParseResult package = OsPackageParser.Parse(packageBytes);
        if (!package.Succeeded)
        {
            throw new ImageBuilderException(
                30,
                "kestrel_package_output_invalid",
                "Generated Kestrel package failed OSPKG validation: " + package.Status + ".");
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
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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

    private static void WriteLongFileNameChars(
        byte[] directory,
        int offset,
        int count,
        string fileName,
        ref int fileNameIndex,
        ref bool wroteTerminator)
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

    private readonly struct FatLayout
    {
        private FatLayout(uint fatSectors, uint clusterCount, int fileClusterCount, int manifestFileClusterCount, int packageFileClusterCount)
        {
            FatSectors = fatSectors;
            ClusterCount = clusterCount;
            FileClusterCount = fileClusterCount;
            ManifestFileClusterCount = manifestFileClusterCount;
            PackageFileClusterCount = packageFileClusterCount;
        }

        public uint FatSectors { get; }

        public uint ClusterCount { get; }

        public int FileClusterCount { get; }

        public int ManifestFileClusterCount { get; }

        public int PackageFileClusterCount { get; }

        public bool HasManifest => ManifestFileClusterCount != 0;

        public bool HasPackage => PackageFileClusterCount != 0;

        public bool HasOsDirectory => HasManifest || HasPackage;

        public int OsDirectoryCluster => BootFileFirstCluster + FileClusterCount;

        public int ManifestFileFirstCluster => OsDirectoryCluster + 1;

        public int PackageFileFirstCluster => ManifestFileFirstCluster + ManifestFileClusterCount;

        public int NextFreeCluster => PackageFileFirstCluster + PackageFileClusterCount;

        public int UsedClusterCount => 3 + FileClusterCount + (HasOsDirectory ? 1 + ManifestFileClusterCount + PackageFileClusterCount : 0);

        public static FatLayout Create(uint partitionSectorCount, int efiLength, int manifestLength, int packageLength)
        {
            int fileClusterCount = checked((efiLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            int manifestFileClusterCount = manifestLength == 0
                ? 0
                : checked((manifestLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            int packageFileClusterCount = packageLength == 0
                ? 0
                : checked((packageLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            // Ignoring the FATs gives an upper bound on the data clusters.
            // Size for that bound, then derive the actual count from the same FAT size.
            uint maximumClusterCount = checked((partitionSectorCount - ReservedSectors) / SectorsPerCluster);
            uint fatSectors = DivRoundUp(checked(maximumClusterCount + 2), SectorSize / 4);
            uint clusterCount = checked((partitionSectorCount - ReservedSectors - FatCount * fatSectors) / SectorsPerCluster);

            // The last data cluster is count + 1; 0x0FFFFFF7 is the bad-cluster sentinel.
            if (clusterCount >= 0x0FFFFFF6)
            {
                throw new ImageBuilderException(5, "image_too_large", "Image exceeds the FAT32 allocatable cluster-number range.");
            }

            if (clusterCount <= 65525)
            {
                throw new ImageBuilderException(5, "image_too_small", "FAT32 ESP requires more than 65525 clusters with the current layout.");
            }

            int requiredNextCluster = BootFileFirstCluster + fileClusterCount;
            if (manifestFileClusterCount != 0 || packageFileClusterCount != 0)
            {
                requiredNextCluster += 1 + manifestFileClusterCount + packageFileClusterCount;
            }

            if ((uint)requiredNextCluster >= clusterCount)
            {
                throw new ImageBuilderException(6, "efi_too_large", "EFI artifact does not fit in the requested ESP image.");
            }

            return new FatLayout(fatSectors, clusterCount, fileClusterCount, manifestFileClusterCount, packageFileClusterCount);
        }

        private static uint DivRoundUp(uint value, int divisor)
        {
            return checked((uint)(((ulong)value + (uint)divisor - 1) / (uint)divisor));
        }
    }
}

internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320;

    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFF_FFFF;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                uint mask = 0u - (crc & 1u);
                crc = (crc >> 1) ^ (Polynomial & mask);
            }
        }

        return ~crc;
    }
}

internal sealed class ImageBuilderException : Exception
{
    public ImageBuilderException(int exitCode, string reason, string message)
        : base(message)
    {
        ExitCode = exitCode;
        Reason = reason;
    }

    public int ExitCode { get; }

    public string Reason { get; }
}
