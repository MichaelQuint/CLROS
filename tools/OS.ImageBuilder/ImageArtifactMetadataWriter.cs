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

internal static partial class ImageArtifactMetadataWriter
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
        output.WriteLine("IMAGE_METADATA " + JsonSerializer.Serialize(new ImageArtifactMetadata(input.FullName, format, input.Length, sha256)));
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
}
