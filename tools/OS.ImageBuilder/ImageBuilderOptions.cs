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
            else if (string.Equals(args[0], "plan-raw", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "plan", StringComparison.OrdinalIgnoreCase))
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
            else if (string.Equals(args[0], "build-linux-elf-package", StringComparison.OrdinalIgnoreCase))
            {
                options.Command = ImageBuilderCommand.BuildLinuxElfPackage;
                options.InputPath = string.Empty;
                options.OutputPath = Path.Combine("artifacts", "packages", "linux-elf.pkg");
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
                    if (options.Command == ImageBuilderCommand.ConvertQcow2 || options.Command == ImageBuilderCommand.ConvertAzureVhd || options.Command == ImageBuilderCommand.PlanAwsAmi)
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

        if (options.Command != ImageBuilderCommand.CreateRaw && options.Command != ImageBuilderCommand.PlanRaw)
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

        if (string.Equals(args[0], "plan-raw", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "plan", StringComparison.OrdinalIgnoreCase))
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
            ImageBuilderCommand.BuildLinuxElfPackage => "m16-002.package",
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
        Console.WriteLine("OS.ImageBuilder build-linux-elf-package --manifest <v1-line-manifest> --input <static-ELF> --output <new-package>");
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
