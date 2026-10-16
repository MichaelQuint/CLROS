using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only creation of the native V2 immutable-config envelope.
/// ELF execution, guest startup and Kestrel acceptance remain separate. The
/// sole data file is appsettings.json; no TPA/CoreCLR payloads are introduced.</summary>
public static class NativeAotWebPackage
{
    public const string SettingsPath = "apps/appsettings.json";

    /// <summary>Creates owned package bytes from a supplied static ELF and a
    /// bounded JSON object. This validates format/digests, not producer identity.
    /// Missing/unsupported/oversized inputs throw before returning any package.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, ReadOnlySpan<byte> settings)
    {
        if (elf.Length > LinuxElfPackage.MaximumBytes || settings.Length is <= 0 or > (int)LinuxElfPackage.MaximumDataBytes)
            throw new InvalidDataException("web_package_input_length");
        NativeAotPayload.Inspection image = NativeAotPayload.Inspect(elf);
        using JsonDocument json = JsonDocument.Parse(settings.ToArray());
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("web_package_config_object");
        string manifest = "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
            "app.path=apps/MinimalApiSmoke\nargs=--urls http://0.0.0.0:8080\ndigest=" + image.Sha256.ToLowerInvariant() +
            "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" +
            "env.CLROS_USER_IRQS=1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n";
        byte[] text = Encoding.ASCII.GetBytes(manifest), path = Encoding.ASCII.GetBytes(SettingsPath);
        int recordBytes = checked((int)LinuxElfPackage.DataRecordHeaderBytes + path.Length + settings.Length);
        int length = checked((int)LinuxElfPackage.HeaderBytes + text.Length + elf.Length + recordBytes);
        if (length > LinuxElfPackage.MaximumBytes) throw new InvalidDataException("web_package_length");
        byte[] package = new byte[length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        W32(package, 8, 2); W32(package, 12, 2); W32(package, 16, LinuxElfPackage.HeaderBytes);
        W32(package, 20, (uint)text.Length); BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        W32(package, 32, (uint)recordBytes); W32(package, 36, 1);
        text.CopyTo(package, (int)LinuxElfPackage.HeaderBytes);
        int at = (int)LinuxElfPackage.HeaderBytes + text.Length;
        elf.CopyTo(package.AsSpan(at)); at += elf.Length;
        W32(package, at, (uint)recordBytes); W32(package, at + 4, (uint)path.Length); W32(package, at + 8, (uint)settings.Length);
        SHA256.HashData(settings).CopyTo(package, at + 16);
        path.CopyTo(package, at + (int)LinuxElfPackage.DataRecordHeaderBytes);
        settings.CopyTo(package.AsSpan(at + (int)LinuxElfPackage.DataRecordHeaderBytes + path.Length));
        LinuxElfPackageView view = LinuxElfPackage.Validate(package);
        if (!view.Succeeded || view.DataFileCount != 1) throw new InvalidDataException("web_package_admission " + view.Status);
        return package;
    }

    /// <summary>Packages exact retained Linux web outputs after source/native/
    /// output revalidation under the original root's lease. Owns only a fresh
    /// file beneath the calling root's artifacts directory. No guest is started.</summary>
    public static string Write(string root, string recipeRoot, string evidence, string destination)
    {
        root = Path.GetFullPath(root); recipeRoot = Path.GetFullPath(recipeRoot);
        evidence = Path.GetFullPath(evidence); destination = Path.GetFullPath(destination);
        using var lease = NativeAotPayload.OpenQualificationLease(recipeRoot);
        NativeAotWebPayload.ValidateQualification(recipeRoot, evidence);
        if (!destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(destination))
            throw new InvalidDataException("web_package_fresh_artifacts_output");
        string elf = Path.Combine(evidence, "MinimalApiSmoke", "MinimalApiSmoke");
        string settings = Path.Combine(evidence, "MinimalApiSmoke", "appsettings.json");
        if (new FileInfo(elf).Length > LinuxElfPackage.MaximumBytes ||
            new FileInfo(settings).Length > LinuxElfPackage.MaximumDataBytes) throw new InvalidDataException("web_package_input_length");
        byte[] package = Create(File.ReadAllBytes(elf), File.ReadAllBytes(settings));
        EfiBuildFiles.InitializeDirectory(Path.GetDirectoryName(destination)!, "web_package_output_path");
        EfiBuildFiles.RequirePlainOrMissing(root, destination, "web_package_output_path");
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) file.Write(package);
        string hash = Convert.ToHexString(SHA256.HashData(package));
        if (NativeAotWebPayload.HashFile(destination) != hash) throw new InvalidDataException("web_package_write_changed");
        return hash;
    }

    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
}
