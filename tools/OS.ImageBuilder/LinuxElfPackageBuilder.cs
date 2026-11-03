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

internal static class LinuxElfPackageBuilder
{
    public static byte[] Create(byte[] manifest, byte[] payload)
    {
        if (manifest.Length == 0 || manifest.Length > OS.Manifest.LinuxElfPackage.MaximumManifestBytes || payload.Length < 64 || (ulong)manifest.Length + (ulong)payload.Length + 64 > OS.Manifest.LinuxElfPackage.MaximumBytes)
            throw new ImageBuilderException(20, "linux_elf_length", "Linux ELF manifest/payload exceeds the bounded package format.");
        byte[] bytes = new byte[64 + manifest.Length + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, OS.Manifest.LinuxElfPackage.Magic);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 64);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), (uint)manifest.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), (ulong)payload.Length);
        manifest.CopyTo(bytes, 64);
        payload.CopyTo(bytes, 64 + manifest.Length);
        var admitted = OS.Manifest.LinuxElfPackage.Validate(bytes);
        if (!admitted.Succeeded)
            throw new ImageBuilderException(20, "linux_elf_invalid", "Linux ELF package admission failed: " + admitted.Status);
        return bytes;
    }

    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        if (options.DryRun)
            throw new ImageBuilderException(20, "linux_elf_dry_run", "A package requires actual input bytes.");
        byte[] manifest = Read(options.ManifestPath, OS.Manifest.LinuxElfPackage.MaximumManifestBytes);
        byte[] payload = Read(options.InputPath, OS.Manifest.LinuxElfPackage.MaximumBytes - 64);
        byte[] bytes = Create(manifest, payload);
        string path = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(bytes);
            file.Flush(true);
        }

        byte[] retained = File.ReadAllBytes(path);
        if (!retained.AsSpan().SequenceEqual(bytes))
            throw new ImageBuilderException(20, "linux_elf_readback", "Written package bytes differ.");
        output.WriteLine("LINUX_ELF_PACKAGE bytes=" + bytes.Length + " sha256=" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static byte[] Read(string? path, uint maximum)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ImageBuilderException(20, "linux_elf_input", "Manifest and ELF input paths are required.");
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || (ulong)stream.Length > maximum)
            throw new ImageBuilderException(20, "linux_elf_length", "Input size is outside the package limit.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new ImageBuilderException(20, "linux_elf_input_changed", "Input size changed during read.");
        return bytes;
    }
}
