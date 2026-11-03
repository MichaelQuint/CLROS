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

internal static partial class AwsAmiImportPlanner
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

        if (!string.Equals(bootMode, "uefi", StringComparison.OrdinalIgnoreCase) && !string.Equals(bootMode, "legacy-bios", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(34, "aws_ami_invalid_boot_mode", "Boot mode must be uefi or legacy-bios.");
        }

        if (string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase) && string.Equals(bootMode, "legacy-bios", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImageBuilderException(37, "aws_ami_boot_mode_mismatch", "The current OS raw image layout is GPT/ESP/BOOTX64.EFI and must use uefi metadata.");
        }

        if (!options.EnaSupport)
        {
            throw new ImageBuilderException(35, "aws_ami_ena_required", "Nitro/HVM first target requires ENA support metadata.");
        }

        output.WriteLine("AWS_AMI_PLAN " + JsonSerializer.Serialize(new AwsAmiPlan(input.FullName, format, architecture, virtualization, rootDevice, bootMode, options.EnaSupport, options.Region ?? string.Empty, options.BucketName ?? string.Empty, options.VmImportRole ?? string.Empty, input.Length)));
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
}
