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
