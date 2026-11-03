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
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
        catch (Exception ex)when (ex is ArgumentException or NotSupportedException or PathTooLongException)
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
