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
            throw new ImageBuilderException(23, "manifest_malformed", "Native manifest parse failed: status=" + manifest.Status + " offset=" + manifest.ErrorOffset + ".");
        }

        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(manifestBytes, manifest.Manifest, BootInfo.CreateHost(BootCapabilities.TextOutput), NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        if (!launchPlan.Succeeded)
        {
            throw new ImageBuilderException(24, "manifest_launch_plan_invalid", "Native manifest launch plan failed: status=" + launchPlan.Status + ".");
        }

        if (!launchPlan.Plan.TryGetAppPath(manifestBytes, out ReadOnlySpan<byte> appPath) || appPath.Length == 0)
        {
            throw new ImageBuilderException(24, "manifest_launch_plan_invalid", "Native manifest launch plan did not expose app.path.");
        }

        return Encoding.ASCII.GetString(appPath);
    }
}
