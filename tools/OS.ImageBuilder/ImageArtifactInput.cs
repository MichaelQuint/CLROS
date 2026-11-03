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
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
