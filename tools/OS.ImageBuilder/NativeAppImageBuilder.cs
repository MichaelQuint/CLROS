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
        string candidate = string.IsNullOrWhiteSpace(packagePath) ? rawOutputPath + ".app.pkg" : packagePath;
        string outputPath = NativePayloadBuilder.ValidateOutputPath(candidate);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(outputPath, rawOutputPath, comparison))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Native app image package output must not match raw image output.");
        }

        return outputPath;
    }
}
