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
            throw new ImageBuilderException(23, "manifest_malformed", "Native manifest parse failed: status=" + manifest.Status + " offset=" + manifest.ErrorOffset + ".");
        }

        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(manifestBytes, manifest.Manifest, BootInfo.CreateHost(BootCapabilities.TextOutput), NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        if (!launchPlan.Succeeded)
        {
            throw new ImageBuilderException(24, "manifest_launch_plan_invalid", "Native manifest launch plan failed: status=" + launchPlan.Status + ".");
        }

        OsPackageParseResult package = OsPackageParser.Parse(packageBytes);
        if (!package.Succeeded)
        {
            throw new ImageBuilderException(25, "package_malformed", "Package parse failed: status=" + package.Status + ".");
        }

        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(manifestBytes, packageBytes, launchPlan);
        if (!descriptor.Succeeded)
        {
            ThrowDescriptorFailure(descriptor);
        }

        if (!descriptor.Descriptor.Plan.TryGetAppPath(manifestBytes, out ReadOnlySpan<byte> appPath))
        {
            throw new ImageBuilderException(24, "manifest_launch_plan_invalid", "Native manifest launch plan did not expose app.path.");
        }

        return new PackagePayloadValidation(Encoding.ASCII.GetString(appPath), descriptor.Descriptor.AppBinaryLength, descriptor.Descriptor.PayloadHeader.EntryOffset);
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

    private static void ThrowDescriptorFailure(NativeAppLaunchDescriptorResult descriptor)
    {
        string message = "Package app validation failed: status=" + descriptor.Status + " launch=" + descriptor.LaunchStatus + " packageFile=" + descriptor.PackageFileStatus + " package=" + descriptor.PackageStatus + " payload=" + descriptor.PayloadHeaderStatus + ".";
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
